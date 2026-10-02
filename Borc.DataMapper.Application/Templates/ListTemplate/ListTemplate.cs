using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Borc.DataMapper.Application.Templates.ListTemplates;

public sealed record ListTemplatesQuery(
    string? Search = null,
    TemplateStatus? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<TemplateListItemDto>>>
{
    public const int MaxPageSize = 100;
}

public sealed record TemplateListItemDto(
    long Id,
    string Code,
    string Name,
    string? Description,
    TemplateStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class ListTemplatesHandler
    : IRequestHandler<ListTemplatesQuery, Result<PagedResult<TemplateListItemDto>>>
{
    private readonly IAppDbContext _db;

    public ListTemplatesHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PagedResult<TemplateListItemDto>>> Handle(
        ListTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, ListTemplatesQuery.MaxPageSize);

        // حذف‌شده‌ها را فیلتر سراسری MapperContext خودش کنار می‌گذارد.
        var query = _db.Templates.AsNoTracking();

        if (search is not null)
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Code, pattern) ||
                EF.Functions.Like(x.Name, pattern));
        }

        if (request.Status.HasValue)
        {
            var status = request.Status.Value;
            query = query.Where(x => x.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TemplateListItemDto(
                x.Id,
                x.Code,
                x.Name,
                x.Description,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<TemplateListItemDto>>.Ok(
            new PagedResult<TemplateListItemDto>(items, page, pageSize, total));
    }
}