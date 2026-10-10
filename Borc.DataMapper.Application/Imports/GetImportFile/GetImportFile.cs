using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Imports.GetImportFile;

/// <summary>فایل اصلی یک ایمپورت برای دانلود؛ فقط اگر خود ایمپورت برای کاربر دیدنی باشد (فیلتر حذف و محدودهٔ داده).</summary>
public sealed record GetImportFileQuery(long ImportBatchId) : IRequest<Result<ImportFileDto>>;

public sealed record ImportFileDto(string FileName, string ContentType, byte[] Content);

public sealed class GetImportFileHandler : IRequestHandler<GetImportFileQuery, Result<ImportFileDto>>
{
    private readonly IAppDbContext _db;

    public GetImportFileHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ImportFileDto>> Handle(GetImportFileQuery request, CancellationToken cancellationToken)
    {
        var fileName = await _db.ImportBatches.AsNoTracking()
            .Where(b => b.Id == request.ImportBatchId)
            .Select(b => b.FileName)
            .FirstOrDefaultAsync(cancellationToken);

        if (fileName is null)
            return Result<ImportFileDto>.Failure("ایمپورت موردنظر پیدا نشد.");

        var file = await _db.ImportBatchFiles.AsNoTracking()
            .Where(f => f.ImportBatchId == request.ImportBatchId)
            .Select(f => new { f.ContentType, f.Content })
            .FirstOrDefaultAsync(cancellationToken);

        return file is null
            ? Result<ImportFileDto>.Failure("فایل اصلی این ایمپورت نگه‌داری نشده است (پیش از افزوده‌شدن این امکان آپلود شده).")
            : Result<ImportFileDto>.Ok(new ImportFileDto(fileName, file.ContentType, file.Content));
    }
}
