using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;

namespace Borc.DataMapper.Web.ViewModels.Templates;

public static class TemplateStatusLabels
{
    public static string ToLabel(this TemplateStatus status) => status switch
    {
        TemplateStatus.Draft => "پیش‌نویس",
        TemplateStatus.Active => "فعال",
        TemplateStatus.Inactive => "غیرفعال",
        TemplateStatus.Archived => "بایگانی‌شده",
        _ => status.ToString()
    };

    /// <summary>برچسب دکمه تغییر وضعیت به مقدار هدف.</summary>
    public static string ToActionLabel(this TemplateStatus target) => target switch
    {
        TemplateStatus.Draft => "بازگشت به پیش‌نویس",
        TemplateStatus.Active => "فعال‌سازی",
        TemplateStatus.Inactive => "غیرفعال‌سازی",
        TemplateStatus.Archived => "بایگانی",
        _ => target.ToString()
    };

    public static string ToLabel(this TemplateVersionStatus status) => status switch
    {
        TemplateVersionStatus.Draft => "پیش‌نویس",
        TemplateVersionStatus.Published => "منتشرشده",
        TemplateVersionStatus.Archived => "بایگانی‌شده",
        _ => status.ToString()
    };

    public static string ToLabel(this FieldDataType type) => type switch
    {
        FieldDataType.Text => "متن",
        FieldDataType.Number => "عدد",
        FieldDataType.Date => "تاریخ",
        FieldDataType.DateTime => "تاریخ و زمان",
        FieldDataType.Any => "هر نوع",
        _ => type.ToString()
    };
}