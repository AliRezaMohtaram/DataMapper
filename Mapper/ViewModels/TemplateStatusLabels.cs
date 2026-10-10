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

/// <summary>مدل مشترک فرم ایجاد و ویرایش قالب (partial «_TemplateForm»).</summary>
public sealed class TemplateFormViewModel : System.ComponentModel.DataAnnotations.IValidatableObject
{
    public long Id { get; set; }

    /// <summary>فقط برای نمایش در ویرایش؛ در ایجاد خودکار ساخته می‌شود.</summary>
    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    /// <summary>مقصد پس از ذخیره/انصراف در ویرایش (فقط آدرس داخلی پذیرفته می‌شود).</summary>
    public string? ReturnUrl { get; set; }

    public bool IsEdit => Id > 0;

    /// <summary>قواعد CreateTemplateValidator؛ آن validator در pipeline اجرا نمی‌شود.</summary>
    public IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> Validate(
        System.ComponentModel.DataAnnotations.ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Name))
            yield return new("نام قالب را وارد کنید.", new[] { nameof(Name) });
        else if (Name.Length > 200)
            yield return new("نام قالب حداکثر ۲۰۰ کاراکتر است.", new[] { nameof(Name) });

        if (Description?.Length > 1000)
            yield return new("توضیحات حداکثر ۱۰۰۰ کاراکتر است.", new[] { nameof(Description) });
    }
}
