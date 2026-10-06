namespace Borc.DataMapper.Domain.DataSources;

/// <summary>
/// نوع منبع داده برای فیلدهای انتخابی.
/// StaticList: فهرست دستی؛ File: فایل Excel/CSV بارگذاری‌شده؛
/// Template: رکوردهای ثبت‌شدهٔ یک قالب داخلی؛ Api: سرویس خارجی (JSON).
/// StaticList و File موردهایشان را در جدول DataSourceItem نگه می‌دارند.
/// پیشنهادی: نیاز به تأیید (مقدار 2 قبلاً SqlQuery بود و هیچ پیاده‌سازی نداشت).
/// </summary>
public enum DataSourceType : short
{
    StaticList = 1,
    Template = 2,
    Api = 3,
    File = 4
}
