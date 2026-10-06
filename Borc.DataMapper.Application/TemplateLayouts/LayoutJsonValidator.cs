using System.Text.Json;

namespace Borc.DataMapper.Application.TemplateLayouts;

/// <summary>اعتبارسنجی ساختار LayoutJson (مدل: version / settings / blocks).</summary>
public static class LayoutJsonValidator
{
    public const int MaxLength = 200_000;

    private static readonly HashSet<string> BlockTypes =
        new() { "field", "heading", "paragraph", "divider", "spacer", "pagebreak" };

    private static readonly HashSet<string> Widgets =
        new() { "auto", "textarea", "select", "radio" };

    private static readonly HashSet<string> Ops =
        new() { "eq", "neq", "filled", "empty" };

    /// <summary>null یعنی معتبر؛ در غیر این صورت پیام خطا.</summary>
    public static string? Validate(string? json, ISet<string> fieldKeys)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "محتوای Layout خالی است.";

        if (json.Length > MaxLength)
            return "حجم Layout بیش از حد مجاز است.";

        JsonDocument doc;

        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return "Layout معتبر نیست؛ JSON درست نیست.";
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return "ساختار Layout معتبر نیست.";

            if (!root.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
                return "Layout باید فهرست blocks داشته باشد.";

            var ids = new HashSet<string>();
            var placed = new HashSet<string>();

            foreach (var b in blocks.EnumerateArray())
            {
                if (b.ValueKind != JsonValueKind.Object)
                    return "یکی از المان‌های Layout معتبر نیست.";

                var id = GetString(b, "id");

                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                    return "شناسه المان‌ها باید یکتا و غیرخالی باشد.";

                var type = GetString(b, "type");

                if (type is null || !BlockTypes.Contains(type))
                    return "نوع یکی از المان‌ها معتبر نیست.";

                if (b.TryGetProperty("span", out var span))
                {
                    if (span.ValueKind != JsonValueKind.Number || !span.TryGetInt32(out var n) || n < 2 || n > 12)
                        return "عرض المان‌ها باید عددی بین 2 و 12 باشد.";
                }

                if (type != "field")
                    continue;

                var key = GetString(b, "fieldKey");

                if (key is null || !fieldKeys.Contains(key))
                    return $"فیلد «{key}» در این نسخه وجود ندارد.";

                if (!placed.Add(key))
                    return $"فیلد «{key}» بیش از یک‌بار در Layout آمده است.";

                var widget = GetString(b, "widget");

                if (widget is not null && !Widgets.Contains(widget))
                    return $"نوع ورودی فیلد «{key}» معتبر نیست.";

                if (widget is "select" or "radio")
                {
                    var hasOption = b.TryGetProperty("options", out var opts)
                        && opts.ValueKind == JsonValueKind.Array
                        && opts.EnumerateArray().Any(o =>
                            o.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(o.GetString()));

                    if (!hasOption)
                        return $"برای فیلد «{key}» گزینه‌ای تعریف نشده است.";
                }
            }

            if (placed.Count == 0)
                return "حداقل یک فیلد باید در Layout قرار بگیرد.";

            // شرط‌های نمایش باید به فیلدهای چیده‌شده اشاره کنند
            foreach (var b in blocks.EnumerateArray())
            {
                if (!b.TryGetProperty("showIf", out var rule) || rule.ValueKind == JsonValueKind.Null)
                    continue;

                if (rule.ValueKind != JsonValueKind.Object)
                    return "شرط نمایش یکی از المان‌ها معتبر نیست.";

                var key = GetString(rule, "key");
                var op = GetString(rule, "op");

                if (key is null || !placed.Contains(key) || op is null || !Ops.Contains(op))
                    return "شرط نمایش یکی از المان‌ها به فیلد یا عملگر نامعتبر اشاره می‌کند.";
            }
        }

        return null;
    }

    private static string? GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}
