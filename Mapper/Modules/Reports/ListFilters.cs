using System.Globalization;
using Borc.DataMapper.Domain.Records;
using MReporter.Core.Execution;
using MReporter.Core.Queries;
using MReporter.Razor.Resources;

namespace Borc.DataMapper.Web.Modules.Reports;

/// <summary>A page of a list chosen by the advanced filter: the row ids in order and the total.</summary>
/// <param name="QueryJson">The filter as the page carries it (q), or null when there is none.</param>
/// <param name="Error">Why the filter was not applied (shown above the list).</param>
public sealed record FilteredPage(IReadOnlyList<long>? Ids, int Total, string? QueryJson, string? Error);

/// <summary>
/// The advanced filter (MReporter) on Mapper's list pages: the page's query string "q" (ReportQuery JSON) plus the
/// page's own filters, run through <see cref="IReportRunner"/> with the user's visibility; the list then shows exactly
/// those rows. The old "search" text becomes the quick search (contains, over the text columns).
/// </summary>
public sealed class ListFilters(IReportRunner runner, MapperReportSources sources)
{
    /// <summary>Records of one template version; null ids = no filter, list as usual.</summary>
    public Task<(FilteredPage Page, string? SourceKey)> RecordsAsync(
        long versionId, string? q, string? search, RecordSource? source, long? importBatchId, int page, int pageSize, CancellationToken ct) =>
        RecordsAsync(versionId, null, q, search, source, importBatchId, page, pageSize, ct);

    /// <summary>Records of one template version, or (versionId null) of every version of a template.</summary>
    public async Task<(FilteredPage Page, string? SourceKey)> RecordsAsync(
        long? versionId, long? templateId, string? q, string? search, RecordSource? source, long? importBatchId, int page, int pageSize, CancellationToken ct)
    {
        var built = await BuildRecordsAsync(versionId, templateId, q, search, source, importBatchId, ct);
        if (built is null)
            return (new FilteredPage(null, 0, null, null), null);

        var (sourceKey, _, info, run, json, filtered, error) = built.Value;
        if (error is not null)
            return (new FilteredPage(null, 0, null, error), sourceKey);
        if (!filtered)
            return (new FilteredPage(null, 0, null, null), sourceKey);

        return (await RunAsync(sourceKey, run, json, info, page, pageSize, ct), sourceKey);
    }

    /// <summary>
    /// Excel export of what the records page shows: the same filter, the version's fields (in form order) and the
    /// system columns. Null request = the version is unknown; Error = the filter cannot be applied.
    /// </summary>
    public async Task<(RunRequest? Request, string Title, string? Error)> RecordsExportAsync(
        long? versionId, long? templateId, string? q, string? search, RecordSource? source, long? importBatchId, CancellationToken ct)
    {
        var built = await BuildRecordsAsync(versionId, templateId, q, search, source, importBatchId, ct);
        if (built is null)
            return (null, string.Empty, null);

        var (sourceKey, versionNo, info, run, _, _, error) = built.Value;
        string title = versionId is null
            ? $"{info?.Title} - همهٔ نسخه‌ها"
            : $"{info?.Title} - نسخهٔ {versionNo.ToString(CultureInfo.InvariantCulture)}";
        if (error is not null)
            return (null, title, error);

        IReadOnlyList<string> fields = versionId is { } one
            ? await sources.VersionFieldKeysAsync(one, ct)
            : [.. await sources.TemplateFieldKeysAsync(templateId!.Value, ct), MapperReportSources.Sys.Version];
        run.Columns = [.. fields,
            MapperReportSources.Sys.Unit, MapperReportSources.Sys.Author, MapperReportSources.Sys.CreatedAt,
            MapperReportSources.Sys.Editor, MapperReportSources.Sys.UpdatedAt, MapperReportSources.Sys.Source, MapperReportSources.Sys.Import];
        return (RunRequest.AdHoc(sourceKey, run), title, null);
    }

    /// <summary>
    /// The records query of a records page: the user's filter (Filtered = there is one) inside the page's own
    /// conditions — one version, or (versionId null) every version of the template (no version condition).
    /// </summary>
    private async Task<(string SourceKey, int VersionNo, ReportSourceInfo? Info, ReportQuery Run, string? Json, bool Filtered, string? Error)?> BuildRecordsAsync(
        long? versionId, long? templateId, string? q, string? search, RecordSource? source, long? importBatchId, CancellationToken ct)
    {
        string sourceKey;
        int versionNo = 0;
        if (versionId is { } vid)
        {
            var target = await sources.VersionAsync(vid, ct);
            if (target is null)
                return null;
            (sourceKey, versionNo) = target.Value;
        }
        else if (templateId is { } tid && await sources.TemplateSourceAsync(tid, ct) is { } key)
            sourceKey = key;
        else
            return null;

        var info = await sources.FindAsync(sourceKey, ct);
        var query = Parse(q, search, info, out string? parseError);
        if (parseError is not null)
            return (sourceKey, versionNo, info, new ReportQuery(), null, false, parseError);

        string? json = query is null ? null : ReportQueryJson.Serialize(query);
        var run = query ?? new ReportQuery();
        run.Filter = new FilterGroup
        {
            Conditions = versionId is null ? [] : [Condition(MapperReportSources.Sys.Version, versionNo.ToString(CultureInfo.InvariantCulture))],
            Groups = [run.Filter],
        };
        if (source is { } s)
            run.Filter.Conditions.Add(Condition(MapperReportSources.Sys.Source, s == RecordSource.Import ? "import" : "manual"));
        if (importBatchId is { } batch)
            run.Filter.Conditions.Add(Condition(MapperReportSources.Sys.Import, batch.ToString(CultureInfo.InvariantCulture)));

        return (sourceKey, versionNo, info, run, json, query is not null || source is not null || importBatchId is not null, null);
    }

    /// <summary>Imports; null ids = no filter.</summary>
    public async Task<FilteredPage> ImportsAsync(string? q, string? search, string? status, int page, int pageSize, CancellationToken ct)
    {
        var info = await sources.FindAsync(MapperReportSources.ImportsKey, ct);
        var query = Parse(q, search, info, out string? parseError);
        if (parseError is not null)
            return new FilteredPage(null, 0, null, parseError);
        if (query is null)
            return new FilteredPage(null, 0, null, null);

        string json = ReportQueryJson.Serialize(query);
        return await RunAsync(MapperReportSources.ImportsKey, WithStatus(query, status), json, info, page, pageSize, ct);
    }

    /// <summary>Excel export of the imports list as filtered (every column of the imports source).</summary>
    public async Task<(RunRequest? Request, string Title, string? Error)> ImportsExportAsync(string? q, string? search, string? status, CancellationToken ct)
    {
        var info = await sources.FindAsync(MapperReportSources.ImportsKey, ct);
        string title = info?.Title ?? "ایمپورت‌ها";
        var query = Parse(q, search, info, out string? parseError);
        if (parseError is not null)
            return (null, title, parseError);

        return (RunRequest.AdHoc(MapperReportSources.ImportsKey, WithStatus(query ?? new ReportQuery(), status)), title, null);
    }

    private static ReportQuery WithStatus(ReportQuery query, string? status)
    {
        if (status is not null)
            query.Filter = new FilterGroup { Conditions = [Condition(MapperReportSources.Imp.Status, status)], Groups = [query.Filter] };
        return query;
    }

    private async Task<FilteredPage> RunAsync(string sourceKey, ReportQuery query, string? json, ReportSourceInfo? info, int page, int pageSize, CancellationToken ct)
    {
        try
        {
            var result = await runner.RunAsync(RunRequest.AdHoc(sourceKey, query), page, pageSize, ct);
            var ids = result.Rows.Select(r => long.Parse(r.Key!, CultureInfo.InvariantCulture)).ToList();
            return new FilteredPage(ids, result.Total, json, null);
        }
        catch (ReportRunException ex) when (ex.QueryErrors.Count > 0)
        {
            return new FilteredPage(null, 0, null, ReportText.Format("Filter_Ignored", string.Join(" ", ex.QueryErrors.Select(e => ReportText.QueryError(e, info)))));
        }
    }

    /// <summary>The page's filter: q (JSON), else the old search text as a quick search; null when there is none.</summary>
    private static ReportQuery? Parse(string? q, string? search, ReportSourceInfo? info, out string? error)
    {
        error = null;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var query = ReportQueryJson.TryDeserialize(q);
            if (query is null)
                error = ReportText.Format("Filter_Ignored", ReportText.Format("QueryError_Unknown", "JSON"));
            else
                query.Columns = []; // the list shows its own columns
            return query;
        }

        if (string.IsNullOrWhiteSpace(search) || info is null)
            return null;

        var text = info.Columns.Where(c => c.Type == ReportColumnType.Text && !c.IsSystem).ToList();
        return new ReportQuery
        {
            Filter = new FilterGroup
            {
                Groups = [new FilterGroup { Logic = FilterLogic.Or, Conditions = text.Select(c => Condition(c.Key, search.Trim(), FilterOperator.Contains)).ToList() }],
            },
        };
    }

    private static FilterCondition Condition(string column, string value, FilterOperator op = FilterOperator.Equals) =>
        new() { Column = column, Operator = op, Values = [value] };
}
