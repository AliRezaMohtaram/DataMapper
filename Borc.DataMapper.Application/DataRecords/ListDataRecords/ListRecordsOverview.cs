using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Records;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.ListDataRecords;

/// <summary>
/// Main "recorded data" page, master/detail (user request: template, then its versions): one row per template that has
/// records the user may see (record count, imported count, its versions with their counts, units, last change and who
/// made it). The records themselves (detail) are on the records page, for one version or all of them. Search matches
/// the template name or record values; counts then cover the matching records.
/// </summary>
/// <param name="TemplateId">Only this template (its row gives the version chips of the records page).</param>
public sealed record ListRecordsOverviewQuery(
    string? Search = null,
    long? TemplateVersionId = null,
    RecordSource? Source = null,
    int Page = 1,
    int PageSize = 20,
    long? TemplateId = null
) : IRequest<Result<PagedResult<RecordVersionSummaryDto>>>
{
    public const int MaxPageSize = 100;
}

/// <param name="TemplateVersionId">The version with the latest change (opened from the row's version chip).</param>
/// <param name="LastChangedAt">Latest creation or edit among the records.</param>
/// <param name="LastChangedBy">Who created or edited that record last.</param>
/// <param name="Versions">The template's versions that have such records, newest first.</param>
public sealed record RecordVersionSummaryDto(
    long TemplateId,
    string TemplateName,
    long TemplateVersionId,
    int VersionNo,
    int RecordCount,
    int ImportedCount,
    DateTime LastChangedAt,
    long? LastChangedBy,
    IReadOnlyList<string?> OrgUnitKeys,
    IReadOnlyList<RecordVersionCountDto>? Versions = null);

public sealed record RecordVersionCountDto(long TemplateVersionId, int VersionNo, int RecordCount);

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

        if (request.TemplateId.HasValue)
            records = records.Where(r => r.TemplateId == request.TemplateId.Value);

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
            .GroupBy(r => r.TemplateId)
            .Select(g => new
            {
                TemplateId = g.Key,
                Count = g.Count(),
                Imported = g.Count(r => r.SourceType == RecordSource.Import),
                LastAt = g.Max(r => r.UpdatedAt ?? r.CreatedAt)
            });

        var total = await groups.CountAsync(cancellationToken);

        var pageRows = await groups
            .OrderByDescending(x => x.LastAt)
            .ThenByDescending(x => x.TemplateId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var ids = pageRows.Select(x => x.TemplateId).ToList();

        var names = await _db.Templates.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        // each template's versions that have such records, with their counts
        var perVersion = (await (
                from r in records
                where ids.Contains(r.TemplateId)
                group r by new { r.TemplateId, r.TemplateVersionId } into g
                select new { g.Key.TemplateId, g.Key.TemplateVersionId, Count = g.Count() })
            .ToListAsync(cancellationToken));
        var versionNos = await _db.TemplateVersions.AsNoTracking()
            .Where(v => ids.Contains(v.TemplateId))
            .ToDictionaryAsync(v => v.Id, v => v.VersionNo, cancellationToken);
        var versionsOf = perVersion
            .Select(x => new { x.TemplateId, Dto = new RecordVersionCountDto(x.TemplateVersionId, versionNos.GetValueOrDefault(x.TemplateVersionId), x.Count) })
            .ToLookup(x => x.TemplateId, x => x.Dto);

        // the latest change in each template: which version and who made it
        var last = await records
            .Where(r => ids.Contains(r.TemplateId))
            .GroupBy(r => r.TemplateId)
            .Select(g => g
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Select(r => new { r.TemplateId, r.TemplateVersionId, By = r.UpdatedAt != null ? r.UpdatedBy : r.CreatedBy })
                .First())
            .ToDictionaryAsync(x => x.TemplateId, cancellationToken);

        var units = (await records
                .Where(r => ids.Contains(r.TemplateId))
                .Select(r => new { r.TemplateId, r.OrgUnitKey })
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToLookup(x => x.TemplateId, x => x.OrgUnitKey);

        var items = pageRows
            .Where(x => names.ContainsKey(x.TemplateId) && last.ContainsKey(x.TemplateId))
            .Select(x =>
            {
                var latest = last[x.TemplateId];
                return new RecordVersionSummaryDto(
                    x.TemplateId, names[x.TemplateId], latest.TemplateVersionId, versionNos.GetValueOrDefault(latest.TemplateVersionId),
                    x.Count, x.Imported, x.LastAt, latest.By,
                    units[x.TemplateId].OrderBy(k => k is null).ThenBy(k => k, StringComparer.Ordinal).ToList(),
                    versionsOf[x.TemplateId].OrderByDescending(v => v.VersionNo).ToList());
            })
            .ToList();

        return Result<PagedResult<RecordVersionSummaryDto>>.Ok(
            new PagedResult<RecordVersionSummaryDto>(items, page, pageSize, total));
    }
}
