using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.MappingProfiles.ListMappingProfiles;

public sealed record ListMappingProfilesQuery(
    string? Search = null,
    long? TemplateVersionId = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<MappingProfileListItemDto>>>
{
    public const int MaxPageSize = 100;
}

public sealed record MappingProfileListItemDto(
    long Id,
    string Name,
    string TemplateName,
    int VersionNo,
    long TemplateVersionId,
    ImportSourceType SourceType,
    MappingProfileStatus Status,
    int RuleCount,
    DateTime CreatedAt);

public sealed class ListMappingProfilesHandler
    : IRequestHandler<ListMappingProfilesQuery, Result<PagedResult<MappingProfileListItemDto>>>
{
    private readonly IAppDbContext _db;

    public ListMappingProfilesHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PagedResult<MappingProfileListItemDto>>> Handle(
        ListMappingProfilesQuery request,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, ListMappingProfilesQuery.MaxPageSize);

        var query =
            from p in _db.MappingProfiles.AsNoTracking()
            join v in _db.TemplateVersions on p.TemplateVersionId equals v.Id
            join t in _db.Templates on v.TemplateId equals t.Id
            select new { p, v, t };

        if (request.TemplateVersionId.HasValue)
            query = query.Where(x => x.p.TemplateVersionId == request.TemplateVersionId.Value);

        if (search is not null)
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.Like(x.p.Name, pattern) ||
                EF.Functions.Like(x.t.Name, pattern));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.t.Name)
            .ThenByDescending(x => x.v.VersionNo)
            .ThenBy(x => x.p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MappingProfileListItemDto(
                x.p.Id,
                x.p.Name,
                x.t.Name,
                x.v.VersionNo,
                x.v.Id,
                x.p.SourceType,
                x.p.Status,
                _db.MappingRules.Count(r => r.MappingProfileId == x.p.Id),
                x.p.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<MappingProfileListItemDto>>.Ok(
            new PagedResult<MappingProfileListItemDto>(items, page, pageSize, total));
    }
}
