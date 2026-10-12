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
    public async Task<(FilteredPage Page, string? SourceKey)> RecordsAsync(
        long versionId, string? q, string? search, RecordSource? source, long? importBatchId, int page, int pageSize, CancellationToken ct)
    {
        var target = await sources.VersionAsync(versionId, ct);
        if (target is null)
            return (new FilteredPage(null, 0, null, null), null);

        var (sourceKey, versionNo) = target.Value;
        var info = await sources.FindAsync(sourceKey, ct);
        var query = Parse(q, search, info, out string? parseError);
        if (parseError is not null)
            return (new FilteredPage(null, 0, null, parseError), sourceKey);
        if (query is null && source is null && importBatchId is null)
            return (new FilteredPage(null, 0, null, null), sourceKey);

        string? json = query is null ? null : ReportQueryJson.Serialize(query);
        var run = query ?? new ReportQuery();
        run.Filter = new FilterGroup { Conditions = [Condition(MapperReportSources.Sys.Version, versionNo.ToString(CultureInfo.InvariantCulture))], Groups = [run.Filter] };
        if (source is { } s)
            run.Filter.Conditions.Add(Condition(MapperReportSources.Sys.Source, s == RecordSource.Import ? "import" : "manual"));
        if (importBatchId is { } batch)
            run.Filter.Conditions.Add(Condition(MapperReportSources.Sys.Import, batch.ToString(CultureInfo.InvariantCulture)));

        return (await RunAsync(sourceKey, run, json, info, page, pageSize, ct), sourceKey);
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
        var run = query;
        if (status is not null)
            run.Filter = new FilterGroup { Conditions = [Condition(MapperReportSources.Imp.Status, status)], Groups = [run.Filter] };

        return await RunAsync(MapperReportSources.ImportsKey, run, json, info, page, pageSize, ct);
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
