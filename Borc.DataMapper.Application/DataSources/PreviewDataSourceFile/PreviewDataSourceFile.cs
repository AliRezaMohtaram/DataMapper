using Borc.DataMapper.Application.Abstractions.Files;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataSources.Common;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.DataSources;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Borc.DataMapper.Application.DataSources.PreviewDataSourceFile;

/// <summary>
/// مرحلهٔ ۱ بارگذاری: فایل Excel/CSV خوانده و موقتاً نگه داشته می‌شود؛ کاربر در مرحلهٔ ۲ ستون مقدار و ستون نمایشی را انتخاب می‌کند.
/// </summary>
public sealed record PreviewDataSourceFileCommand(
    long DataSourceId,
    string FileName,
    byte[] Content
) : IRequest<Result<DataSourceFilePreviewDto>>;

public sealed record DataSourceFilePreviewDto(
    string Token,
    string DataSourceName,
    string FileName,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string?>> SampleRows,
    int RowCount,
    int SuggestedValueIndex,
    int? SuggestedDisplayIndex);

/// <summary>جدول خوانده‌شده که بین دو مرحله در حافظه می‌ماند.</summary>
public sealed record StagedDataSourceFile(long DataSourceId, string FileName, ExcelSheetData Sheet);

public static class DataSourceFileStaging
{
    public static string Key(string token) => $"dsfile:{token}";

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
}

public sealed class PreviewDataSourceFileHandler
    : IRequestHandler<PreviewDataSourceFileCommand, Result<DataSourceFilePreviewDto>>
{
    private static readonly string[] ValueHints = { "code", "id", "key", "value", "کد", "شناسه", "مقدار" };
    private static readonly string[] DisplayHints = { "name", "title", "label", "عنوان", "نام", "شرح", "توضیح" };

    private readonly IAppDbContext _db;
    private readonly IExcelReader _excel;
    private readonly IMemoryCache _cache;

    public PreviewDataSourceFileHandler(IAppDbContext db, IExcelReader excel, IMemoryCache cache)
    {
        _db = db;
        _excel = excel;
        _cache = cache;
    }

    public async Task<Result<DataSourceFilePreviewDto>> Handle(
        PreviewDataSourceFileCommand request,
        CancellationToken cancellationToken)
    {
        var ds = await _db.DataSources
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DataSourceId, cancellationToken);

        if (ds is null)
            return Result<DataSourceFilePreviewDto>.Failure("منبع داده موردنظر پیدا نشد.");

        if (ds.SourceType != DataSourceType.File)
            return Result<DataSourceFilePreviewDto>.Failure("فقط منبع داده‌ای از نوع «فایل» فایل می‌پذیرد.");

        if (request.Content.Length == 0)
            return Result<DataSourceFilePreviewDto>.Failure("فایل خالی است.");

        if (request.Content.Length > ImportLimits.MaxFileBytes)
            return Result<DataSourceFilePreviewDto>.Failure("حجم فایل بیش از 10 مگابایت است.");

        var fileName = Path.GetFileName(request.FileName.Trim());

        if (!TabularFileReader.IsSupported(fileName))
            return Result<DataSourceFilePreviewDto>.Failure("فقط فایل‌های xlsx، csv و tsv پذیرفته می‌شوند.");

        ExcelSheetData sheet;

        try
        {
            sheet = TabularFileReader.Read(
                fileName, request.Content, _excel, ImportLimits.MaxRows, ImportLimits.MaxColumns);
        }
        catch (ExcelReadException ex)
        {
            return Result<DataSourceFilePreviewDto>.Failure(ex.Message);
        }

        if (sheet.Rows.Count == 0)
            return Result<DataSourceFilePreviewDto>.Failure("فایل هیچ سطر دادهٔ غیرخالی ندارد.");

        var token = Guid.NewGuid().ToString("N");

        _cache.Set(
            DataSourceFileStaging.Key(token),
            new StagedDataSourceFile(ds.Id, fileName, sheet),
            DataSourceFileStaging.Lifetime);

        var valueIdx = Guess(sheet.Headers, ValueHints) ?? 0;
        var displayIdx = Guess(sheet.Headers, DisplayHints, exclude: valueIdx)
                         ?? (sheet.Headers.Count > 1 ? (valueIdx == 0 ? 1 : 0) : (int?)null);

        var sample = sheet.Rows.Take(8).Select(r => r.Values).ToList();

        return Result<DataSourceFilePreviewDto>.Ok(new DataSourceFilePreviewDto(
            token, ds.Name, fileName, sheet.Headers, sample, sheet.Rows.Count, valueIdx, displayIdx));
    }

    private static int? Guess(IReadOnlyList<string> headers, string[] hints, int? exclude = null)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            if (i == exclude)
                continue;

            var h = TextNormalizerShim(headers[i]);

            if (hints.Any(hint => h == hint || h.EndsWith(hint, StringComparison.Ordinal)))
                return i;
        }

        return null;
    }

    private static string TextNormalizerShim(string header)
        => Borc.DataMapper.Domain.Common.TextNormalizer.NormalizeHeader(header);
}
