using System.Text;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataSources.Common;

/// <summary>اعتبارسنجی تنظیمات هر نوع منبع داده و ساخت ConfigJson (مشترک بین ایجاد و ویرایش).</summary>
public static class DataSourceSettings
{
    public const int MaxItems = 5_000;

    public static async Task<(string? ConfigJson, string? Error)> BuildConfigAsync(
        IAppDbContext db,
        DataSourceType type,
        long? templateId,
        string? valueKey,
        string? displayKey,
        string? apiUrl,
        string? apiItemsPath,
        string? apiValueKey,
        string? apiDisplayKey,
        CancellationToken ct)
    {
        switch (type)
        {
            case DataSourceType.Template:
            {
                if (!templateId.HasValue)
                    return (null, "قالب منبع را انتخاب کنید.");

                if (string.IsNullOrWhiteSpace(valueKey) || string.IsNullOrWhiteSpace(displayKey))
                    return (null, "فیلد مقدار و فیلد نمایشی را انتخاب کنید.");

                var version = await db.TemplateVersions
                    .AsNoTracking()
                    .Where(v => v.TemplateId == templateId.Value && v.Status == TemplateVersionStatus.Published)
                    .OrderByDescending(v => v.VersionNo)
                    .FirstOrDefaultAsync(ct);

                if (version is null)
                    return (null, "قالب انتخاب‌شده نسخهٔ منتشرشده ندارد.");

                var keys = await db.TemplateFields
                    .AsNoTracking()
                    .Where(f => f.TemplateVersionId == version.Id)
                    .Select(f => f.FieldKey)
                    .ToListAsync(ct);

                var vk = keys.FirstOrDefault(k => string.Equals(k, valueKey.Trim(), StringComparison.OrdinalIgnoreCase));
                var dk = keys.FirstOrDefault(k => string.Equals(k, displayKey.Trim(), StringComparison.OrdinalIgnoreCase));

                if (vk is null || dk is null)
                    return (null, "فیلد مقدار یا نمایشی در نسخهٔ منتشرشدهٔ قالب وجود ندارد.");

                return (DataSourceConfigJson.Build(new TemplateSourceConfig(templateId.Value, vk, dk)), null);
            }

            case DataSourceType.Api:
            {
                var url = apiUrl?.Trim();

                if (string.IsNullOrEmpty(url)
                    || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    return (null, "آدرس API باید با http یا https شروع شود.");

                if (url.Length > 1000)
                    return (null, "آدرس API بیش از حد طولانی است.");

                var vk = string.IsNullOrWhiteSpace(apiValueKey) ? null : apiValueKey.Trim();
                var dk = string.IsNullOrWhiteSpace(apiDisplayKey) ? null : apiDisplayKey.Trim();

                if (vk is null && dk is not null)
                    return (null, "برای تعیین فیلد نمایشی، فیلد مقدار هم لازم است.");

                var path = string.IsNullOrWhiteSpace(apiItemsPath) ? null : apiItemsPath.Trim();

                return (DataSourceConfigJson.Build(new ApiSourceConfig(url, path, vk, dk)), null);
            }

            default:
                return (null, null);
        }
    }

    /// <summary>
    /// هر خط یک گزینه: «مقدار» یا «مقدار | عنوان» (جداکننده | یا Tab). خطوط خالی نادیده گرفته می‌شوند.
    /// </summary>
    public static (List<(string Value, string Label)> Items, string? Error) ParseItemsText(string? text)
    {
        var items = new List<(string, string)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(text))
            return (items, null);

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim('\r', ' ');

            if (line.Length == 0)
                continue;

            var sep = line.IndexOfAny(new[] { '|', '\t' });
            var value = (sep < 0 ? line : line[..sep]).Trim();
            var label = sep < 0 ? value : line[(sep + 1)..].Trim();

            if (value.Length == 0)
                return (items, $"خط «{line}»: مقدار خالی است.");

            if (value.Length > 500 || label.Length > 500)
                return (items, $"خط «{Truncate(line)}»: مقدار یا عنوان بیش از 500 نویسه است.");

            if (!seen.Add(value))
                return (items, $"مقدار «{value}» تکراری است.");

            items.Add((value, label.Length == 0 ? value : label));

            if (items.Count > MaxItems)
                return (items, $"حداکثر {MaxItems} گزینه مجاز است.");
        }

        return (items, null);
    }

    public static string ToItemsText(IEnumerable<(string Value, string Label)> items)
    {
        var sb = new StringBuilder();

        foreach (var (value, label) in items)
        {
            sb.Append(value);

            if (!string.Equals(value, label, StringComparison.Ordinal))
                sb.Append(" | ").Append(label);

            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static string Truncate(string s) => s.Length <= 40 ? s : s[..40] + "…";
}
