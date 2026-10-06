using System.Text.Json;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.DataSources;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.Common;

public sealed record RecordOption(string Value, string Label);

/// <summary>فیلد قالب + گزینه‌های مجاز (اگر به منبع داده لیست ثابت وصل باشد).</summary>
public sealed record RecordField(TargetField Target, IReadOnlyList<RecordOption> Options);

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
                Config = d != null && d.IsActive && d.SourceType == DataSourceType.StaticList ? d.ConfigJson : null
            }).ToListAsync(ct);

        return rows.Select(r => new RecordField(
                new TargetField(
                    r.Field.Id, r.Field.FieldKey, r.Field.Label, r.Field.DataType, r.Field.IsRequired,
                    r.Field.Length, r.Field.Precision, r.Field.Scale, r.Field.Regex, r.Field.DefaultValue),
                ParseStaticOptions(r.Config)))
            .ToList();
    }

    /// <summary>
    /// ConfigJson لیست ثابت: آرایه‌ای از متن/عدد یا شیء {value, label}؛ یا شیئی که آرایه را در
    /// items / options / values دارد.
    /// </summary>
    public static IReadOnlyList<RecordOption> ParseStaticOptions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<RecordOption>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                JsonElement? found = null;

                foreach (var name in new[] { "items", "options", "values" })
                {
                    if (root.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        found = arr;
                        break;
                    }
                }

                if (found is null)
                    return Array.Empty<RecordOption>();

                root = found.Value;
            }

            if (root.ValueKind != JsonValueKind.Array)
                return Array.Empty<RecordOption>();

            var list = new List<RecordOption>();

            foreach (var item in root.EnumerateArray())
            {
                switch (item.ValueKind)
                {
                    case JsonValueKind.String:
                    case JsonValueKind.Number:
                    {
                        var v = item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText();
                        if (!string.IsNullOrWhiteSpace(v))
                            list.Add(new RecordOption(v.Trim(), v.Trim()));
                        break;
                    }
                    case JsonValueKind.Object:
                    {
                        var value = ReadText(item, "value") ?? ReadText(item, "code") ?? ReadText(item, "id");
                        var label = ReadText(item, "label") ?? ReadText(item, "text") ?? ReadText(item, "name") ?? value;

                        if (!string.IsNullOrWhiteSpace(value))
                            list.Add(new RecordOption(value.Trim(), (label ?? value).Trim()));
                        break;
                    }
                }
            }

            return list
                .GroupBy(o => o.Value, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<RecordOption>();
        }
    }

    private static string? ReadText(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p))
            return null;

        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            _ => null
        };
    }

    /// <summary>
    /// مقدارهای ارسالی را اعتبارسنجی می‌کند. فیلدهای پنهان‌شده با شرط نمایش (showIf) الزامی نیستند و مقدارشان ذخیره نمی‌شود.
    /// </summary>
    public static RecordBuildResult Build(
        IReadOnlyList<RecordField> fields,
        IReadOnlyDictionary<string, string?> submitted,
        ISet<string> hiddenKeys)
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

            if (result.Error is null && result.Value is not null && f.Options.Count > 0)
            {
                var text = result.Value.ToString();

                if (!f.Options.Any(o => string.Equals(o.Value, text, StringComparison.Ordinal)))
                    result = new FieldValue(null, $"«{f.Target.Label}»: مقدار انتخاب‌شده در فهرست مجاز نیست.");
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
