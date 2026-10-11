using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Records;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.ListDataRecords;

/// <summary>
/// Main "recorded data" page, master/detail: one row per template version that has records the user may see
/// (record count, imported count, units, last change and who made it). The records themselves (detail) are on the
/// version page. Search matches the template name or record values; counts then cover the matching records.
/// </summary>
public sealed record ListRecordsOverviewQuery(
    string? Search = null,
    long? TemplateVersionId = null,
    RecordSource? Source = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<RecordVersionSummaryDto>>>
{
    public const int MaxPageSize = 100;
}

/// <param name="LastChangedAt">Latest creation or edit among the records.</param>
/// <param name="LastChangedBy">Who created or edited that record last.</param>
public sealed record RecordVersionSummaryDto(
    long TemplateId,
    string TemplateName,
    long TemplateVersionId,
    int VersionNo,
    int RecordCount,
    int ImportedCount,
    DateTime LastChangedAt,
    long? LastChangedBy,
    IReadOnlyList<string?> OrgUnitKeys);

public sealed class ListRecordsOverviewHandler
    : IRequestHandler<ListRecordsOverviewQuery, Result<PagedResult<RecordVersionSummaryDto>>>
{
    private readonly IAppDbContext _db;

    public ListRecordsOverviewHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PagedResult<RecordVersionSummaryDto>>> Handle(
        ListRecordsOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, ListRecordsOverviewQuery.MaxPageSize);

        var records = _db.DataRecords.AsNoTracking();

        if (request.TemplateVersionId.HasValue)
            records = records.Where(r => r.TemplateVersionId == request.TemplateVersionId.Value);

        if (request.Source.HasValue)
            records = records.Where(r => r.SourceType == request.Source.Value);

        if (search is not null)
        {
            var pattern = $"%{search}%";
            records = records.Where(r =>
                EF.Functions.Like(r.DataJson, pattern)
                || _db.Templates.Any(t => t.Id == r.TemplateId && EF.Functions.Like(t.Name, pattern)));
        }

        var groups = records
            .GroupBy(r => r.TemplateVersionId)
            .Select(g => new
            {
                VersionId = g.Key,
                Count = g.Count(),
                Imported = g.Count(r => r.SourceType == RecordSource.Import),
                LastAt = g.Max(r => r.UpdatedAt ?? r.CreatedAt)
            });

        var total = await groups.CountAsync(cancellationToken);

        var pageRows = await groups
            .OrderByDescending(x => x.LastAt)
            .ThenByDescending(x => x.VersionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var ids = pageRows.Select(x => x.VersionId).ToList();

        var versions = await (
                from v in _db.TemplateVersions.AsNoTracking()
                join t in _db.Templates on v.TemplateId equals t.Id
                where ids.Contains(v.Id)
                select new { v.Id, v.VersionNo, TemplateId = t.Id, t.Name })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        // who made the latest change in each version
        var lastBy = await records
            .Where(r => ids.Contains(r.TemplateVersionId))
            .GroupBy(r => r.TemplateVersionId)
            .Select(g => g
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Select(r => new { r.TemplateVersionId, By = r.UpdatedAt != null ? r.UpdatedBy : r.CreatedBy })
                .First())
            .ToDictionaryAsync(x => x.TemplateVersionId, x => x.By, cancellationToken);

        var units = (await records
                .Where(r => ids.Contains(r.TemplateVersionId))
                .Select(r => new { r.TemplateVersionId, r.OrgUnitKey })
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.TemplateVersionId, x => x.OrgUnitKey);

        var items = pageRows
            .Where(x => versions.ContainsKey(x.VersionId))
            .Select(x =>
            {
                var v = versions[x.VersionId];
                return new RecordVersionSummaryDto(
                    v.TemplateId, v.Name, v.Id, v.VersionNo, x.Count, x.Imported, x.LastAt,
                    lastBy.GetValueOrDefault(x.VersionId),
                    units[x.VersionId].OrderBy(k => k is null).ThenBy(k => k, StringComparer.Ordinal).ToList());
            })
            .ToList();

        return Result<PagedResult<RecordVersionSummaryDto>>.Ok(
            new PagedResult<RecordVersionSummaryDto>(items, page, pageSize, total));
    }
}
