using System.Text.Json;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataSources.Common;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Records;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Borc.DataMapper.Application.Imports.RunImportJob;

/// <summary>
/// Runs the pending background step of an import (sent by the import worker, never by a page):
/// <list type="bullet">
/// <item>Validating → checks every row against the stored column mapping (type, required, length, pattern and, for list
/// fields, that the value is one of the data source's values), in chunks of <see cref="ImportLimits.ChunkSize"/>.</item>
/// <item>Committing → turns the valid rows into records, a chunk at a time: each chunk's records, its rows' "imported"
/// mark and the progress are saved together, so a stopped commit continues where it left off.</item>
/// </list>
/// Each chunk is saved and then forgotten by the change tracker, so memory stays flat for large files.
/// Records get the batch's unit and its uploader as author (there is no signed-in user in the background).
/// </summary>
public sealed record RunImportJobCommand(long BatchId) : IRequest<Result>;

public sealed class RunImportJobHandler : IRequestHandler<RunImportJobCommand, Result>
{
    private readonly IAppDbContext _db;
    private readonly DataSourceOptionService _options;
    private readonly ILogger<RunImportJobHandler> _logger;

    public RunImportJobHandler(IAppDbContext db, DataSourceOptionService options, ILogger<RunImportJobHandler> logger)
    {
        _db = db;
        _options = options;
        _logger = logger;
    }

    public async Task<Result> Handle(RunImportJobCommand request, CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches.FirstOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken);

        if (batch is null)
            return Result.Failure("ایمپورت موردنظر پیدا نشد.");

        try
        {
            return batch.Status switch
            {
                ImportBatchStatus.Validating => await ValidateAsync(batch, cancellationToken),
                ImportBatchStatus.Committing => await CommitAsync(batch, cancellationToken),
                _ => Result.Ok("کاری برای انجام نیست.")
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // shutting down: the worker picks the batch up again at the next start
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import {BatchId}: background step failed.", request.BatchId);
            return await FailAsync(request.BatchId, cancellationToken);
        }
    }

    // ---------------------------------------------------------------- validation

    private async Task<Result> ValidateAsync(ImportBatch batch, CancellationToken ct)
    {
        var version = await _db.TemplateVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == batch.TemplateVersionId, ct);

        if (version is null || version.Status != TemplateVersionStatus.Published)
            return await StopValidationAsync(batch, "نسخهٔ این ایمپورت دیگر منتشرشده نیست.", ct);

        var rows = await _db.TemplateFields.AsNoTracking()
            .Where(f => f.TemplateVersionId == version.Id)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .ToListAsync(ct);

        var fields = rows
            .Select(f => new TargetField(f.Id, f.FieldKey, f.Label, f.DataType, f.IsRequired,
                f.Length, f.Precision, f.Scale, f.Regex, f.DefaultValue))
            .ToList();

        var sourceOf = rows.Where(f => f.DataSourceId.HasValue)
            .ToDictionary(f => f.FieldKey, f => f.DataSourceId!.Value, StringComparer.OrdinalIgnoreCase);

        var mapping = string.IsNullOrWhiteSpace(batch.MappingJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(batch.MappingJson);

        if (mapping is null || mapping.Count == 0)
            return await StopValidationAsync(batch, "نگاشت ستون‌ها پیدا نشد؛ دوباره تطبیق دهید.", ct);

        var headerOfField = mapping.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

        // the values of each list source, loaded once for the whole file
        var sourceValues = new Dictionary<long, (IReadOnlySet<string>? Values, string? Error)>();
        foreach (var id in sourceOf.Values.Distinct())
            sourceValues[id] = await _options.GetValueSetAsync(id, ct);

        var valid = 0;
        var invalid = 0;
        long lastId = 0;

        while (true)
        {
            var currentLastId = lastId;

            var chunk = await _db.ImportRows
                .Where(r => r.ImportBatchId == batch.Id && r.Id > currentLastId)
                .OrderBy(r => r.Id)
                .Take(ImportLimits.ChunkSize)
                .ToListAsync(ct);

            if (chunk.Count == 0)
                break;

            foreach (var row in chunk)
            {
                var raw = ImportJson.ParseRaw(row.RawDataJson)
                    .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

                var values = new List<KeyValuePair<string, object?>>();
                var errors = new List<string>();

                foreach (var field in fields)
                {
                    string? rawValue = null;

                    if (headerOfField.TryGetValue(field.Key, out var header))
                        raw.TryGetValue(header, out rawValue);

                    var result = ImportValueValidator.Validate(field, rawValue);

                    if (result.Error is null && result.Value is not null && sourceOf.TryGetValue(field.Key, out var sourceId))
                    {
                        var (set, sourceError) = sourceValues[sourceId];
                        var text = result.Value.ToString() ?? string.Empty;

                        if (sourceError is not null)
                            result = new FieldValue(result.Value, $"«{field.Label}»: {sourceError}");
                        else if (!set!.Contains(text))
                            result = new FieldValue(result.Value, $"«{field.Label}»: مقدار «{text}» در منبع داده وجود ندارد.");
                    }

                    values.Add(new KeyValuePair<string, object?>(field.Key, result.Value));

                    if (result.Error is not null)
                        errors.Add(result.Error);
                }

                row.SetMapped(ImportJson.BuildMapped(values));

                if (errors.Count == 0)
                {
                    row.MarkValid();
                    valid++;
                }
                else
                {
                    row.MarkInvalid(errors.Count, ImportJson.BuildErrors(errors));
                    invalid++;
                }
            }

            batch.ReportProgress(valid + invalid);
            await _db.SaveChangesAsync(ct);
            lastId = chunk[^1].Id;
            Forget<ImportRow>();
        }

        batch.CompleteValidation(valid + invalid, valid, invalid);
        await _db.SaveChangesAsync(ct);

        return Result.Ok($"اعتبارسنجی انجام شد: {valid} سطر معتبر و {invalid} سطر نامعتبر.");
    }

    private async Task<Result> StopValidationAsync(ImportBatch batch, string error, CancellationToken ct)
    {
        batch.FailValidation(error);
        await _db.SaveChangesAsync(ct);
        return Result.Failure(error);
    }

    // ---------------------------------------------------------------- commit

    private async Task<Result> CommitAsync(ImportBatch batch, CancellationToken ct)
    {
        var version = await _db.TemplateVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == batch.TemplateVersionId, ct);

        if (version is null || version.Status != TemplateVersionStatus.Published)
        {
            const string error = "نسخهٔ این ایمپورت دیگر منتشرشده نیست؛ ثبت نهایی ممکن نیست.";
            batch.FailCommit(error);
            await _db.SaveChangesAsync(ct);
            return Result.Failure(error);
        }

        // rows committed by an earlier, interrupted run count as done
        var done = await _db.ImportRows.CountAsync(r => r.ImportBatchId == batch.Id && r.Status == ImportRowStatus.Imported, ct);
        var added = 0;

        while (true)
        {
            var chunk = await _db.ImportRows
                .Where(r => r.ImportBatchId == batch.Id && r.Status == ImportRowStatus.Valid)
                .OrderBy(r => r.Id)
                .Take(ImportLimits.ChunkSize)
                .ToListAsync(ct);

            if (chunk.Count == 0)
                break;

            foreach (var row in chunk)
            {
                _db.DataRecords.Add(DataRecord.CreateFromImport(
                    version.TemplateId,
                    version.Id,
                    row.MappedDataJson!,
                    batch.Id,
                    row.Id,
                    batch.OrgUnitKey,     // records belong to the import's unit …
                    batch.CreatedBy));    // … and to whoever uploaded the file

                row.MarkImported();
            }

            added += chunk.Count;
            batch.ReportProgress(done + added);
            await _db.SaveChangesAsync(ct); // records, row marks and progress together
            Forget<ImportRow>();
            Forget<DataRecord>();
        }

        batch.MarkImported();
        await _db.SaveChangesAsync(ct);

        return Result.Ok($"{done + added} رکورد ثبت شد.");
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Stops tracking saved rows/records so the change tracker does not grow with the file.</summary>
    private void Forget<T>() where T : class
    {
        foreach (var entry in _db.ChangeTracker.Entries<T>().ToList())
            entry.State = EntityState.Detached;
    }

    /// <summary>An unexpected error: put the batch back to the step before, with a message, from a clean tracker.</summary>
    private async Task<Result> FailAsync(long batchId, CancellationToken ct)
    {
        const string error = "پردازش ایمپورت با خطا متوقف شد؛ دوباره تلاش کنید. اگر تکرار شد به پشتیبانی اطلاع دهید.";

        _db.ChangeTracker.Clear();
        var batch = await _db.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);

        if (batch?.Status == ImportBatchStatus.Validating)
            batch.FailValidation(error);
        else if (batch?.Status == ImportBatchStatus.Committing)
            batch.FailCommit(error);

        await _db.SaveChangesAsync(ct);
        return Result.Failure(error);
    }
}
