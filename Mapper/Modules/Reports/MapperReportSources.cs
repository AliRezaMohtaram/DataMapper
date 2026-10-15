using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Imports;
using Acl.Core.Abstractions;
using Borc.DataMapper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using MReporter.Core.Abstractions;
using MReporter.Core.Queries;

namespace Borc.DataMapper.Web.Modules.Reports;

/// <summary>
/// Mapper's report sources: the records of each template ("mapper.records:{template code}") and the imports
/// ("mapper.imports"). A template's columns are its fields (by field key, stable across versions; title and type from the
/// newest version that has the field) and the system columns every record has. Also the definition sources
/// (<see cref="DefinitionSources"/>): found for every run, listed for new reports only with View on their page.
/// </summary>
public sealed class MapperReportSources(BorcDataMapperDbContext db, IAccessService access) : IReportSourceCatalog
{
    public const string RecordsPrefix = "mapper.records:";
    public const string ImportsKey = "mapper.imports";

    /// <summary>System columns of records (prefixed so they never clash with a field key).</summary>
    public static class Sys
    {
        public const string Version = "sys.version";
        public const string Unit = "sys.unit";
        public const string Source = "sys.source";
        public const string Author = "sys.author";
        public const string CreatedAt = "sys.createdAt";
        public const string Editor = "sys.editor";
        public const string UpdatedAt = "sys.updatedAt";
        public const string Import = "sys.import";
    }

    /// <summary>Columns of the imports source.</summary>
    public static class Imp
    {
        public const string File = "imp.file";
        public const string Template = "imp.template";
        public const string Version = "imp.version";
        public const string Status = "imp.status";
        public const string Total = "imp.total";
        public const string Valid = "imp.valid";
        public const string Invalid = "imp.invalid";
    }

    private Dictionary<string, ReportSourceInfo>? _byKey;

    public async Task<IReadOnlyList<ReportSourceInfo>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        var rights = await access.GetCurrentUserAccessAsync(cancellationToken);
        return (await LoadAsync(cancellationToken)).Values
            .Where(s => DefinitionSources.Find(s.Key) is not { } d || rights.IsAllowed(d.Resource, "View"))
            .OrderBy(s => s.Group).ThenBy(s => s.Title, StringComparer.CurrentCulture).ToList();
    }

    public async Task<ReportSourceInfo?> FindAsync(string sourceKey, CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken)).GetValueOrDefault(sourceKey);

    /// <summary>The template id behind a records source key, or null.</summary>
    public async Task<long?> TemplateIdAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        if (!sourceKey.StartsWith(RecordsPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        string code = sourceKey[RecordsPrefix.Length..];
        return await db.Templates.AsNoTracking().Where(t => t.Code == code).Select(t => (long?)t.Id).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>The records source of a template version and the version's number, or null.</summary>
    public async Task<(string SourceKey, int VersionNo)?> VersionAsync(long versionId, CancellationToken cancellationToken = default)
    {
        var found = await (
                from v in db.TemplateVersions.AsNoTracking()
                join t in db.Templates on v.TemplateId equals t.Id
                where v.Id == versionId
                select new { t.Code, v.VersionNo })
            .FirstOrDefaultAsync(cancellationToken);
        return found is null ? null : (RecordsPrefix + found.Code, found.VersionNo);
    }

    /// <summary>The field keys of one template version, in form order.</summary>
    public async Task<IReadOnlyList<string>> VersionFieldKeysAsync(long versionId, CancellationToken cancellationToken = default) =>
        await db.TemplateFields.AsNoTracking()
            .Where(f => f.TemplateVersionId == versionId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
            .Select(f => f.FieldKey)
            .ToListAsync(cancellationToken);

    /// <summary>List fields of a template: field key → data source of the newest version that has the field.</summary>
    public async Task<IReadOnlyDictionary<string, long>> FieldSourcesAsync(long templateId, CancellationToken cancellationToken = default) =>
        (await (
                from f in db.TemplateFields.AsNoTracking()
                join v in db.TemplateVersions on f.TemplateVersionId equals v.Id
                where v.TemplateId == templateId && f.DataSourceId != null
                select new { f.FieldKey, v.VersionNo, DataSourceId = f.DataSourceId!.Value })
            .ToListAsync(cancellationToken))
            .GroupBy(f => f.FieldKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.VersionNo).First().DataSourceId, StringComparer.OrdinalIgnoreCase);

    private async Task<Dictionary<string, ReportSourceInfo>> LoadAsync(CancellationToken ct)
    {
        if (_byKey is not null)
            return _byKey;

        var templates = await db.Templates.AsNoTracking().Select(t => new { t.Id, t.Code, t.Name }).ToListAsync(ct);

        // every field of every version; the newest version that has a key gives its title and type
        var fields = await (
                from f in db.TemplateFields.AsNoTracking()
                join v in db.TemplateVersions on f.TemplateVersionId equals v.Id
                select new { v.TemplateId, v.VersionNo, f.FieldKey, f.Label, f.DataType, f.DataSourceId, f.SortOrder })
            .ToListAsync(ct);

        var units = (await db.TemplateOrgUnits.AsNoTracking().Select(u => new { u.TemplateId, u.OrgUnitKey }).ToListAsync(ct))
            .ToLookup(u => u.TemplateId, u => u.OrgUnitKey);

        var result = new Dictionary<string, ReportSourceInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in templates)
        {
            var columns = fields.Where(f => f.TemplateId == t.Id)
                .GroupBy(f => f.FieldKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(f => f.VersionNo).First())
                .OrderBy(f => f.SortOrder).ThenBy(f => f.FieldKey, StringComparer.OrdinalIgnoreCase)
                .Select(f => new ReportColumn(f.FieldKey, f.Label, TypeOf(f.DataType, f.DataSourceId.HasValue)))
                .Concat(RecordSystemColumns)
                .ToList();

            string key = RecordsPrefix + t.Code;
            result[key] = new ReportSourceInfo(key, t.Name, columns, "قالب‌ها", units[t.Id].ToList());
        }

        result[ImportsKey] = new ReportSourceInfo(ImportsKey, "ایمپورت‌ها",
        [
            new ReportColumn(Imp.File, "فایل", ReportColumnType.Text),
            new ReportColumn(Imp.Template, "قالب", ReportColumnType.List),
            new ReportColumn(Imp.Version, "نسخه", ReportColumnType.Number),
            new ReportColumn(Imp.Status, "وضعیت", ReportColumnType.List),
            new ReportColumn(Imp.Total, "کل سطرها", ReportColumnType.Number),
            new ReportColumn(Imp.Valid, "سطرهای معتبر", ReportColumnType.Number),
            new ReportColumn(Imp.Invalid, "سطرهای نامعتبر", ReportColumnType.Number),
            new ReportColumn(Sys.Unit, "واحد سازمانی", ReportColumnType.List, IsSystem: true),
            new ReportColumn(Sys.Author, "آپلودکننده", ReportColumnType.List, IsSystem: true),
            new ReportColumn(Sys.CreatedAt, "زمان آپلود", ReportColumnType.DateTime, IsSystem: true),
        ], "سیستم");

        foreach (var (source, _) in DefinitionSources.All)
            result[source.Key] = source;

        return _byKey = result;
    }

    private static readonly ReportColumn[] RecordSystemColumns =
    [
        new(Sys.Version, "نسخه", ReportColumnType.Number, IsSystem: true),
        new(Sys.Unit, "واحد سازمانی", ReportColumnType.List, IsSystem: true),
        new(Sys.Source, "منبع", ReportColumnType.List, IsSystem: true),
        new(Sys.Author, "ثبت‌کننده", ReportColumnType.List, IsSystem: true),
        new(Sys.CreatedAt, "زمان ثبت", ReportColumnType.DateTime, IsSystem: true),
        new(Sys.Editor, "آخرین ویرایش‌کننده", ReportColumnType.List, IsSystem: true),
        new(Sys.UpdatedAt, "زمان ویرایش", ReportColumnType.DateTime, IsSystem: true),
        new(Sys.Import, "شمارهٔ ایمپورت", ReportColumnType.Number, IsSystem: true),
    ];

    private static ReportColumnType TypeOf(FieldDataType type, bool hasSource) => hasSource
        ? ReportColumnType.List
        : type switch
        {
            FieldDataType.Number => ReportColumnType.Number,
            FieldDataType.Date => ReportColumnType.Date,
            FieldDataType.DateTime => ReportColumnType.DateTime,
            _ => ReportColumnType.Text,
        };

    /// <summary>Value of the "status" list column of imports.</summary>
    public static string StatusValue(ImportBatchStatus status) => status.ToString();
}
