using System.Text.Json;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataSources.GetDataSourceLookups;
using Borc.DataMapper.Application.DataSources.ListDataSources;
using Borc.DataMapper.Domain.DataSources;

namespace Borc.DataMapper.Web.ViewModels.DataSources;

public sealed record DataSourceIndexViewModel(
    ListDataSourcesQuery Filter,
    PagedResult<DataSourceListItemDto> Result);

/// <summary>مدل مشترک فرم ایجاد و ویرایش منبع داده؛ بخش مربوط به هر نوع در ویو نشان داده می‌شود.</summary>
public sealed class DataSourceFormViewModel
{
    public long Id { get; set; }

    /// <summary>در ویرایش ارسال نمی‌شود (nullable تا Required ضمنی MVC خطا ندهد).</summary>
    public string? Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public DataSourceType SourceType { get; set; } = DataSourceType.StaticList;

    // StaticList
    public string? ItemsText { get; set; }

    // Template
    public long? TemplateId { get; set; }

    public string? ValueKey { get; set; }

    public string? DisplayKey { get; set; }

    // Api
    public string? ApiUrl { get; set; }

    public string? ApiItemsPath { get; set; }

    public string? ApiValueKey { get; set; }

    public string? ApiDisplayKey { get; set; }

    public bool IsEdit => Id > 0;

    /// <summary>داده‌ای که JavaScript فرم برای پر کردن فهرست فیلدهای قالب می‌خواند.</summary>
    public string LookupsJson { get; set; } = "{\"templates\":[]}";

    public static string BuildLookupsJson(DataSourceLookupsDto lookups)
        => JsonSerializer.Serialize(new
        {
            templates = lookups.Templates.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                fields = t.Fields.Select(f => new { key = f.Key, label = f.Label })
            })
        });
}

public static class DataSourceTypeLabels
{
    public static string ToLabel(this DataSourceType type) => type switch
    {
        DataSourceType.StaticList => "فهرست دستی",
        DataSourceType.File => "فایل Excel / CSV",
        DataSourceType.Template => "قالب داخلی",
        DataSourceType.Api => "سرویس خارجی (API)",
        _ => type.ToString()
    };

    public static string Describe(this DataSourceType type) => type switch
    {
        DataSourceType.StaticList => "گزینه‌ها را خودتان تایپ می‌کنید (مثلاً انواع سایت، وضعیت‌ها).",
        DataSourceType.File => "گزینه‌ها از ستون‌های یک فایل Excel یا CSV خوانده می‌شود.",
        DataSourceType.Template => "گزینه‌ها از رکوردهای ثبت‌شدهٔ یک قالب دیگر در همین سامانه می‌آید.",
        DataSourceType.Api => "گزینه‌ها از یک سرویس JSON خارجی خوانده می‌شود.",
        _ => string.Empty
    };
}
