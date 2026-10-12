using System.Threading.Channels;
using Borc.DataMapper.Application.Abstractions.Imports;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Imports.RunImportJob;
using Borc.DataMapper.Domain.Imports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Web.Background;

/// <summary>In-process queue of import batches whose background step (validation or commit) should run.</summary>
public sealed class ImportJobQueue : IImportJobQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<long> Reader => _channel.Reader;

    public void Enqueue(long importBatchId) => _channel.Writer.TryWrite(importBatchId);
}

/// <summary>
/// Runs queued import steps one after another, each in its own DI scope (no HTTP request: data visibility is
/// "everything" and records are stamped explicitly). At start it resumes batches a stop left "validating" or
/// "committing" — a commit continues with the rows not yet turned into records.
/// One worker per application instance; with several servers on one database a batch could be resumed twice
/// (a resumed commit is still safe: only rows still marked valid are committed, chunk by chunk).
/// </summary>
public sealed class ImportJobWorker(ImportJobQueue queue, IServiceScopeFactory scopes, ILogger<ImportJobWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ResumeAsync(stoppingToken);

        try
        {
            await foreach (long id in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunImportJobCommand(id), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Import {BatchId}: background job crashed.", id);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ResumeAsync(CancellationToken ct)
    {
        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            List<long> pending = await db.ImportBatches.AsNoTracking()
                .Where(b => b.Status == ImportBatchStatus.Validating || b.Status == ImportBatchStatus.Committing)
                .OrderBy(b => b.Id)
                .Select(b => b.Id)
                .ToListAsync(ct);

            foreach (long id in pending)
                queue.Enqueue(id);

            if (pending.Count > 0)
                logger.LogInformation("Resuming {Count} interrupted import(s).", pending.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not look for interrupted imports.");
        }
    }
}
