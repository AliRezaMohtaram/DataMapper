using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.DataSources.Common;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.DataSources;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.Common;

/// <summary>
/// فیلد قالب برای ورود داده. اگر به منبع داده وصل باشد:
/// Inline پر است (منبع کوچک: لیست دستی/فایل تا InlineLimit گزینه) یا Inline خالی و DataSourceId پر است (جستجوی سمت سرور).
/// </summary>
public sealed record RecordField(
    TargetField Target,
    long? DataSourceId,
    IReadOnlyList<DataSourceOption>? Inline)
{
    public bool HasSource => DataSourceId.HasValue;

    public bool IsRemote => DataSourceId.HasValue && Inline is null;
}

public sealed record RecordBuildResult(string? Json, IReadOnlyDictionary<string, string> Errors);

/// <summary>خواندن فیلدهای نسخه، اعتبارسنجی مقدارهای فرم و ساخت DataJson (مشترک بین ثبت و ویرایش).</summary>
public static class RecordDataBuilder
{
    public static async Task<List<RecordField>> LoadFieldsAsync(
        IAppDbContext db,
        long templateVersionId,
        CancellationToken ct)
    {
        var rows = await (
            from f in db.TemplateFields.AsNoTracking()
            join d in db.DataSources.AsNoTracking() on f.DataSourceId equals d.Id into ds
            from d in ds.DefaultIfEmpty()
            where f.TemplateVersionId == templateVersionId
            orderby f.SortOrder, f.Id
            select new
            {
                Field = f,
                SourceId = d != null ? (long?)d.Id : null,
                SourceType = d != null ? (DataSourceType?)d.SourceType : null,
                Active = d != null && d.IsActive
            }).ToListAsync(ct);

        // منبع‌های کوچکِ لیست/فایل → گزینه‌ها همراه فرم می‌آیند
        var listIds = rows
            .Where(r => r.Active && r.SourceType is DataSourceType.StaticList or DataSourceType.File)
            .Select(r => r.SourceId!.Value)
            .Distinct()
            .ToList();

        var counts = listIds.Count == 0
            ? new Dictionary<long, int>()
            : await db.DataSourceItems
                .Where(i => listIds.Contains(i.DataSourceId))
                .GroupBy(i => i.DataSourceId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        var smallIds = counts
            .Where(c => c.Value <= DataSourceOptionService.InlineLimit)
            .Select(c => c.Key)
            .ToList();

        // منبع بدون گزینه هم «کوچک» است (فهرست خالی)
        smallIds.AddRange(listIds.Where(id => !counts.ContainsKey(id)));

        var inline = smallIds.Count == 0
            ? new Dictionary<long, List<DataSourceOption>>()
            : (await db.DataSourceItems
                    .AsNoTracking()
                    .Where(i => smallIds.Contains(i.DataSourceId))
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .Select(i => new { i.DataSourceId, i.Value, i.Label })
                    .ToListAsync(ct))
                .GroupBy(i => i.DataSourceId)
                .ToDictionary(g => g.Key, g => g.Select(i => new DataSourceOption(i.Value, i.Label)).ToList());

        return rows.Select(r =>
        {
            var target = new TargetField(
                r.Field.Id, r.Field.FieldKey, r.Field.Label, r.Field.DataType, r.Field.IsRequired,
                r.Field.Length, r.Field.Precision, r.Field.Scale, r.Field.Regex, r.Field.DefaultValue);

            if (!r.SourceId.HasValue)
                return new RecordField(target, null, null);

            IReadOnlyList<DataSourceOption>? options = null;

            if (r.Active && inline.TryGetValue(r.SourceId.Value, out var list))
                options = list;
            else if (r.Active && smallIds.Contains(r.SourceId.Value))
                options = Array.Empty<DataSourceOption>();

            return new RecordField(target, r.SourceId, options);
        }).ToList();
    }

    /// <summary>
    /// مقدارهای ارسالی را اعتبارسنجی می‌کند. فیلدهای پنهان‌شده با شرط نمایش (showIf) الزامی نیستند و مقدارشان ذخیره نمی‌شود.
    /// مقدار فیلد متصل به منبع داده باید یکی از گزینه‌های همان منبع باشد.
    /// </summary>
    public static async Task<RecordBuildResult> BuildAsync(
        IReadOnlyList<RecordField> fields,
        IReadOnlyDictionary<string, string?> submitted,
        ISet<string> hiddenKeys,
        DataSourceOptionService options,
        CancellationToken ct)
    {
        var values = new Dictionary<string, string?>(submitted, StringComparer.OrdinalIgnoreCase);
        var output = new List<KeyValuePair<string, object?>>();
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in fields)
        {
            var key = f.Target.Key;

            if (hiddenKeys.Contains(key))
            {
                output.Add(new KeyValuePair<string, object?>(key, null));
                continue;
            }

            values.TryGetValue(key, out var raw);

            var result = ImportValueValidator.Validate(f.Target, raw);

            if (result.Error is null && result.Value is not null && f.HasSource)
            {
                var text = result.Value.ToString() ?? string.Empty;
                string? sourceError = null;

                if (f.Inline is not null)
                {
                    if (!f.Inline.Any(o => string.Equals(o.Value, text, StringComparison.Ordinal)))
                        sourceError = "مقدار انتخاب‌شده در فهرست مجاز نیست.";
                }
                else
                {
                    var found = await options.FindAsync(f.DataSourceId!.Value, text, ct);

                    if (found.Error is not null)
                        sourceError = found.Error;
                    else if (found.Option is null)
                        sourceError = "مقدار انتخاب‌شده در منبع داده وجود ندارد.";
                }

                if (sourceError is not null)
                    result = new FieldValue(null, $"«{f.Target.Label}»: {sourceError}");
            }

            if (result.Error is not null)
            {
                var prefix = $"«{f.Target.Label}»: ";
                errors[key] = result.Error.StartsWith(prefix, StringComparison.Ordinal)
                    ? result.Error[prefix.Length..]
                    : result.Error;
            }

            output.Add(new KeyValuePair<string, object?>(key, result.Value));
        }

        return errors.Count > 0
            ? new RecordBuildResult(null, errors)
            : new RecordBuildResult(ImportJson.BuildMapped(output), errors);
    }
}
