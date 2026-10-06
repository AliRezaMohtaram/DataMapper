using System.Text.Json;

namespace Borc.DataMapper.Application.DataRecords.Common;

/// <summary>
/// محاسبهٔ فیلدهای پنهان‌شده با «شرط نمایش» Layout روی سرور، با همان منطق فرم (منبعِ مخفی = مقدار خالی؛ دو گذر).
/// </summary>
public static class LayoutVisibility
{
    private sealed record Rule(string FieldKey, string SourceKey, string Op, string Value);

    public static HashSet<string> HiddenFieldKeys(
        string? layoutJson,
        IReadOnlyDictionary<string, string?> submitted)
    {
        var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(layoutJson))
            return hidden;

        var rules = ReadRules(layoutJson);

        if (rules.Count == 0)
            return hidden;

        var values = new Dictionary<string, string?>(submitted, StringComparer.OrdinalIgnoreCase);

        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var r in rules)
            {
                var v = hidden.Contains(r.SourceKey)
                    ? string.Empty
                    : (values.TryGetValue(r.SourceKey, out var s) ? s ?? string.Empty : string.Empty).Trim();

                var show = r.Op switch
                {
                    "eq" => v == r.Value,
                    "neq" => v != r.Value,
                    "filled" => v != string.Empty,
                    "empty" => v == string.Empty,
                    _ => true
                };

                if (show)
                    hidden.Remove(r.FieldKey);
                else
                    hidden.Add(r.FieldKey);
            }
        }

        return hidden;
    }

    private static List<Rule> ReadRules(string layoutJson)
    {
        var list = new List<Rule>();

        try
        {
            using var doc = JsonDocument.Parse(layoutJson);

            if (!doc.RootElement.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var b in blocks.EnumerateArray())
            {
                if (!b.TryGetProperty("type", out var t) || t.GetString() != "field")
                    continue;

                if (!b.TryGetProperty("showIf", out var s) || s.ValueKind != JsonValueKind.Object)
                    continue;

                var fieldKey = b.TryGetProperty("fieldKey", out var fk) ? fk.GetString() : null;
                var source = s.TryGetProperty("key", out var k) ? k.GetString() : null;
                var op = s.TryGetProperty("op", out var o) ? o.GetString() : null;
                var value = s.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString() ?? string.Empty
                    : string.Empty;

                if (!string.IsNullOrEmpty(fieldKey) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(op))
                    list.Add(new Rule(fieldKey, source, op, value));
            }
        }
        catch (JsonException)
        {
            // Layout خراب: شرط نمایشی اعمال نمی‌شود
        }

        return list;
    }
}
