using System.Security.Cryptography;
using Borc.DataMapper.Application.Abstractions.Files;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Imports.UploadImportBatch;

/// <summary>آپلود فایل Excel: ساخت ImportBatch و یک ImportRow به‌ازای هر سطر. خروجی: شناسه دسته.</summary>
public sealed record UploadImportBatchCommand(
    long TemplateVersionId,
    string FileName,
    byte[] Content,
    long? MappingProfileId
) : IRequest<Result<long>>;

public sealed class UploadImportBatchHandler
    : IRequestHandler<UploadImportBatchCommand, Result<long>>
{
    private readonly IAppDbContext _db;
    private readonly IExcelReader _excel;

    public UploadImportBatchHandler(IAppDbContext db, IExcelReader excel)
    {
        _db = db;
        _excel = excel;
    }

    public async Task<Result<long>> Handle(
        UploadImportBatchCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.TemplateVersionId, cancellationToken);

        if (version is null)
            return Result<long>.Failure("نسخه موردنظر پیدا نشد.");

        if (version.Status != TemplateVersionStatus.Published)
            return Result<long>.Failure("ایمپورت فقط برای نسخهٔ منتشرشده ممکن است.");

        if (request.Content.Length == 0)
            return Result<long>.Failure("فایل خالی است.");

        if (request.Content.Length > ImportLimits.MaxFileBytes)
            return Result<long>.Failure("حجم فایل بیش از حد مجاز (10 مگابایت) است.");

        if (request.MappingProfileId.HasValue)
        {
            var profileOk = await _db.MappingProfiles.AnyAsync(
                p => p.Id == request.MappingProfileId.Value && p.TemplateVersionId == version.Id,
                cancellationToken);

            if (!profileOk)
                return Result<long>.Failure("پروفایل نگاشت انتخاب‌شده به این نسخه تعلق ندارد.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(request.Content));

        var alreadyImported = await _db.ImportBatches.AnyAsync(
            b => b.TemplateVersionId == version.Id
                 && b.FileHash == hash
                 && b.Status == ImportBatchStatus.Imported,
            cancellationToken);

        if (alreadyImported)
            return Result<long>.Failure("این فایل قبلاً برای همین نسخه ایمپورت شده است.");

        ExcelSheetData sheet;

        try
        {
            using var stream = new MemoryStream(request.Content);
            sheet = _excel.Read(stream, ImportLimits.MaxRows, ImportLimits.MaxColumns);
        }
        catch (ExcelReadException ex)
        {
            return Result<long>.Failure(ex.Message);
        }

        if (sheet.Rows.Count == 0)
            return Result<long>.Failure("فایل هیچ سطر دادهٔ غیرخالی ندارد.");

        var fileName = Path.GetFileName(request.FileName.Trim());

        if (fileName.Length > 500)
            fileName = fileName[^500..];

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            var batch = ImportBatch.Create(
                version.Id,
                fileName,
                hash,
                request.Content.Length,
                request.MappingProfileId);

            batch.SetRowCount(sheet.Rows.Count);

            _db.ImportBatches.Add(batch);
            await _db.SaveChangesAsync(cancellationToken);

            for (var offset = 0; offset < sheet.Rows.Count; offset += ImportLimits.ChunkSize)
            {
                var count = Math.Min(ImportLimits.ChunkSize, sheet.Rows.Count - offset);

                for (var i = 0; i < count; i++)
                {
                    var row = sheet.Rows[offset + i];

                    _db.ImportRows.Add(ImportRow.Create(
                        batch.Id,
                        row.RowNumber,
                        ImportJson.BuildRaw(sheet.Headers, row.Values)));
                }

                await _db.SaveChangesAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);

            return Result<long>.Ok(batch.Id, $"فایل بارگذاری شد ({sheet.Rows.Count} سطر)؛ ستون‌ها را تطبیق دهید.");
        }
        catch (DbUpdateException)
        {
            return Result<long>.Failure("ذخیرهٔ فایل انجام نشد؛ دوباره تلاش کنید.");
        }
    }
}