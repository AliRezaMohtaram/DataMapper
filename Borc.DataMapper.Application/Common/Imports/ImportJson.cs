using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Borc.DataMapper.Application.Imports.Common;

/// <summary>ساخت و خواندن JSON سطرهای ایمپورت (ترتیب ستون‌ها حفظ می‌شود).</summary>
public static class ImportJson
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>{"سرستون": "مقدار", ...} — مقدارهای خالی null هستند.</summary>
    public static string BuildRaw(IReadOnlyList<string> headers, IReadOnlyList<string?> values)
    {
        using var ms = new MemoryStream();

        using (var w = new Utf8JsonWriter(ms, WriterOptions))
        {
            w.WriteStartObject();

            for (var i = 0; i < headers.Count; i++)
            {
                w.WritePropertyName(headers[i]);

                var v = i < values.Count ? values[i] : null;

                if (v is null)
                    w.WriteNullValue();
                else
                    w.WriteStringValue(v);
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>{"FieldKey": مقدار تایپ‌شده} — عدد به‌صورت عدد JSON، بقیه متن.</summary>
    public static string BuildMapped(IEnumerable<KeyValuePair<string, object?>> values)
    {
        using var ms = new MemoryStream();

        using (var w = new Utf8JsonWriter(ms, WriterOptions))
        {
            w.WriteStartObject();

            foreach (var (key, value) in values)
            {
                w.WritePropertyName(key);

                switch (value)
                {
                    case null:
                        w.WriteNullValue();
                        break;
                    case decimal d:
                        w.WriteNumberValue(d);
                        break;
                    default:
                        w.WriteStringValue(value.ToString());
                        break;
                }
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static string BuildErrors(IEnumerable<string> errors)
        => JsonSerializer.Serialize(errors, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    public static List<KeyValuePair<string, string?>> ParseRaw(string json)
    {
        var result = new List<KeyValuePair<string, string?>>();

        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var p in doc.RootElement.EnumerateObject())
        {
            string? value = p.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => p.Value.GetString(),
                _ => p.Value.GetRawText()
            };

            result.Add(new KeyValuePair<string, string?>(p.Name, value));
        }

        return result;
    }

    public static IReadOnlyList<string> ReadErrors(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>خلاصهٔ چند مقدار اول سطر نگاشت‌شده برای نمایش در جدول.</summary>
    public static string Preview(string? mappedJson, int max = 4)
    {
        if (string.IsNullOrWhiteSpace(mappedJson))
            return string.Empty;

        try
        {
            return string.Join("  |  ", ParseRaw(mappedJson)
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .Take(max)
                .Select(p => $"{p.Key}: {p.Value}"));
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}