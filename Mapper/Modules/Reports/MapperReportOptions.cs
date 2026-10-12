using System.Globalization;
using Borc.DataMapper.Application.DataSources.Common;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Infrastructure.Persistence;
using Borc.DataMapper.Web.ViewModels.Imports;
using Borc.Users.Services;
using Microsoft.EntityFrameworkCore;
using MReporter.Core.Abstractions;
using MReporter.Core.Execution;
using OrgChart.Core.Chart;
using Sys = Borc.DataMapper.Web.Modules.Reports.MapperReportSources.Sys;
using Imp = Borc.DataMapper.Web.Modules.Reports.MapperReportSources.Imp;

namespace Borc.DataMapper.Web.Modules.Reports;

/// <summary>
/// Options of the list columns of Mapper's sources for the advanced filter: a field's data source items, the chart's
/// units, users (by name), record sources, templates and import states.
/// </summary>
public sealed class MapperReportOptions(
    BorcDataMapperDbContext db,
    MapperReportSources sources,
    DataSourceOptionService dataSourceOptions,
    IOrgChartReader chart,
    IUserLookup users,
    UserNames userNames) : IReportOptionProvider
{
    public bool Serves(string sourceKey) =>
        sourceKey.StartsWith(MapperReportSources.RecordsPrefix, StringComparison.OrdinalIgnoreCase)
        || string.Equals(sourceKey, MapperReportSources.ImportsKey, StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<ReportListValue>> SearchAsync(string sourceKey, string columnKey, string? search, int take, CancellationToken cancellationToken = default)
    {
        string? q = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        switch (columnKey)
        {
            case Sys.Unit:
                var snapshot = await chart.GetSnapshotAsync(cancellationToken);
                return snapshot.Units.Where(u => u.IsActive && (q is null || u.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase)))
                    .Take(take).Select(u => new ReportListValue(u.Key, u.Title)).ToList();

            case Sys.Author or Sys.Editor:
                if (q is null)
                    return [];
                return (await users.SearchAsync(q, take, cancellationToken))
                    .Select(u => new ReportListValue(u.Id.ToString(CultureInfo.InvariantCulture), u.DisplayName)).ToList();

            case Sys.Source:
                return Filter(RecordSources, q, take);

            case Imp.Status:
                return Filter(ImportStates, q, take);

            case Imp.Template:
                return (await db.Templates.AsNoTracking()
                        .Where(t => q == null || t.Name.Contains(q) || t.Code.Contains(q))
                        .OrderBy(t => t.Name).Take(take).Select(t => new { t.Code, t.Name }).ToListAsync(cancellationToken))
                    .Select(t => new ReportListValue(t.Code, t.Name)).ToList();
        }

        if (await FieldSourceAsync(sourceKey, columnKey, cancellationToken) is not { } dataSourceId)
            return [];

        var found = await dataSourceOptions.SearchAsync(dataSourceId, q, take, cancellationToken);
        return found.Items.Select(o => new ReportListValue(o.Value, o.Label)).ToList();
    }

    public async Task<IReadOnlyList<ReportListValue>> GetAsync(string sourceKey, string columnKey, IReadOnlyCollection<string> values, CancellationToken cancellationToken = default)
    {
        switch (columnKey)
        {
            case Sys.Unit:
                var snapshot = await chart.GetSnapshotAsync(cancellationToken);
                return values.Select(v => snapshot.FindUnit(v) is { } u ? new ReportListValue(v, u.Title) : null).OfType<ReportListValue>().ToList();

            case Sys.Author or Sys.Editor:
                var names = await userNames.GetAsync(values.Select(v => long.TryParse(v, out var id) ? id : (long?)null), cancellationToken);
                return names.Select(n => new ReportListValue(n.Key.ToString(CultureInfo.InvariantCulture), n.Value)).ToList();

            case Sys.Source:
                return RecordSources.Where(o => values.Contains(o.Value)).ToList();

            case Imp.Status:
                return ImportStates.Where(o => values.Contains(o.Value)).ToList();

            case Imp.Template:
                return (await db.Templates.AsNoTracking().Where(t => values.Contains(t.Code)).Select(t => new { t.Code, t.Name }).ToListAsync(cancellationToken))
                    .Select(t => new ReportListValue(t.Code, t.Name)).ToList();
        }

        if (await FieldSourceAsync(sourceKey, columnKey, cancellationToken) is not { } dataSourceId)
            return [];

        var result = new List<ReportListValue>();
        foreach (var value in values)
        {
            var found = await dataSourceOptions.FindAsync(dataSourceId, value, cancellationToken);
            if (found.Option is { } option)
                result.Add(new ReportListValue(option.Value, option.Label));
        }

        return result;
    }

    private async Task<long?> FieldSourceAsync(string sourceKey, string columnKey, CancellationToken ct) =>
        await sources.TemplateIdAsync(sourceKey, ct) is { } templateId
        && (await sources.FieldSourcesAsync(templateId, ct)).TryGetValue(columnKey, out var id)
            ? id
            : null;

    private static readonly ReportListValue[] RecordSources = [new("manual", "ورود دستی"), new("import", "ایمپورت")];

    private static readonly ReportListValue[] ImportStates =
        Enum.GetValues<ImportBatchStatus>().Select(s => new ReportListValue(s.ToString(), s.ToLabel())).ToArray();

    private static IReadOnlyList<ReportListValue> Filter(IEnumerable<ReportListValue> options, string? q, int take) =>
        options.Where(o => q is null || o.Label.Contains(q, StringComparison.CurrentCultureIgnoreCase)).Take(take).ToList();
}
