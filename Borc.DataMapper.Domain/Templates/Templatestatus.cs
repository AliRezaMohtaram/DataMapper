namespace Borc.DataMapper.Domain.Templates;

/// <summary>وضعیت کلی قالب. مقدارها در ستون SMALLINT ذخیره می‌شوند (پیش‌فرض دیتابیس = 1 = Draft).</summary>
public enum TemplateStatus : short
{
    Draft = 1,
    Active = 2,
    Inactive = 3,
    Archived = 4
}