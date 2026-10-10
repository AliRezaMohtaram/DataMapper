using System.Globalization;

namespace Borc.DataMapper.Web.ViewModels;

public static class DateFormatting
{
    private static readonly PersianCalendar Pc = new();


    public static string ToJalali(this DateTime utc)
    {

        var d = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        return $"{Pc.GetYear(d):0000}/{Pc.GetMonth(d):00}/{Pc.GetDayOfMonth(d):00} {d:HH:mm}";

    }

    public static string ToJalali(this DateTime? utc)
        => utc.HasValue ? utc.Value.ToJalali() : "—";

    private static readonly string[] MonthNames =
        { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

    private static readonly string[] DayNames =
        { "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه" };

    /// <summary>تاریخ محلی به‌صورت «سه‌شنبه ۱۴ مهر ۱۴۰۵».</summary>
    public static string ToJalaliLong(this DateTime local)
        => ToPersianDigits($"{DayNames[(int)local.DayOfWeek]} {Pc.GetDayOfMonth(local)} {MonthNames[Pc.GetMonth(local) - 1]} {Pc.GetYear(local)}");

    /// <summary>فاصله زمانی نسبی کوتاه: «۵ دقیقه پیش»، «دیروز»، یا تاریخ شمسی.</summary>
    public static string ToRelative(this DateTime utc)
    {
        var diff = DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        if (diff.TotalMinutes < 1) return "همین حالا";
        if (diff.TotalMinutes < 60) return ToPersianDigits($"{(int)diff.TotalMinutes} دقیقه پیش");
        if (diff.TotalHours < 24) return ToPersianDigits($"{(int)diff.TotalHours} ساعت پیش");
        if (diff.TotalDays < 2) return "دیروز";
        if (diff.TotalDays < 7) return ToPersianDigits($"{(int)diff.TotalDays} روز پیش");
        return ToPersianDigits(utc.ToJalali());
    }

    /// <summary>
    /// نمایش مقدار ذخیره‌شدهٔ یک فیلد رکورد: تاریخ (ISO، همان‌طور که ذخیره شده و بدون تبدیل منطقهٔ زمانی) به شمسی،
    /// عدد و تاریخ با رقم فارسی؛ بقیه همان‌طور.
    /// </summary>
    public static string ToRecordDisplay(this string value, Borc.DataMapper.Domain.Common.FieldDataType type)
    {
        if (type is Borc.DataMapper.Domain.Common.FieldDataType.Date or Borc.DataMapper.Domain.Common.FieldDataType.DateTime
            && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            var date = $"{Pc.GetYear(d):0000}/{Pc.GetMonth(d):00}/{Pc.GetDayOfMonth(d):00}";
            return ToPersianDigits(type == Borc.DataMapper.Domain.Common.FieldDataType.DateTime ? $"{date} {d:HH:mm}" : date);
        }

        return type == Borc.DataMapper.Domain.Common.FieldDataType.Number ? ToPersianDigits(value) : value;
    }

    public static string ToPersianDigits(this string value)
        => string.Concat(value.Select(ch => ch is >= '0' and <= '9' ? (char)('۰' + (ch - '0')) : ch));

    public static string ToPersianNumber(this int value)
        => value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '٬').ToPersianDigits();
}