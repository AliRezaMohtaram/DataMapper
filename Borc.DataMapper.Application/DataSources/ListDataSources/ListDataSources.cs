using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.DataSources;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataSources.ListDataSources;

public sealed record ListDataSourcesQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<DataSourceListItemDto>>>
{
    public const int MaxPageSize = 100;
}

public sealed record DataSourceListItemDto(
    long Id,
    string Code,
    string Name,
    DataSourceType SourceType,
    bool IsActive,
    DateTime CreatedAt);

public sealed class ListDataSourcesHandler
    : IRequestHandler<ListDataSourcesQuery, Result<PagedResult<DataSourceListItemDto>>>
{
    private readonly IAppDbContext _db;

    public ListDataSourcesHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PagedResult<DataSourceListItemDto>>> Handle(
        ListDataSourcesQuery request,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, ListDataSourcesQuery.MaxPageSize);

        var query = _db.DataSources.AsNoTracking();

        if (search is not null)
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Code, pattern) ||
                EF.Functions.Like(x.Name, pattern));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new DataSourceListItemDto(
                x.Id, x.Code, x.Name, x.SourceType, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<DataSourceListItemDto>>.Ok(
            new PagedResult<DataSourceListItemDto>(items, page, pageSize, total));
    }
}