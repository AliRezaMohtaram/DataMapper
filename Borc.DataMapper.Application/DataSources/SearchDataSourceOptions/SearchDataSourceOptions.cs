using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataSources.Common;
using MediatR;

namespace Borc.DataMapper.Application.DataSources.SearchDataSourceOptions;

/// <summary>
/// جستجوی گزینه‌های یک منبع داده (برای Combobox فرم ورود داده و «نمایش نمونه» صفحهٔ منبع).
/// ExactValue پر باشد، فقط همان مقدار برگردانده می‌شود (گرفتن عنوان مقدار ذخیره‌شده).
/// </summary>
public sealed record SearchDataSourceOptionsQuery(
    long DataSourceId,
    string? Query = null,
    int Take = 30,
    string? ExactValue = null
) : IRequest<Result<DataSourceOptionsResult>>;

public sealed class SearchDataSourceOptionsHandler
    : IRequestHandler<SearchDataSourceOptionsQuery, Result<DataSourceOptionsResult>>
{
    private readonly DataSourceOptionService _options;

    public SearchDataSourceOptionsHandler(DataSourceOptionService options)
    {
        _options = options;
    }

    public async Task<Result<DataSourceOptionsResult>> Handle(
        SearchDataSourceOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ExactValue is not null)
        {
            var found = await _options.FindAsync(request.DataSourceId, request.ExactValue, cancellationToken);

            var items = found.Option is null
                ? Array.Empty<DataSourceOption>()
                : new[] { found.Option };

            return Result<DataSourceOptionsResult>.Ok(
                new DataSourceOptionsResult(items, items.Length, found.Error));
        }

        var result = await _options.SearchAsync(
            request.DataSourceId, request.Query, request.Take, cancellationToken);

        return Result<DataSourceOptionsResult>.Ok(result);
    }
}
