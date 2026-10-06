using System.Globalization;
using FluentValidation;

namespace Borc.DataMapper.Application.Common.Validation;

/// <summary>
/// پیام‌های پیش‌فرض FluentValidation به فارسی و نام فارسی ویژگی‌ها در پیام‌ها
/// (مثلاً «'نام' نباید خالی باشد.» به‌جای «'Name' must not be empty.»).
/// </summary>
public static class ValidationMessages
{
    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Code"] = "کد",
        ["Name"] = "نام",
        ["Description"] = "توضیحات",
        ["FieldKey"] = "کلید فیلد",
        ["Label"] = "برچسب",
        ["DataType"] = "نوع داده",
        ["DbType"] = "نوع در مقصد",
        ["SortOrder"] = "ترتیب",
        ["Alias"] = "نام مستعار",
        ["SourceColumn"] = "عنوان ستون",
        ["SourceType"] = "نوع منبع",
        ["TemplateVersionId"] = "نسخهٔ قالب",
        ["TargetFieldId"] = "فیلد مقصد",
        ["MappingProfileId"] = "پروفایل نگاشت",
        ["TemplateFieldId"] = "فیلد",
        ["DataSourceId"] = "منبع داده",
        ["Values"] = "مقادیر",
        ["Token"] = "فایل بارگذاری‌شده",
        ["ValueColumnIndex"] = "ستون مقدار",
        ["Id"] = "شناسه"
    };

    private static bool _configured;

    public static void Configure()
    {
        if (_configured) return;
        _configured = true;

        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("fa");
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
            member is not null && DisplayNames.TryGetValue(member.Name, out var name) ? name : null;
    }
}
