using Borc.DataMapper.Application.Dashboard.GetDashboard;
using Borc.DataMapper.Application.Imports.ListImportBatches;
using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Templates;
using Borc.DataMapper.Web.ViewModels.DataSources;
using Borc.DataMapper.Web.ViewModels.Imports;
using Borc.DataMapper.Web.ViewModels.Templates;

namespace Borc.DataMapper.Web.ViewModels.Dashboard;

public sealed record DashboardViewModel(DashboardDto Data)
{
    /// <summary>
    /// خط زمانی فعالیت‌ها از روی داده‌های همین صفحه ساخته می‌شود
    /// (ایمپورت‌های اخیر + تغییر قالب‌ها و منابع داده).
    /// </summary>
    public IReadOnlyList<ActivityItem> Activity(int take = 6)
    {
        var items = new List<ActivityItem>();

        foreach (var b in Data.RecentImports)
        {
            items.Add(b.Status switch
            {
                ImportBatchStatus.Imported => new ActivityItem("tone-success", "i-check",
                    $"«{b.FileName}» ثبت نهایی شد", $"{b.ValidRows.ToPersianNumber()} رکورد · {b.TemplateName}", b.CreatedAt),
                ImportBatchStatus.Validated => new ActivityItem("tone-primary", "i-play",
                    $"«{b.FileName}» آماده ثبت نهایی است", $"{b.ValidRows.ToPersianNumber()} معتبر · {b.InvalidRows.ToPersianNumber()} نامعتبر", b.CreatedAt),
                ImportBatchStatus.Validating => new ActivityItem("tone-warning", "i-clock",
                    $"اعتبارسنجی «{b.FileName}» در جریان است", b.TemplateName, b.CreatedAt),
                ImportBatchStatus.Failed => new ActivityItem("tone-danger", "i-alert",
                    $"ایمپورت «{b.FileName}» ناموفق بود", b.TemplateName, b.CreatedAt),
                _ => new ActivityItem("tone-info", "i-upload",
                    $"«{b.FileName}» بارگذاری شد", $"در انتظار تطبیق ستون‌ها · {b.TemplateName}", b.CreatedAt)
            });
        }

        foreach (var t in Data.Templates)
            items.Add(new ActivityItem("tone-violet", "i-template",
                $"قالب «{t.Name}» به‌روزرسانی شد",
                t.VersionNo is int v ? $"نسخه {v.ToPersianNumber()} · {t.Status.ToLabel()}" : t.Status.ToLabel(),
                t.ChangedAt));

        foreach (var d in Data.DataSources)
            items.Add(new ActivityItem("tone-info", DashboardUi.Icon(d.SourceType),
                $"منبع داده «{d.Name}» به‌روزرسانی شد", d.SourceType.ToLabel(), d.ChangedAt));

        return items.OrderByDescending(i => i.At).Take(take).ToList();
    }
}

public sealed record ActivityItem(string Tone, string Icon, string Title, string? Subtitle, DateTime At);

/// <summary>نگاشت وضعیت‌ها و انواع دامنه به کلاس‌ها و آیکون‌های سیستم طراحی MX.</summary>
public static class DashboardUi
{
    public static string BadgeClass(this ImportBatchStatus status) => status switch
    {
        ImportBatchStatus.Uploaded => "st-uploaded",
        ImportBatchStatus.Validating => "st-validating pulse",
        ImportBatchStatus.Validated => "st-validated",
        ImportBatchStatus.Imported => "st-imported",
        ImportBatchStatus.Failed => "st-failed",
        _ => ""
    };

    /// <summary>برچسب کوتاه برای جدول‌های فشرده.</summary>
    public static string ShortLabel(this ImportBatchStatus status) => status switch
    {
        ImportBatchStatus.Uploaded => "نیازمند تطبیق",
        ImportBatchStatus.Validating => "در حال اعتبارسنجی",
        ImportBatchStatus.Validated => "آماده ثبت",
        ImportBatchStatus.Imported => "ثبت شد",
        ImportBatchStatus.Failed => "ناموفق",
        _ => status.ToString()
    };

    /// <summary>کلیدهای فیلتر سمت کاربر (data-status) برای chipهای جدول ایمپورت.</summary>
    public static string FilterKeys(this ImportBatchListItemDto b)
    {
        var keys = new List<string>();
        if (b.Status is ImportBatchStatus.Uploaded or ImportBatchStatus.Validating or ImportBatchStatus.Validated) keys.Add("running");
        if (b.Status == ImportBatchStatus.Imported) keys.Add("imported");
        if (b.Status == ImportBatchStatus.Failed || b.InvalidRows > 0) keys.Add("issues");
        return string.Join(' ', keys);
    }

    public static string BadgeClass(this TemplateStatus status) => status switch
    {
        TemplateStatus.Active => "st-active",
        TemplateStatus.Draft => "st-draft",
        TemplateStatus.Inactive => "st-inactive",
        TemplateStatus.Archived => "st-archived",
        _ => ""
    };

    public static string Icon(DataSourceType type) => type switch
    {
        DataSourceType.StaticList => "i-list",
        DataSourceType.File => "i-sheet",
        DataSourceType.Template => "i-template",
        DataSourceType.Api => "i-cloud",
        _ => "i-db"
    };

    public static string Tone(DataSourceType type) => type switch
    {
        DataSourceType.StaticList => "tone-primary",
        DataSourceType.File => "tone-success",
        DataSourceType.Template => "tone-violet",
        DataSourceType.Api => "tone-info",
        _ => ""
    };

    /// <summary>XLSX / CSV برای آیکون فایل.</summary>
    public static string FileKind(string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.').ToUpperInvariant();
        return string.IsNullOrEmpty(ext) ? "FILE" : ext;
    }

    public static string CoverageTone(int percent) => percent switch
    {
        >= 95 => "tone-success",
        >= 75 => "tone-primary",
        >= 50 => "tone-warning",
        _ => "tone-danger"
    };
}
