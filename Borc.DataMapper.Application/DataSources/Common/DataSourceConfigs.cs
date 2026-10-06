using System.Text.Json;
using System.Text.Json.Serialization;

namespace Borc.DataMapper.Application.DataSources.Common;

/// <summary>تنظیمات منبع دادهٔ نوع Template: مقدار واقعی و عنوان نمایشی از کلید فیلدهای قالب.</summary>
public sealed record TemplateSourceConfig(long TemplateId, string ValueKey, string DisplayKey);

/// <summary>
/// تنظیمات منبع دادهٔ نوع Api. ItemsPath مسیر آرایه در پاسخ است (مثلاً data.items؛ خالی = ریشه).
/// ValueKey/DisplayKey خالی یعنی عضوهای آرایه خودشان متن یا عدد هستند.
/// </summary>
public sealed record ApiSourceConfig(string Url, string? ItemsPath, string? ValueKey, string? DisplayKey);

/// <summary>اطلاعات آخرین بارگذاری فایل (نوع File)؛ فقط برای نمایش.</summary>
public sealed record FileSourceConfig(
    string FileName,
    string ValueColumn,
    string? DisplayColumn,
    int RowCount,
    DateTime ImportedAt);

public static class DataSourceConfigJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Build<T>(T config) => JsonSerializer.Serialize(config, Options);

    public static T? Parse<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
