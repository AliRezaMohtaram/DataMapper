using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Imports.ListImportBatches;
using Borc.DataMapper.Application.Templates.Entry;
using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Dashboard.GetDashboard;

/// <summary>
/// داده‌های صفحه داشبورد: شاخص‌ها، ایمپورت‌های اخیر، قالب‌های پرکاربرد و منابع داده — به اندازهٔ دسترسی کاربر
/// (رکوردها و ایمپورت‌ها را فیلتر محدودهٔ داده محدود می‌کند؛ قالب‌ها و منابع داده را <see cref="Access"/>).
/// </summary>
public sealed record GetDashboardQuery(
    DashboardAccess Access,
    int RecentImports = 6,
    int TopTemplates = 5,
    int RecentDataSources = 5
) : IRequest<Result<DashboardDto>>;

/// <summary>
/// What the user may open (Acl permissions, decided by the web host).
/// AllTemplates: View on templates — every template; otherwise only the templates the user may enter data for or has
/// visible records of. DataSources: View on data sources — otherwise none are listed.
/// </summary>
public sealed record DashboardAccess(bool AllTemplates, bool DataSources)
{
    public static DashboardAccess Everything { get; } = new(true, true);
}

public sealed record DashboardDto(
    DashboardKpis Kpis,
    IReadOnlyList<ImportBatchListItemDto> RecentImports,
    IReadOnlyList<DashboardTemplateDto> Templates,
    IReadOnlyList<DashboardDataSourceDto> DataSources);

public sealed record DashboardKpis(
    int TemplatesTotal,
    int TemplatesActive,
    int TemplatesDraft,
    int RecordsTotal,
    int RecordsThisWeek,
    int ImportsInProgress,
    int ImportsReadyToCommit,
    int ImportsFailed,
    /// <summary>درصد ردیف‌های معتبر در ایمپورت‌های اعتبارسنجی‌شده؛ null اگر هنوز داده‌ای نیست.</summary>
    double? ValidRowRate);

/// <summary>
/// قالب به همراه نسخه جاری (آخرین نسخه منتشرشده، وگرنه آخرین نسخه).
/// AliasedFieldCount: فیلدهایی که حداقل یک نام مستعار دارند و در ایمپورت خودکار نگاشت می‌شوند.
/// </summary>
public sealed record DashboardTemplateDto(
    long Id,
    string Code,
    string Name,
    TemplateStatus Status,
    long? VersionId,
    int? VersionNo,
    int FieldCount,
    int AliasedFieldCount,
    int RecordCount,
    DateTime ChangedAt);

public sealed record DashboardDataSourceDto(
    long Id,
    string Code,
    string Name,
    DataSourceType SourceType,
    bool IsActive,
    int ItemCount,
    DateTime ChangedAt);

public sealed class GetDashboardHandler : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
{
    private readonly IAppDbContext _db;
    private readonly ITemplateEntryPolicy _entry;

    public GetDashboardHandler(IAppDbContext db, ITemplateEntryPolicy entry)
    {
        _db = db;
        _entry = entry;
    }

    public async Task<Result<DashboardDto>> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var templateIds = request.Access.AllTemplates ? null : await VisibleTemplateIdsAsync(cancellationToken);
        var kpis = await GetKpisAsync(templateIds, cancellationToken);
        var imports = await GetRecentImportsAsync(request.RecentImports, cancellationToken);
        var templates = await GetTopTemplatesAsync(request.TopTemplates, templateIds, cancellationToken);

        var dataSources = !request.Access.DataSources ? [] : await _db.DataSources.AsNoTracking()
            .OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt)
            .Take(request.RecentDataSources)
            .Select(d => new DashboardDataSourceDto(
                d.Id,
                d.Code,
                d.Name,
                d.SourceType,
                d.IsActive,
                _db.DataSourceItems.Count(i => i.DataSourceId == d.Id),
                d.UpdatedAt ?? d.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result<DashboardDto>.Ok(new DashboardDto(kpis, imports, templates, dataSources));
    }

    /// <summary>Without View on templates: the ones the user may enter data for, and the ones with records they see.</summary>
    private async Task<IReadOnlySet<long>> VisibleTemplateIdsAsync(CancellationToken ct)
    {
        var allowed = await _entry.AllowedTemplateIdsAsync(ct);
        if (allowed is null)
            return (await _db.Templates.AsNoTracking().Select(t => t.Id).ToListAsync(ct)).ToHashSet();
        var withRecords = await _db.DataRecords.AsNoTracking().Select(r => r.TemplateId).Distinct().ToListAsync(ct);
        return allowed.Concat(withRecords).ToHashSet();
    }

    private IQueryable<Template> Templates(IReadOnlySet<long>? ids) =>
        ids is null ? _db.Templates.AsNoTracking() : _db.Templates.AsNoTracking().Where(t => ids.Contains(t.Id));

    private async Task<DashboardKpis> GetKpisAsync(IReadOnlySet<long>? templateIds, CancellationToken ct)
    {
        var templateStatuses = await Templates(templateIds)
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var importStatuses = await _db.ImportBatches.AsNoTracking()
            .GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var recordsTotal = await _db.DataRecords.AsNoTracking().CountAsync(ct);
        var recordsThisWeek = await _db.DataRecords.AsNoTracking().CountAsync(r => r.CreatedAt >= weekAgo, ct);

        var rows = await _db.ImportBatches.AsNoTracking()
            .Where(b => b.Status == ImportBatchStatus.Validated || b.Status == ImportBatchStatus.Imported)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Valid = g.Sum(b => (long)b.ValidRows),
                Invalid = g.Sum(b => (long)b.InvalidRows)
            })
            .FirstOrDefaultAsync(ct);

        int TemplatesIn(TemplateStatus s) => templateStatuses.Where(x => x.Status == s).Sum(x => x.Count);
        int ImportsIn(params ImportBatchStatus[] s) => importStatuses.Where(x => s.Contains(x.Status)).Sum(x => x.Count);

        double? validRate = rows is { } r && r.Valid + r.Invalid > 0
            ? Math.Round(100d * r.Valid / (r.Valid + r.Invalid), 1)
            : null;

        return new DashboardKpis(
            TemplatesTotal: templateStatuses.Sum(x => x.Count),
            TemplatesActive: TemplatesIn(TemplateStatus.Active),
            TemplatesDraft: TemplatesIn(TemplateStatus.Draft),
            RecordsTotal: recordsTotal,
            RecordsThisWeek: recordsThisWeek,
            ImportsInProgress: ImportsIn(ImportBatchStatus.Uploaded, ImportBatchStatus.Validating, ImportBatchStatus.Validated, ImportBatchStatus.Committing),
            ImportsReadyToCommit: ImportsIn(ImportBatchStatus.Validated),
            ImportsFailed: ImportsIn(ImportBatchStatus.Failed),
            ValidRowRate: validRate);
    }

    private Task<List<ImportBatchListItemDto>> GetRecentImportsAsync(int take, CancellationToken ct) =>
        (from b in _db.ImportBatches.AsNoTracking()
         join v in _db.TemplateVersions on b.TemplateVersionId equals v.Id
         join t in _db.Templates on v.TemplateId equals t.Id
         orderby b.CreatedAt descending, b.Id descending
         select new ImportBatchListItemDto(
             b.Id,
             b.FileName,
             t.Name,
             v.VersionNo,
             b.TotalRows,
             b.ValidRows,
             b.InvalidRows,
             b.Status,
             b.CreatedAt))
        .Take(take)
        .ToListAsync(ct);

    private async Task<List<DashboardTemplateDto>> GetTopTemplatesAsync(int take, IReadOnlySet<long>? visibleIds, CancellationToken ct)
    {
        var templates = await Templates(visibleIds)
            .Select(t => new
            {
                t.Id,
                t.Code,
                t.Name,
                t.Status,
                ChangedAt = t.UpdatedAt ?? t.CreatedAt,
                Records = _db.DataRecords.Count(r => r.TemplateId == t.Id)
            })
            .OrderByDescending(t => t.Records)
            .ThenByDescending(t => t.ChangedAt)
            .Take(take)
            .ToListAsync(ct);

        var templateIds = templates.Select(t => t.Id).ToList();

        var currentVersions = (await _db.TemplateVersions.AsNoTracking()
                .Where(v => templateIds.Contains(v.TemplateId))
                .Select(v => new { v.Id, v.TemplateId, v.VersionNo, v.IsPublished })
                .ToListAsync(ct))
            .GroupBy(v => v.TemplateId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(v => v.IsPublished).ThenByDescending(v => v.VersionNo).First());

        var versionIds = currentVersions.Values.Select(v => v.Id).ToList();

        var fields = await _db.TemplateFields.AsNoTracking()
            .Where(f => versionIds.Contains(f.TemplateVersionId))
            .Select(f => new { f.Id, f.TemplateVersionId })
            .ToListAsync(ct);

        var fieldIds = fields.Select(f => f.Id).ToList();

        var aliasedFieldIds = (await _db.TemplateFieldAliases.AsNoTracking()
                .Where(a => fieldIds.Contains(a.TemplateFieldId))
                .Select(a => a.TemplateFieldId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        return templates.Select(t =>
        {
            currentVersions.TryGetValue(t.Id, out var version);
            var versionFields = version is null
                ? new List<long>()
                : fields.Where(f => f.TemplateVersionId == version.Id).Select(f => f.Id).ToList();

            return new DashboardTemplateDto(
                t.Id,
                t.Code,
                t.Name,
                t.Status,
                version?.Id,
                version?.VersionNo,
                versionFields.Count,
                versionFields.Count(aliasedFieldIds.Contains),
                t.Records,
                t.ChangedAt);
        }).ToList();
    }
}
