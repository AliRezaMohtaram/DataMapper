using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Borc.DataMapper.Application.Abstractions.Identity;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Infrastructure.Persistence;
using Borc.DataMapper.Web.ViewModels.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MReporter.Core.Abstractions;
using MReporter.Core.Execution;
using MReporter.Core.Model;
using MReporter.Core.Queries;
using MReporter.Sql;
using OrgChart.Core.Chart;
using Sys = Borc.DataMapper.Web.Modules.Reports.MapperReportSources.Sys;
using Imp = Borc.DataMapper.Web.Modules.Reports.MapperReportSources.Imp;

namespace Borc.DataMapper.Web.Modules.Reports;

/// <summary>
/// Runs report queries on Mapper's records and imports with SQL built by MReporter.Sql (parameters only; field values are
/// read from DataRecord.DataJson with JSON_VALUE on SQL Server and json_extract on SQLite). Raw SQL bypasses EF's query
/// filters, so deleted rows and the run's scope are applied here: Viewer = the same rules as the lists
/// (<see cref="IDataScopeProvider"/>: own rows, units headed, Acl data scope, "only my data"); Units / All = granted by a
/// shared report. List values come back with their titles (data source items, unit titles, user names).
/// </summary>
public sealed class MapperReportData(
    BorcDataMapperDbContext db,
    MapperReportSources sources,
    IDataScopeProvider visibility,
    IOrgChartReader chart,
    UserNames userNames) : IReportDataProvider
{
    private const int LabelBatch = 500;

    public bool Serves(string sourceKey) =>
        sourceKey.StartsWith(MapperReportSources.RecordsPrefix, StringComparison.OrdinalIgnoreCase)
        || string.Equals(sourceKey, MapperReportSources.ImportsKey, StringComparison.OrdinalIgnoreCase);

    public async Task<ReportPage> QueryAsync(ReportExecution execution, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var plan = await PlanAsync(execution, cancellationToken);

        int total;
        await using (var count = await CommandAsync($"SELECT COUNT(*) {plan.From} WHERE {plan.Where}", plan.Sql, cancellationToken))
        {
            try
            {
                total = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }

        var rows = new List<ReportRow>();
        if (total > 0)
        {
            string text = $"SELECT {plan.Select} {plan.From} WHERE {plan.Where} {plan.Sql.OrderBy(execution.Query.Sort, plan.TieBreaker)} {plan.Sql.Page((page - 1) * pageSize, pageSize)}";
            await foreach (var row in ReadAsync(text, plan, execution, cancellationToken))
                rows.Add(row);
        }

        return new ReportPage(execution.Columns, rows, page, pageSize, total);
    }

    public async IAsyncEnumerable<ReportRow> StreamAsync(ReportExecution execution, int maxRows, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var plan = await PlanAsync(execution, cancellationToken);
        string text = $"SELECT {plan.Select} {plan.From} WHERE {plan.Where} {plan.Sql.OrderBy(execution.Query.Sort, plan.TieBreaker)} {plan.Sql.Page(0, maxRows)}";
        await foreach (var row in ReadAsync(text, plan, execution, cancellationToken))
            yield return row;
    }

    // ------------------------------------------------------------------ plan

    private sealed record Plan(ReportSqlBuilder Sql, string Select, string From, string Where, string TieBreaker, bool Records, IReadOnlyDictionary<string, long> FieldSources);

    private async Task<Plan> PlanAsync(ReportExecution execution, CancellationToken ct)
    {
        bool sqlite = db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;
        var dialect = sqlite ? SqlDialect.Sqlite : SqlDialect.SqlServer;
        var expressions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sql = new ReportSqlBuilder(dialect, execution.Source, expressions);
        string Text(string column) => sqlite ? $"CAST({column} AS TEXT)" : $"CAST({column} AS nvarchar(20))";

        if (string.Equals(execution.Source.Key, MapperReportSources.ImportsKey, StringComparison.OrdinalIgnoreCase))
        {
            expressions[Imp.File] = "b.FileName";
            expressions[Imp.Template] = "t.Code";
            expressions[Imp.Version] = "v.VersionNo";
            expressions[Imp.Status] = "CASE b.Status " + string.Join(" ", Enum.GetValues<ImportBatchStatus>().Select(s => $"WHEN {(int)s} THEN '{s}'")) + " END";
            expressions[Imp.Total] = "b.TotalRows";
            expressions[Imp.Valid] = "b.ValidRows";
            expressions[Imp.Invalid] = "b.InvalidRows";
            expressions[Sys.Unit] = "b.OrgUnitKey";
            expressions[Sys.Author] = Text("b.CreatedBy");
            expressions[Sys.CreatedAt] = "b.CreatedAt";

            string where = $"b.IsDeleted = 0 AND {Scope(sql, execution.Scope, "Mapper.Imports", "b", await DescendantsAsync(execution.Scope, ct))} AND {sql.Where(execution.Query.Filter)}";
            return new Plan(sql,
                "b.Id, b.FileName, t.Code, t.Name, v.VersionNo, b.Status, b.TotalRows, b.ValidRows, b.InvalidRows, b.OrgUnitKey, b.CreatedBy, b.CreatedAt",
                "FROM ImportBatch b JOIN TemplateVersion v ON v.Id = b.TemplateVersionId JOIN Template t ON t.Id = v.TemplateId",
                where, "b.Id DESC", Records: false, new Dictionary<string, long>());
        }

        long templateId = await sources.TemplateIdAsync(execution.Source.Key, ct)
            ?? throw new ReportRunException(RunErrors.SourceNotFound, $"Unknown template source '{execution.Source.Key}'.");

        foreach (var column in execution.Source.Columns.Where(c => !c.IsSystem))
        {
            string path = sql.Add("$.\"" + column.Key + "\"");
            string raw = sqlite ? $"json_extract(r.DataJson, {path})" : $"JSON_VALUE(r.DataJson, {path})";
            expressions[column.Key] = (column.Type, sqlite) switch
            {
                (ReportColumnType.Number, false) => $"TRY_CONVERT(decimal(38, 10), {raw})",
                (ReportColumnType.Date, false) => $"TRY_CONVERT(date, {raw})",
                (ReportColumnType.DateTime, false) => $"TRY_CONVERT(datetime2, {raw})",
                (ReportColumnType.DateTime, true) => $"replace({raw}, 'T', ' ')",
                _ => raw,
            };
        }

        expressions[Sys.Version] = "v.VersionNo";
        expressions[Sys.Unit] = "r.OrgUnitKey";
        expressions[Sys.Source] = "CASE r.SourceType WHEN 2 THEN 'import' ELSE 'manual' END";
        expressions[Sys.Author] = Text("r.CreatedBy");
        expressions[Sys.CreatedAt] = "r.CreatedAt";
        expressions[Sys.Editor] = Text("r.UpdatedBy");
        expressions[Sys.UpdatedAt] = "r.UpdatedAt";

        string template = sql.Add(templateId);
        string recordsWhere = $"r.IsDeleted = 0 AND r.TemplateId = {template} AND {Scope(sql, execution.Scope, "Mapper.DataRecords", "r", await DescendantsAsync(execution.Scope, ct))} AND {sql.Where(execution.Query.Filter)}";

        // list fields: the data source of the newest version that has the field (for the option titles)
        var fieldSources = (await (
                from f in db.TemplateFields.AsNoTracking()
                join v in db.TemplateVersions on f.TemplateVersionId equals v.Id
                where v.TemplateId == templateId && f.DataSourceId != null
                select new { f.FieldKey, v.VersionNo, DataSourceId = f.DataSourceId!.Value })
            .ToListAsync(ct))
            .GroupBy(f => f.FieldKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.VersionNo).First().DataSourceId, StringComparer.OrdinalIgnoreCase);

        return new Plan(sql,
            "r.Id, r.DataJson, v.VersionNo, r.OrgUnitKey, r.SourceType, r.CreatedBy, r.CreatedAt, r.UpdatedBy, r.UpdatedAt",
            "FROM DataRecord r JOIN TemplateVersion v ON v.Id = r.TemplateVersionId",
            recordsWhere, "r.Id DESC", Records: true, fieldSources);
    }

    /// <summary>The run's scope as SQL on the alias's OrgUnitKey / CreatedBy.</summary>
    private string Scope(ReportSqlBuilder sql, RunScope scope, string resource, string alias, IReadOnlyCollection<string> grantedUnits)
    {
        switch (scope.Kind)
        {
            case ReportScopeKind.All:
                return "1 = 1";

            case ReportScopeKind.Units:
                return $"{alias}.OrgUnitKey IN {sql.AddList(grantedUnits)}";

            default:
                var view = visibility.For(resource);
                if (view.All)
                    return "1 = 1";

                var parts = new List<string>();
                if (view.OwnerUserId is { } owner)
                    parts.Add($"{alias}.CreatedBy = {sql.Add(owner)}");
                if (view.OrgUnitKeys.Count > 0)
                    parts.Add($"{alias}.OrgUnitKey IN {sql.AddList(view.OrgUnitKeys)}");
                return parts.Count == 0 ? "1 = 0" : "(" + string.Join(" OR ", parts) + ")";
        }
    }

    /// <summary>A granted unit scope with its sub-units where asked (from the org chart).</summary>
    private async Task<IReadOnlyCollection<string>> DescendantsAsync(RunScope scope, CancellationToken ct)
    {
        if (scope.Kind != ReportScopeKind.Units)
            return [];

        var snapshot = await chart.GetSnapshotAsync(ct);
        var units = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var unit in scope.Units)
        {
            units.Add(unit.UnitKey);
            if (unit.IncludeSubUnits)
                units.UnionWith(snapshot.GetDescendantKeys(unit.UnitKey));
        }

        return units;
    }

    // ------------------------------------------------------------------ reading

    private sealed record RawRow(object?[] Values, JsonElement? Data);

    private async IAsyncEnumerable<ReportRow> ReadAsync(string text, Plan plan, ReportExecution execution, [EnumeratorCancellation] CancellationToken ct)
    {
        // option titles of the selected list fields, loaded before the reader opens: the connection has one active
        // command at a time (no MARS), and these sources are small lists
        var sourceIds = execution.Columns.Where(c => c.Type == ReportColumnType.List && !c.IsSystem && plan.FieldSources.ContainsKey(c.Key))
            .Select(c => plan.FieldSources[c.Key]).Distinct().ToList();
        var labels = sourceIds.Count == 0
            ? []
            : (await db.DataSourceItems.AsNoTracking().Where(i => sourceIds.Contains(i.DataSourceId))
                .Select(i => new { i.DataSourceId, i.Value, i.Label }).ToListAsync(ct))
                .GroupBy(i => i.DataSourceId)
                .ToDictionary(g => g.Key, g => g.GroupBy(i => i.Value).ToDictionary(v => v.Key, v => v.First().Label));

        var batch = new List<RawRow>(LabelBatch);
        await using var command = await CommandAsync(text, plan.Sql, ct);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var values = new object?[reader.FieldCount];
                for (int i = 0; i < reader.FieldCount; i++)
                    values[i] = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);

                JsonElement? data = null;
                if (plan.Records && values[1] is string json)
                {
                    try { data = JsonDocument.Parse(json).RootElement.Clone(); }
                    catch (JsonException) { }
                }

                batch.Add(new RawRow(values, data));
                if (batch.Count == LabelBatch)
                {
                    foreach (var row in await ToRowsAsync(batch, plan, execution, labels, ct))
                        yield return row;
                    batch.Clear();
                }
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        foreach (var row in await ToRowsAsync(batch, plan, execution, labels, ct))
            yield return row;
    }

    private async Task<List<ReportRow>> ToRowsAsync(List<RawRow> batch, Plan plan, ReportExecution execution,
        Dictionary<long, Dictionary<string, string>> optionTitles, CancellationToken ct)
    {
        if (batch.Count == 0)
            return [];

        var columns = execution.Columns;
        var snapshot = await chart.GetSnapshotAsync(ct);
        string UnitTitle(string key) => snapshot.FindUnit(key)?.Title ?? key;

        // user names of authors/editors in this batch
        int authorIndex = plan.Records ? 5 : 10;
        var users = await userNames.GetAsync(batch.SelectMany(r => new[] { ToLong(r.Values[authorIndex]), plan.Records ? ToLong(r.Values[7]) : null }), ct);
        ReportListValue? User(object? id) => ToLong(id) is { } v ? new ReportListValue(v.ToString(CultureInfo.InvariantCulture), users.GetValueOrDefault(v) ?? v.ToString(CultureInfo.InvariantCulture)) : null;

        var labels = columns.Where(c => plan.FieldSources.ContainsKey(c.Key) && optionTitles.ContainsKey(plan.FieldSources[c.Key]))
            .ToDictionary(c => c.Key, c => optionTitles[plan.FieldSources[c.Key]], StringComparer.OrdinalIgnoreCase);

        return batch.Select(raw => new ReportRow(columns.Select<ReportColumn, object?>(c => plan.Records ? RecordValue(c, raw) : ImportValue(c, raw)).ToList())).ToList();

        object? RecordValue(ReportColumn c, RawRow raw) => c.Key switch
        {
            Sys.Version => ToDecimal(raw.Values[2]),
            Sys.Unit => raw.Values[3] is string u ? new ReportListValue(u, UnitTitle(u)) : null,
            Sys.Source => Convert.ToInt32(raw.Values[4], CultureInfo.InvariantCulture) == 2
                ? new ReportListValue("import", "ایمپورت") : new ReportListValue("manual", "ورود دستی"),
            Sys.Author => User(raw.Values[5]),
            Sys.CreatedAt => ToDateTime(raw.Values[6]),
            Sys.Editor => User(raw.Values[7]),
            Sys.UpdatedAt => ToDateTime(raw.Values[8]),
            _ => FieldValue(c, raw.Data),
        };

        object? FieldValue(ReportColumn c, JsonElement? data)
        {
            if (data is not { ValueKind: JsonValueKind.Object } obj || !TryGet(obj, c.Key, out var e) || e.ValueKind == JsonValueKind.Null)
                return null;

            string? text = e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText();
            return c.Type switch
            {
                ReportColumnType.Number => decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null,
                ReportColumnType.Date => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null,
                ReportColumnType.DateTime => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null,
                ReportColumnType.List => string.IsNullOrEmpty(text) ? null
                    : new ReportListValue(text, labels.TryGetValue(c.Key, out var map) && map.TryGetValue(text, out var label) ? label : text),
                _ => text,
            };
        }

        object? ImportValue(ReportColumn c, RawRow raw) => c.Key switch
        {
            Imp.File => raw.Values[1],
            Imp.Template => raw.Values[2] is string code ? new ReportListValue(code, raw.Values[3] as string ?? code) : null,
            Imp.Version => ToDecimal(raw.Values[4]),
            Imp.Status => (ImportBatchStatus)Convert.ToInt32(raw.Values[5], CultureInfo.InvariantCulture) is var s
                ? new ReportListValue(s.ToString(), s.ToLabel()) : null,
            Imp.Total => ToDecimal(raw.Values[6]),
            Imp.Valid => ToDecimal(raw.Values[7]),
            Imp.Invalid => ToDecimal(raw.Values[8]),
            Sys.Unit => raw.Values[9] is string u ? new ReportListValue(u, UnitTitle(u)) : null,
            Sys.Author => User(raw.Values[10]),
            Sys.CreatedAt => ToDateTime(raw.Values[11]),
            _ => null,
        };
    }

    private static bool TryGet(JsonElement obj, string key, out JsonElement value)
    {
        if (obj.TryGetProperty(key, out value))
            return true;

        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }

        return false;
    }

    private static long? ToLong(object? value) => value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);

    private static decimal? ToDecimal(object? value) => value is null ? null : Convert.ToDecimal(value, CultureInfo.InvariantCulture);

    private static DateTime? ToDateTime(object? value) => value switch
    {
        null => null,
        DateTime t => DateTime.SpecifyKind(t, DateTimeKind.Utc),
        string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) => t,
        _ => null,
    };

    private async Task<DbCommand> CommandAsync(string text, ReportSqlBuilder sql, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct); // closed again by the caller (EF counts opens)

        var command = connection.CreateCommand();
        command.CommandText = text;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        foreach (var (name, value) in sql.Parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }
}
