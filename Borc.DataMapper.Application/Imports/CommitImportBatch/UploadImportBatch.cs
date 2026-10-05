using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Records;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Imports.CommitImportBatch;

/// <summary>ثبت نهایی: سطرهای معتبر به DataRecord تبدیل می‌شوند؛ سطرهای نامعتبر نادیده گرفته می‌شوند.</summary>
public sealed record CommitImportBatchCommand(long Id) : IRequest<Result>;

public sealed class CommitImportBatchHandler
    : IRequestHandler<CommitImportBatchCommand, Result>
{
    private readonly IAppDbContext _db;

    public CommitImportBatchHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        CommitImportBatchCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (batch is null)
            return Result.Failure("ایمپورت موردنظر پیدا نشد.");

        if (batch.Status != ImportBatchStatus.Validated)
            return Result.Failure("فقط ایمپورت اعتبارسنجی‌شده قابل ثبت نهایی است.");

        if (batch.ValidRows == 0)
            return Result.Failure("هیچ سطر معتبری برای ثبت وجود ندارد.");

        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == batch.TemplateVersionId, cancellationToken);

        if (version is null || version.Status != TemplateVersionStatus.Published)
            return Result.Failure("نسخهٔ این ایمپورت دیگر منتشرشده نیست؛ ثبت نهایی ممکن نیست.");

        var imported = 0;

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            while (true)
            {
                var chunk = await _db.ImportRows
                    .Where(r => r.ImportBatchId == batch.Id && r.Status == ImportRowStatus.Valid)
                    .OrderBy(r => r.Id)
                    .Take(ImportLimits.ChunkSize)
                    .ToListAsync(cancellationToken);

                if (chunk.Count == 0)
                    break;

                foreach (var row in chunk)
                {
                    _db.DataRecords.Add(DataRecord.CreateFromImport(
                        version.TemplateId,
                        version.Id,
                        row.MappedDataJson!,
                        batch.Id,
                        row.Id));

                    row.MarkImported();
                    imported++;
                }

                await _db.SaveChangesAsync(cancellationToken);
            }

            batch.MarkImported();
            await _db.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure("ثبت نهایی انجام نشد؛ هیچ داده‌ای ثبت نشد. دوباره تلاش کنید.");
        }

        return Result.Ok($"{imported} رکورد ثبت شد.");
    }
}