using System.Text.Json;
using Borc.DataMapper.Application.Abstractions.Http;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.DataSources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Borc.DataMapper.Application.DataSources.Common;

/// <summary>یک گزینه: Value = مقدار واقعی ذخیره‌شده در داده، Label = عنوان نمایشی.</summary>
public sealed record DataSourceOption(string Value, string Label);

public sealed record DataSourceOptionsResult(IReadOnlyList<DataSourceOption> Items, int Total, string? Error);

public sealed record DataSourceFindResult(DataSourceOption? Option, string? Error);

/// <summary>
/// فهرست گزینه‌های هر منبع داده (لیست دستی، فایل، قالب داخلی، API) در یک نقطه.
/// ثبت در DI: services.AddScoped&lt;DataSourceOptionService&gt;(); services.AddMemoryCache();
/// </summary>
public sealed class DataSourceOptionService
{
    /// <summary>تا این تعداد گزینه، لیست کشویی ساده؛ بیشتر از آن Combobox جستجوپذیر.</summary>
    public const int InlineLimit = 200;

    private const int MaxTemplateScan = 5_000;
    private const int MaxApiItems = 20_000;
    private static readonly TimeSpan ApiCacheTtl = TimeSpan.FromMinutes(5);

    private readonly IAppDbContext _db;
    private readonly IApiOptionFetcher _api;
    private readonly IMemoryCache _cache;

    public DataSourceOptionService(IAppDbContext db, IApiOptionFetcher api, IMemoryCache cache)
    {
        _db = db;
        _api = api;
        _cache = cache;
    }

    public async Task<DataSourceOptionsResult> SearchAsync(
        long dataSourceId,
        string? query,
        int take,
        CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 200);
        var q = string.IsNullOrWhiteSpace(query) ? null : query.Trim();

        var ds = await LoadAsync(dataSourceId, ct);

        if (ds.Error is not null)
            return Empty(ds.Error);

        var source = ds.Source!;

        switch (source.SourceType)
        {
            case DataSourceType.StaticList:
            case DataSourceType.File:
            {
                var items = _db.DataSourceItems.AsNoTracking().Where(i => i.DataSourceId == source.Id);

                if (q is not null)
                {
                    var pattern = $"%{EscapeLike(q)}%";
                    items = items.Where(i =>
                        EF.Functions.Like(i.Label, pattern, "\\") || EF.Functions.Like(i.Value, pattern, "\\"));
                }

                var total = await items.CountAsync(ct);

                var page = await items
                    .OrderBy(i => i.SortOrder)
                    .ThenBy(i => i.Id)
                    .Take(take)
                    .Select(i => new DataSourceOption(i.Value, i.Label))
                    .ToListAsync(ct);

                return new DataSourceOptionsResult(page, total, null);
            }

            case DataSourceType.Template:
            {
                var all = await LoadTemplateOptionsAsync(source, q, ct);
                return Filter(all, q, take);
            }

            case DataSourceType.Api:
            {
                var all = await LoadApiOptionsAsync(source, ct);
                return all.Error is not null ? Empty(all.Error) : Filter(all.Items, q, take);
            }

            default:
                return Empty("نوع منبع داده پشتیبانی نمی‌شود.");
        }
    }

    /// <summary>پیدا کردن یک مقدار دقیق (برای اعتبارسنجی هنگام ثبت و گرفتن عنوان مقدار ذخیره‌شده).</summary>
    public async Task<DataSourceFindResult> FindAsync(
        long dataSourceId,
        string value,
        CancellationToken ct)
    {
        var ds = await LoadAsync(dataSourceId, ct);

        if (ds.Error is not null)
            return new DataSourceFindResult(null, ds.Error);

        var source = ds.Source!;

        switch (source.SourceType)
        {
            case DataSourceType.StaticList:
            case DataSourceType.File:
            {
                var item = await _db.DataSourceItems
                    .AsNoTracking()
                    .Where(i => i.DataSourceId == source.Id && i.Value == value)
                    .Select(i => new DataSourceOption(i.Value, i.Label))
                    .FirstOrDefaultAsync(ct);

                return new DataSourceFindResult(item is not null && item.Value == value ? item : null, null);
            }

            case DataSourceType.Template:
            {
                var all = await LoadTemplateOptionsAsync(source, value, ct);

                return new DataSourceFindResult(
                    all.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.Ordinal)), null);
            }

            case DataSourceType.Api:
            {
                var all = await LoadApiOptionsAsync(source, ct);

                return all.Error is not null
                    ? new DataSourceFindResult(null, all.Error)
                    : new DataSourceFindResult(
                        all.Items.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.Ordinal)), null);
            }

            default:
                return new DataSourceFindResult(null, "نوع منبع داده پشتیبانی نمی‌شود.");
        }
    }

    // ---------------------------------------------------------------

    private async Task<(DataSource? Source, string? Error)> LoadAsync(long id, CancellationToken ct)
    {
        var source = await _db.DataSources.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);

        if (source is null)
            return (null, "منبع داده پیدا نشد.");

        if (!source.IsActive)
            return (null, $"منبع داده «{source.Name}» غیرفعال است.");

        return (source, null);
    }

    private async Task<IReadOnlyList<DataSourceOption>> LoadTemplateOptionsAsync(
        DataSource source,
        string? q,
        CancellationToken ct)
    {
        var cfg = DataSourceConfigJson.Parse<TemplateSourceConfig>(source.ConfigJson);

        if (cfg is null || string.IsNullOrWhiteSpace(cfg.ValueKey))
            return Array.Empty<DataSourceOption>();

        var records = _db.DataRecords.AsNoTracking().Where(r => r.TemplateId == cfg.TemplateId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{EscapeLike(q)}%";
            records = records.Where(r => EF.Functions.Like(r.DataJson, pattern, "\\"));
        }

        var jsons = await records
            .OrderByDescending(r => r.Id)
            .Take(MaxTemplateScan)
            .Select(r => r.DataJson)
            .ToListAsync(ct);

        var result = new Dictionary<string, DataSourceOption>(StringComparer.Ordinal);

        foreach (var json in jsons)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    continue;

                var value = ReadProp(doc.RootElement, cfg.ValueKey);

                if (value is null || result.ContainsKey(value))
                    continue;

                var label = ReadProp(doc.RootElement, cfg.DisplayKey) ?? value;
                result[value] = new DataSourceOption(value, label);
            }
            catch (JsonException)
            {
                // رکورد خراب نادیده گرفته می‌شود
            }
        }

        return result.Values.OrderBy(o => o.Label, StringComparer.CurrentCulture).ToList();
    }

    private async Task<(IReadOnlyList<DataSourceOption> Items, string? Error)> LoadApiOptionsAsync(
        DataSource source,
        CancellationToken ct)
    {
        var cfg = DataSourceConfigJson.Parse<ApiSourceConfig>(source.ConfigJson);

        if (cfg is null || string.IsNullOrWhiteSpace(cfg.Url))
            return (Array.Empty<DataSourceOption>(), "آدرس API برای این منبع داده تنظیم نشده است.");

        var key = $"dsapi:{source.Id}:{cfg.Url}:{cfg.ItemsPath}:{cfg.ValueKey}:{cfg.DisplayKey}";

        if (_cache.TryGetValue(key, out IReadOnlyList<DataSourceOption>? cached) && cached is not null)
            return (cached, null);

        var fetched = await _api.GetJsonAsync(cfg.Url, ct);

        if (!fetched.Ok || fetched.Json is null)
            return (Array.Empty<DataSourceOption>(), fetched.Error ?? "پاسخی از API دریافت نشد.");

        var parsed = ParseApi(fetched.Json, cfg);

        if (parsed.Error is not null)
            return (Array.Empty<DataSourceOption>(), parsed.Error);

        _cache.Set(key, parsed.Items, ApiCacheTtl);

        return (parsed.Items, null);
    }

    internal static (IReadOnlyList<DataSourceOption> Items, string? Error) ParseApi(string json, ApiSourceConfig cfg)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var node = doc.RootElement;

            if (!string.IsNullOrWhiteSpace(cfg.ItemsPath))
            {
                foreach (var segment in cfg.ItemsPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (node.ValueKind != JsonValueKind.Object || !TryGetIgnoreCase(node, segment.Trim(), out node))
                        return (Array.Empty<DataSourceOption>(), $"مسیر «{cfg.ItemsPath}» در پاسخ API پیدا نشد.");
                }
            }

            if (node.ValueKind != JsonValueKind.Array)
                return (Array.Empty<DataSourceOption>(), "پاسخ API در مسیر تعیین‌شده آرایه نیست.");

            var result = new Dictionary<string, DataSourceOption>(StringComparer.Ordinal);

            foreach (var item in node.EnumerateArray())
            {
                if (result.Count >= MaxApiItems)
                    break;

                string? value;
                string? label;

                if (item.ValueKind == JsonValueKind.Object)
                {
                    if (string.IsNullOrWhiteSpace(cfg.ValueKey))
                        continue;

                    value = ReadProp(item, cfg.ValueKey);
                    label = ReadProp(item, cfg.DisplayKey);
                }
                else
                {
                    value = ScalarText(item);
                    label = null;
                }

                if (value is null || result.ContainsKey(value))
                    continue;

                result[value] = new DataSourceOption(value, label ?? value);
            }

            return (result.Values.ToList(), null);
        }
        catch (JsonException)
        {
            return (Array.Empty<DataSourceOption>(), "پاسخ API JSON معتبر نیست.");
        }
    }

    private static DataSourceOptionsResult Filter(IReadOnlyList<DataSourceOption> all, string? q, int take)
    {
        if (q is null)
            return new DataSourceOptionsResult(all.Take(take).ToList(), all.Count, null);

        var needle = TextNormalizer.NormalizeHeader(q);

        var matched = all
            .Where(o => TextNormalizer.NormalizeHeader(o.Label + " " + o.Value).Contains(needle, StringComparison.Ordinal))
            .ToList();

        return new DataSourceOptionsResult(matched.Take(take).ToList(), matched.Count, null);
    }

    private static DataSourceOptionsResult Empty(string error)
        => new(Array.Empty<DataSourceOption>(), 0, error);

    private static string EscapeLike(string text)
        => text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    private static bool TryGetIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value))
            return true;

        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadProp(JsonElement obj, string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || obj.ValueKind != JsonValueKind.Object)
            return null;

        return TryGetIgnoreCase(obj, key.Trim(), out var p) ? ScalarText(p) : null;
    }

    private static string? ScalarText(JsonElement e)
    {
        var text = e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

        text = text?.Trim();

        return string.IsNullOrEmpty(text) ? null : text;
    }
}
