using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataSources.Common;
using Borc.DataMapper.Application.DataSources.PreviewDataSourceFile;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.DataSources;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Borc.DataMapper.Application.DataSources.ImportDataSourceFile;

/// <summary>
/// مرحلهٔ ۲ بارگذاری: ستون مقدار (واقعی) و ستون نمایشی انتخاب می‌شود و گزینه‌های قبلی منبع با گزینه‌های فایل جایگزین می‌شوند.
/// DisplayColumnIndex خالی = عنوان همان مقدار.
/// </summary>
public sealed record ImportDataSourceFileCommand(
    long DataSourceId,
    string Token,
    int ValueColumnIndex,
    int? DisplayColumnIndex
) : IRequest<Result<int>>;

public sealed class ImportDataSourceFileValidator : AbstractValidator<ImportDataSourceFileCommand>
{
    public ImportDataSourceFileValidator()
    {
        RuleFor(x => x.DataSourceId).GreaterThan(0);
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.ValueColumnIndex).GreaterThanOrEqualTo(0);
    }
}

public sealed class ImportDataSourceFileHandler
    : IRequestHandler<ImportDataSourceFileCommand, Result<int>>
{
    private readonly IAppDbContext _db;
    private readonly IMemoryCache _cache;

    public ImportDataSourceFileHandler(IAppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<Result<int>> Handle(
        ImportDataSourceFileCommand request,
        CancellationToken cancellationToken)
    {
        if (!_cache.TryGetValue(DataSourceFileStaging.Key(request.Token), out StagedDataSourceFile? staged)
            || staged is null)
            return Result<int>.Failure("مهلت این بارگذاری تمام شده است؛ فایل را دوباره بارگذاری کنید.");

        if (staged.DataSourceId != request.DataSourceId)
            return Result<int>.Failure("این فایل برای منبع داده دیگری بارگذاری شده است.");

        var headers = staged.Sheet.Headers;

        if (request.ValueColumnIndex >= headers.Count)
            return Result<int>.Failure("ستون مقدار نامعتبر است.");

        if (request.DisplayColumnIndex.HasValue
            && (request.DisplayColumnIndex.Value < 0 || request.DisplayColumnIndex.Value >= headers.Count))
            return Result<int>.Failure("ستون نمایشی نامعتبر است.");

        var ds = await _db.DataSources
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.DataSourceId, cancellationToken);

        if (ds is null)
            return Result<int>.Failure("منبع داده موردنظر پیدا نشد.");

        if (ds.SourceType != DataSourceType.File)
            return Result<int>.Failure("فقط منبع داده‌ای از نوع «فایل» فایل می‌پذیرد.");

        // ساخت گزینه‌ها؛ مقدار تکراری یا خالی نادیده گرفته می‌شود
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<(string Value, string Label)>();
        var skippedEmpty = 0;
        var skippedDuplicate = 0;
        var tooLong = 0;

        foreach (var row in staged.Sheet.Rows)
        {
            var value = row.Values[request.ValueColumnIndex]?.Trim();

            if (string.IsNullOrEmpty(value))
            {
                skippedEmpty++;
                continue;
            }

            var label = request.DisplayColumnIndex.HasValue
                ? row.Values[request.DisplayColumnIndex.Value]?.Trim()
                : null;

            if (string.IsNullOrEmpty(label))
                label = value;

            if (value.Length > 500 || label.Length > 500)
            {
                tooLong++;
                continue;
            }

            if (!seen.Add(value))
            {
                skippedDuplicate++;
                continue;
            }

            items.Add((value, label));
        }

        if (items.Count == 0)
            return Result<int>.Failure("هیچ مقدار معتبری در ستون انتخاب‌شده پیدا نشد.");

        var now = DateTime.UtcNow;

        var config = DataSourceConfigJson.Build(new FileSourceConfig(
            staged.FileName,
            headers[request.ValueColumnIndex],
            request.DisplayColumnIndex.HasValue ? headers[request.DisplayColumnIndex.Value] : null,
            items.Count,
            now));

        try
        {
            await _db.InTransactionAsync(async () =>
            {
                await _db.DataSourceItems
                    .Where(i => i.DataSourceId == request.DataSourceId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(i => i.IsDeleted, true)
                        .SetProperty(i => i.DeletedAt, (DateTime?)now),
                        cancellationToken);

                for (var offset = 0; offset < items.Count; offset += ImportLimits.ChunkSize)
                {
                    var count = Math.Min(ImportLimits.ChunkSize, items.Count - offset);

                    for (var i = 0; i < count; i++)
                    {
                        var item = items[offset + i];
                        _db.DataSourceItems.Add(
                            DataSourceItem.Create(request.DataSourceId, item.Value, item.Label, offset + i));
                    }

                    await _db.SaveChangesAsync(cancellationToken);
                }

                var tracked = await _db.DataSources.FirstAsync(d => d.Id == request.DataSourceId, cancellationToken);
                tracked.Update(tracked.Name, tracked.SourceType, config);
                await _db.SaveChangesAsync(cancellationToken);

                return true;
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<int>.Failure("ذخیرهٔ گزینه‌ها انجام نشد؛ دوباره تلاش کنید.");
        }

        _cache.Remove(DataSourceFileStaging.Key(request.Token));

        var notes = new List<string>();

        if (skippedDuplicate > 0) notes.Add($"{skippedDuplicate} مقدار تکراری");
        if (skippedEmpty > 0) notes.Add($"{skippedEmpty} سطر با مقدار خالی");
        if (tooLong > 0) notes.Add($"{tooLong} سطر با متن بیش از 500 نویسه");

        var message = $"{items.Count} گزینه ثبت شد"
                      + (notes.Count > 0 ? " (نادیده گرفته شد: " + string.Join("، ", notes) + ")" : string.Empty) + ".";

        return Result<int>.Ok(items.Count, message);
    }
}
