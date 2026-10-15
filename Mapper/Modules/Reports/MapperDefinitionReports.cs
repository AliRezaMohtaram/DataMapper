using System.Globalization;
using Acl.Core.Abstractions;
using Borc.DataMapper.Infrastructure.Persistence;
using Borc.DataMapper.Web.ViewModels;
using Borc.DataMapper.Web.ViewModels.DataSources;
using Borc.DataMapper.Web.ViewModels.MappingProfiles;
using Borc.DataMapper.Web.ViewModels.Templates;
using Borc.Users.Services;
using Microsoft.EntityFrameworkCore;
using MReporter.Core.Abstractions;
using MReporter.Core.Execution;
using MReporter.Core.Model;
using MReporter.Core.Queries;
using OrgChart.Core.Chart;
using OrgChart.Core.Model;

namespace Borc.DataMapper.Web.Modules.Reports;

/// <summary>
/// Report sources over Mapper's definitions and people (user request: reports on users, templates, data sources…):
/// users, templates, template fields, data sources, mapping profiles, and the org chart's units, positions and
/// assignments. They are small, so rows are loaded whole and the query runs in memory (<see cref="ReportMemory"/>).
/// Each source belongs to an Acl page: it is listed for new reports only to users with View on it, and a run with the
/// runner's own visibility (personal reports, ad-hoc runs, shared reports without a granted scope) returns no rows
/// without that permission; a shared report that grants a scope shows its rows to its audience.
/// </summary>
public static class DefinitionSources
{
    public const string Users = "mapper.users";
    public const string Templates = "mapper.templates";
    public const string Fields = "mapper.fields";
    public const string DataSources = "mapper.datasources";
    public const string Profiles = "mapper.profiles";
    public const string Units = "org.units";
    public const string Positions = "org.positions";
    public const string Assignments = "org.assignments";

    private const string GroupDefinitions = "تعاریف";
    private const string GroupPeople = "کاربران و سازمان";

    private static ReportColumn C(string key, string title, ReportColumnType type) => new(key, title, type);

    /// <summary>Every definition source with the Acl page that guards it.</summary>
    public static IReadOnlyList<(ReportSourceInfo Source, string Resource)> All { get; } =
    [
        (new ReportSourceInfo(Users, "کاربران",
        [
            C("u.name", "نام", ReportColumnType.Text), C("u.userName", "نام کاربری", ReportColumnType.Text),
            C("u.email", "ایمیل", ReportColumnType.Text), C("u.active", "فعال", ReportColumnType.Bool),
            C("u.locked", "قفل‌شده", ReportColumnType.Bool), C("u.positions", "سمت‌های کنونی", ReportColumnType.Text),
            C("u.units", "واحدهای کنونی", ReportColumnType.Text), C("u.positionCount", "تعداد سمت", ReportColumnType.Number),
            C("u.createdAt", "تاریخ ایجاد", ReportColumnType.DateTime), C("u.lastSignIn", "آخرین ورود", ReportColumnType.DateTime),
        ], GroupPeople), MapperResources.Users),
        (new ReportSourceInfo(Templates, "قالب‌ها",
        [
            C("t.code", "کد", ReportColumnType.Text), C("t.name", "نام قالب", ReportColumnType.Text),
            C("t.description", "توضیحات", ReportColumnType.Text), C("t.status", "وضعیت", ReportColumnType.List),
            C("t.versions", "تعداد نسخه", ReportColumnType.Number), C("t.current", "نسخهٔ جاری", ReportColumnType.Number),
            C("t.fields", "فیلدهای نسخهٔ جاری", ReportColumnType.Number), C("t.records", "رکوردها", ReportColumnType.Number),
            C("t.imports", "ایمپورت‌ها", ReportColumnType.Number), C("t.units", "واحدهای مجاز", ReportColumnType.Text),
            C("t.author", "سازنده", ReportColumnType.List), C("t.createdAt", "تاریخ ایجاد", ReportColumnType.DateTime),
            C("t.updatedAt", "آخرین تغییر", ReportColumnType.DateTime),
        ], GroupDefinitions), MapperResources.Templates),
        (new ReportSourceInfo(Fields, "فیلدهای قالب‌ها",
        [
            C("f.template", "قالب", ReportColumnType.List), C("f.version", "نسخه", ReportColumnType.Number),
            C("f.key", "کلید", ReportColumnType.Text), C("f.label", "برچسب", ReportColumnType.Text),
            C("f.type", "نوع داده", ReportColumnType.List), C("f.required", "الزامی", ReportColumnType.Bool),
            C("f.source", "منبع داده", ReportColumnType.List), C("f.aliases", "نام‌های مستعار", ReportColumnType.Number),
            C("f.order", "ترتیب", ReportColumnType.Number),
        ], GroupDefinitions), MapperResources.Templates),
        (new ReportSourceInfo(DataSources, "منابع داده",
        [
            C("d.code", "کد", ReportColumnType.Text), C("d.name", "نام", ReportColumnType.Text),
            C("d.type", "نوع", ReportColumnType.List), C("d.active", "فعال", ReportColumnType.Bool),
            C("d.items", "تعداد گزینه", ReportColumnType.Number), C("d.fields", "فیلدهای استفاده‌کننده", ReportColumnType.Number),
            C("d.author", "سازنده", ReportColumnType.List), C("d.createdAt", "تاریخ ایجاد", ReportColumnType.DateTime),
            C("d.updatedAt", "آخرین تغییر", ReportColumnType.DateTime),
        ], GroupDefinitions), MapperResources.DataSources),
        (new ReportSourceInfo(Profiles, "پروفایل‌های نگاشت",
        [
            C("p.name", "نام", ReportColumnType.Text), C("p.template", "قالب", ReportColumnType.List),
            C("p.version", "نسخه", ReportColumnType.Number), C("p.status", "وضعیت", ReportColumnType.List),
            C("p.rules", "قاعده‌ها", ReportColumnType.Number), C("p.createdAt", "تاریخ ایجاد", ReportColumnType.DateTime),
        ], GroupDefinitions), MapperResources.MappingProfiles),
        (new ReportSourceInfo(Units, "واحدهای سازمانی",
        [
            C("o.title", "واحد", ReportColumnType.Text), C("o.code", "کد", ReportColumnType.Text),
            C("o.type", "نوع واحد", ReportColumnType.List), C("o.parent", "واحد بالادست", ReportColumnType.List),
            C("o.level", "سطح", ReportColumnType.Number), C("o.head", "سمت مدیر", ReportColumnType.Text),
            C("o.headHolder", "مدیر", ReportColumnType.Text), C("o.positions", "تعداد سمت", ReportColumnType.Number),
            C("o.vacant", "سمت‌های بلاتصدی", ReportColumnType.Number), C("o.people", "تعداد افراد", ReportColumnType.Number),
            C("o.active", "فعال", ReportColumnType.Bool),
        ], GroupPeople), MapperResources.OrgChart),
        (new ReportSourceInfo(Positions, "سمت‌ها",
        [
            C("s.title", "سمت", ReportColumnType.Text), C("s.code", "کد", ReportColumnType.Text),
            C("s.unit", "واحد", ReportColumnType.List), C("s.type", "نوع سمت", ReportColumnType.List),
            C("s.head", "مدیر واحد", ReportColumnType.Bool), C("s.holders", "متصدیان", ReportColumnType.Text),
            C("s.holderCount", "تعداد متصدی", ReportColumnType.Number), C("s.vacant", "بلاتصدی", ReportColumnType.Bool),
            C("s.active", "فعال", ReportColumnType.Bool),
        ], GroupPeople), MapperResources.OrgChart),
        (new ReportSourceInfo(Assignments, "انتصاب‌ها",
        [
            C("a.person", "شخص", ReportColumnType.List), C("a.position", "سمت", ReportColumnType.List),
            C("a.unit", "واحد", ReportColumnType.List), C("a.kind", "نوع انتصاب", ReportColumnType.List),
            C("a.from", "از", ReportColumnType.DateTime), C("a.to", "تا", ReportColumnType.DateTime),
            C("a.current", "جاری", ReportColumnType.Bool),
        ], GroupPeople), MapperResources.OrgChart),
    ];

    public static (ReportSourceInfo Source, string Resource)? Find(string key) =>
        All.FirstOrDefault(s => string.Equals(s.Source.Key, key, StringComparison.OrdinalIgnoreCase)) is { Source: not null } found ? found : null;
}

/// <summary>Rows of the definition sources (<see cref="DefinitionSources"/>), loaded whole; also their list options.</summary>
public sealed class MapperDefinitionData(
    BorcDataMapperDbContext db,
    IOrgChartReader chart,
    IUserAdministration users,
    UserNames userNames,
    IAccessService access,
    TimeProvider time) : IReportDataProvider, IReportOptionProvider
{
    private readonly Dictionary<string, IReadOnlyList<ReportRow>> _rows = new(StringComparer.OrdinalIgnoreCase);

    public bool Serves(string sourceKey) => DefinitionSources.Find(sourceKey) is not null;

    public async Task<ReportPage> QueryAsync(ReportExecution execution, int page, int pageSize, CancellationToken cancellationToken = default) =>
        ReportMemory.Page(execution, await VisibleRowsAsync(execution, cancellationToken), page, pageSize);

    public async IAsyncEnumerable<ReportRow> StreamAsync(ReportExecution execution, int maxRows,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var row in ReportMemory.StreamAsync(execution, await VisibleRowsAsync(execution, cancellationToken), maxRows, cancellationToken))
            yield return row;
    }

    public async Task<IReadOnlyList<ReportListValue>> SearchAsync(string sourceKey, string columnKey, string? search, int take, CancellationToken cancellationToken = default) =>
        DefinitionSources.Find(sourceKey) is { } found && await AllowedAsync(found.Resource, cancellationToken)
            ? ReportMemory.Options(await RowsAsync(sourceKey, cancellationToken), found.Source, columnKey, search, take)
            : [];

    public async Task<IReadOnlyList<ReportListValue>> GetAsync(string sourceKey, string columnKey, IReadOnlyCollection<string> values, CancellationToken cancellationToken = default) =>
        (await SearchAsync(sourceKey, columnKey, null, int.MaxValue, cancellationToken))
            .Where(o => values.Contains(o.Value, StringComparer.OrdinalIgnoreCase)).ToList();

    /// <summary>The runner's own visibility needs View on the source's page; a granted scope (shared report) does not.</summary>
    private async Task<IReadOnlyList<ReportRow>> VisibleRowsAsync(ReportExecution execution, CancellationToken ct)
    {
        var found = DefinitionSources.Find(execution.Source.Key)!.Value;
        if (execution.Scope.Kind == ReportScopeKind.Viewer && !await AllowedAsync(found.Resource, ct))
            return [];
        return await RowsAsync(execution.Source.Key, ct);
    }

    private async Task<bool> AllowedAsync(string resource, CancellationToken ct) =>
        (await access.GetCurrentUserAccessAsync(ct)).IsAllowed(resource, "View");

    private async Task<IReadOnlyList<ReportRow>> RowsAsync(string sourceKey, CancellationToken ct)
    {
        if (_rows.TryGetValue(sourceKey, out var cached))
            return cached;
        var rows = sourceKey.ToLowerInvariant() switch
        {
            DefinitionSources.Users => await UsersAsync(ct),
            DefinitionSources.Templates => await TemplatesAsync(ct),
            DefinitionSources.Fields => await FieldsAsync(ct),
            DefinitionSources.DataSources => await DataSourcesAsync(ct),
            DefinitionSources.Profiles => await ProfilesAsync(ct),
            DefinitionSources.Units => await UnitsAsync(ct),
            DefinitionSources.Positions => await PositionsAsync(ct),
            DefinitionSources.Assignments => await AssignmentsAsync(ct),
            _ => [],
        };
        return _rows[sourceKey] = rows;
    }

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? Utc(DateTime? value) => value is { } v ? Utc(v) : null;

    private static ReportRow Row(long key, params object?[] values) => new(values, Id(key));

    // ------------------------------------------------------------------ people and the org chart

    /// <summary>Every assignment (past, current, future) with the chart, read once per request.</summary>
    private async Task<(OrgChartSnapshot Chart, IReadOnlyList<AssignmentInfo> Assignments)> OrgAsync(CancellationToken ct)
    {
        var snapshot = await chart.GetSnapshotAsync(ct);
        var all = new List<AssignmentInfo>();
        foreach (var root in snapshot.Roots)
            all.AddRange(await chart.GetUnitAssignmentsAsync(root.Key, includeSubUnits: true, ct));
        return (snapshot, all.DistinctBy(a => a.Id).ToList());
    }

    private bool Current(AssignmentInfo a, DateTime now) => (a.ValidFrom is null || a.ValidFrom <= now) && (a.ValidTo is null || a.ValidTo > now);

    private async Task<IReadOnlyList<ReportRow>> UsersAsync(CancellationToken ct)
    {
        var list = new List<UserSummary>();
        for (int page = 1; ; page++)
        {
            var found = await users.ListAsync(new UserQuery(PageSize: 200, Page: page), ct);
            list.AddRange(found.Items);
            if (page >= found.TotalPages || found.Items.Count == 0)
                break;
        }

        var (snapshot, assignments) = await OrgAsync(ct);
        var now = time.GetUtcNow().UtcDateTime;
        var current = assignments.Where(a => Current(a, now)).ToLookup(a => a.UserId, StringComparer.OrdinalIgnoreCase);
        return list.Select(u =>
        {
            var held = current[Id(u.Id)].ToList();
            return Row(u.Id, u.DisplayName, u.UserName, u.Email, u.IsActive, u.IsLockedOut,
                string.Join("، ", held.Select(a => snapshot.FindPosition(a.PositionKey)?.Title ?? a.PositionKey).Distinct()),
                string.Join("، ", held.Select(a => snapshot.FindUnit(a.OrgUnitKey)?.Title ?? a.OrgUnitKey).Distinct()),
                (decimal)held.Count, Utc(u.CreatedAt), Utc(u.LastSignInAt));
        }).ToList();
    }

    private async Task<IReadOnlyList<ReportRow>> UnitsAsync(CancellationToken ct)
    {
        var (snapshot, assignments) = await OrgAsync(ct);
        var now = time.GetUtcNow().UtcDateTime;
        var holders = assignments.Where(a => Current(a, now)).ToLookup(a => a.PositionKey, StringComparer.OrdinalIgnoreCase);
        var names = await NamesAsync(assignments.Select(a => a.UserId), ct);
        return snapshot.Units.Select((u, i) =>
        {
            var positions = snapshot.GetPositions(u.Key).Where(p => p.IsActive).ToList();
            var head = snapshot.FindPosition(u.ManagerPositionKey);
            var type = snapshot.FindUnitType(u.TypeKey);
            var parent = snapshot.FindUnit(u.ParentKey);
            return Row(i + 1, u.Title, u.Code,
                type is null ? null : new ReportListValue(type.Key, type.Title),
                parent is null ? null : new ReportListValue(parent.Key, parent.Title),
                (decimal)(snapshot.GetLevel(u.Key) + 1), head?.Title,
                head is null ? null : string.Join("، ", holders[head.Key].Select(h => names.GetValueOrDefault(h.UserId, h.UserId)).Distinct()),
                (decimal)positions.Count, (decimal)positions.Count(p => !holders[p.Key].Any()),
                (decimal)positions.SelectMany(p => holders[p.Key]).Select(h => h.UserId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                u.IsActive);
        }).ToList();
    }

    private async Task<IReadOnlyList<ReportRow>> PositionsAsync(CancellationToken ct)
    {
        var (snapshot, assignments) = await OrgAsync(ct);
        var now = time.GetUtcNow().UtcDateTime;
        var holders = assignments.Where(a => Current(a, now)).ToLookup(a => a.PositionKey, StringComparer.OrdinalIgnoreCase);
        var names = await NamesAsync(assignments.Select(a => a.UserId), ct);
        return snapshot.Positions.Select((p, i) =>
        {
            var unit = snapshot.FindUnit(p.UnitKey);
            var type = snapshot.FindPositionType(p.TypeKey);
            var held = holders[p.Key].ToList();
            return Row(i + 1, p.Title, p.Code,
                unit is null ? null : new ReportListValue(unit.Key, unit.Title),
                type is null ? null : new ReportListValue(type.Key, type.Title),
                unit is not null && string.Equals(unit.ManagerPositionKey, p.Key, StringComparison.OrdinalIgnoreCase),
                string.Join("، ", held.Select(h => names.GetValueOrDefault(h.UserId, h.UserId)).Distinct()),
                (decimal)held.Count, held.Count == 0, p.IsActive);
        }).ToList();
    }

    private async Task<IReadOnlyList<ReportRow>> AssignmentsAsync(CancellationToken ct)
    {
        var (snapshot, assignments) = await OrgAsync(ct);
        var now = time.GetUtcNow().UtcDateTime;
        var names = await NamesAsync(assignments.Select(a => a.UserId), ct);
        return assignments.Select(a => Row(a.Id,
            new ReportListValue(a.UserId, names.GetValueOrDefault(a.UserId, a.UserId)),
            new ReportListValue(a.PositionKey, snapshot.FindPosition(a.PositionKey)?.Title ?? a.PositionKey),
            new ReportListValue(a.OrgUnitKey, snapshot.FindUnit(a.OrgUnitKey)?.Title ?? a.OrgUnitKey),
            new ReportListValue(a.Kind.ToString(), a.Kind == AssignmentKind.Acting ? "سرپرستی" : "اصلی"),
            Utc(a.ValidFrom), Utc(a.ValidTo), Current(a, now))).ToList();
    }

    private async Task<IReadOnlyDictionary<string, string>> NamesAsync(IEnumerable<string> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var names = await userNames.GetAsync(ids.Select(id => long.TryParse(id, out var v) ? v : (long?)null), ct);
        return names.ToDictionary(n => Id(n.Key), n => n.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Func<long?, ReportListValue?>> AuthorsAsync(IEnumerable<long?> ids, CancellationToken ct)
    {
        var names = await userNames.GetAsync(ids, ct);
        return id => id is { } v ? new ReportListValue(Id(v), names.GetValueOrDefault(v) ?? Id(v)) : null;
    }

    // ------------------------------------------------------------------ definitions

    private async Task<IReadOnlyList<ReportRow>> TemplatesAsync(CancellationToken ct)
    {
        var templates = await db.Templates.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
        var versions = (await db.TemplateVersions.AsNoTracking().Select(v => new { v.Id, v.TemplateId, v.VersionNo, v.IsPublished }).ToListAsync(ct))
            .ToLookup(v => v.TemplateId);
        var fields = (await db.TemplateFields.AsNoTracking().GroupBy(f => f.TemplateVersionId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);
        // records and imports as the user sees them (the data scope filters)
        var records = (await db.DataRecords.AsNoTracking().GroupBy(r => r.TemplateId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);
        var imports = (await (from b in db.ImportBatches.AsNoTracking()
                              join v in db.TemplateVersions on b.TemplateVersionId equals v.Id
                              group b by v.TemplateId into g
                              select new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);
        var units = (await db.TemplateOrgUnits.AsNoTracking().Select(u => new { u.TemplateId, u.OrgUnitKey }).ToListAsync(ct)).ToLookup(u => u.TemplateId, u => u.OrgUnitKey);
        var snapshot = await chart.GetSnapshotAsync(ct);
        var author = await AuthorsAsync(templates.Select(t => t.CreatedBy), ct);

        return templates.Select(t =>
        {
            var current = versions[t.Id].OrderByDescending(v => v.IsPublished).ThenByDescending(v => v.VersionNo).FirstOrDefault();
            return Row(t.Id, t.Code, t.Name, t.Description, new ReportListValue(t.Status.ToString(), t.Status.ToLabel()),
                (decimal)versions[t.Id].Count(), current is null ? null : (decimal?)current.VersionNo,
                current is null ? 0m : fields.GetValueOrDefault(current.Id), (decimal)records.GetValueOrDefault(t.Id), (decimal)imports.GetValueOrDefault(t.Id),
                string.Join("، ", units[t.Id].Select(k => snapshot.FindUnit(k)?.Title ?? k)),
                author(t.CreatedBy), Utc(t.CreatedAt), Utc(t.UpdatedAt));
        }).ToList();
    }

    private async Task<IReadOnlyList<ReportRow>> FieldsAsync(CancellationToken ct)
    {
        var rows = await (from f in db.TemplateFields.AsNoTracking()
                          join v in db.TemplateVersions on f.TemplateVersionId equals v.Id
                          join t in db.Templates on v.TemplateId equals t.Id
                          select new { f.Id, t.Code, t.Name, v.VersionNo, f.FieldKey, f.Label, f.DataType, f.IsRequired, f.DataSourceId, f.SortOrder })
            .ToListAsync(ct);
        var aliases = (await db.TemplateFieldAliases.AsNoTracking().GroupBy(a => a.TemplateFieldId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);
        var sources = await db.DataSources.AsNoTracking().Select(d => new { d.Id, d.Code, d.Name }).ToDictionaryAsync(d => d.Id, ct);
        return rows.OrderBy(r => r.Name).ThenBy(r => r.VersionNo).ThenBy(r => r.SortOrder).Select(r => Row(r.Id,
            new ReportListValue(r.Code, r.Name), (decimal)r.VersionNo, r.FieldKey, r.Label,
            new ReportListValue(r.DataType.ToString(), r.DataType.ToLabel()), r.IsRequired,
            r.DataSourceId is { } s && sources.TryGetValue(s, out var src) ? new ReportListValue(src.Code, src.Name) : null,
            (decimal)aliases.GetValueOrDefault(r.Id), (decimal)r.SortOrder)).ToList();
    }

    private async Task<IReadOnlyList<ReportRow>> DataSourcesAsync(CancellationToken ct)
    {
        var sources = await db.DataSources.AsNoTracking().OrderBy(d => d.Name).ToListAsync(ct);
        var items = (await db.DataSourceItems.AsNoTracking().GroupBy(i => i.DataSourceId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);
        var used = (await db.TemplateFields.AsNoTracking().Where(f => f.DataSourceId != null).GroupBy(f => f.DataSourceId!.Value)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Key, x => x.Count);
        var author = await AuthorsAsync(sources.Select(d => d.CreatedBy), ct);
        return sources.Select(d => Row(d.Id, d.Code, d.Name, new ReportListValue(d.SourceType.ToString(), d.SourceType.ToLabel()), d.IsActive,
            (decimal)items.GetValueOrDefault(d.Id), (decimal)used.GetValueOrDefault(d.Id), author(d.CreatedBy), Utc(d.CreatedAt), Utc(d.UpdatedAt))).ToList();
    }

    private async Task<IReadOnlyList<ReportRow>> ProfilesAsync(CancellationToken ct)
    {
        var rows = await (from p in db.MappingProfiles.AsNoTracking()
                          join v in db.TemplateVersions on p.TemplateVersionId equals v.Id
                          join t in db.Templates on v.TemplateId equals t.Id
                          select new { p.Id, p.Name, t.Code, TemplateName = t.Name, v.VersionNo, p.Status, p.CreatedAt })
            .ToListAsync(ct);
        var rules = (await db.MappingRules.AsNoTracking().GroupBy(r => r.MappingProfileId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);
        return rows.OrderBy(r => r.Name).Select(r => Row(r.Id, r.Name, new ReportListValue(r.Code, r.TemplateName), (decimal)r.VersionNo,
            new ReportListValue(r.Status.ToString(), r.Status.ToLabel()), (decimal)rules.GetValueOrDefault(r.Id), Utc(r.CreatedAt))).ToList();
    }
}
