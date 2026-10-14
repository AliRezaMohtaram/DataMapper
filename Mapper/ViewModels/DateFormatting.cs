using System.Globalization;
using System.Text.RegularExpressions;

namespace Borc.DataMapper.Web.ViewModels;

public static partial class DateFormatting
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

        // text that is a Gregorian date (e.g. imported as text): shown in Jalali too (user request); stored as it is
        if (type == Borc.DataMapper.Domain.Common.FieldDataType.Text && TryGregorianText(value, out var text, out bool time))
        {
            var date = $"{Pc.GetYear(text):0000}/{Pc.GetMonth(text):00}/{Pc.GetDayOfMonth(text):00}";
            return ToPersianDigits(time ? $"{date} {text:HH:mm}" : date);
        }

        return type == Borc.DataMapper.Domain.Common.FieldDataType.Number ? ToPersianDigits(value) : value;
    }

    /// <summary>Text shown as a Jalali date (see <see cref="TryGregorianText"/>).</summary>
    public static bool IsDateText(this string value, Borc.DataMapper.Domain.Common.FieldDataType type) =>
        type == Borc.DataMapper.Domain.Common.FieldDataType.Text && TryGregorianText(value, out _, out _);

    // text that looks like a Gregorian date: 2015-10-06, 2015/10/06 (time optional), 5/27/2015 (month first unless the
    // first number is above 12), years 1900–2100 only (Jalali text such as 1405/07/22 is left alone)
    [GeneratedRegex(@"^(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})(?:[ T](\d{1,2}):(\d{2})(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:?\d{2})?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex YearFirst();

    [GeneratedRegex(@"^(\d{1,2})[-/.](\d{1,2})[-/.](\d{4})(?: (\d{1,2}):(\d{2})(?::\d{2})?(?: ?([AaPp])[Mm])?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex YearLast();

    /// <summary>A text value that is a Gregorian date (and time) as written in imported files.</summary>
    public static bool TryGregorianText(string? text, out DateTime value, out bool hasTime)
    {
        value = default;
        hasTime = false;
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 40)
            return false;
        int year, month, day, hour = 0, minute = 0;
        var m = YearFirst().Match(text);
        if (m.Success)
        {
            (year, month, day) = (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
        }
        else if ((m = YearLast().Match(text)).Success)
        {
            int a = int.Parse(m.Groups[1].Value), b = int.Parse(m.Groups[2].Value);
            (month, day) = a > 12 ? (b, a) : (a, b);
            year = int.Parse(m.Groups[3].Value);
        }
        else
        {
            return false;
        }

        if (m.Groups[4].Success)
        {
            hour = int.Parse(m.Groups[4].Value);
            minute = int.Parse(m.Groups[5].Value);
            if (m.Groups.Count > 6 && m.Groups[6].Success)
                hour = hour % 12 + (char.ToUpperInvariant(m.Groups[6].Value[0]) == 'P' ? 12 : 0);
            hasTime = true;
        }

        if (year is < 1900 or > 2100 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month) || hour > 23 || minute > 59)
            return false;
        value = new DateTime(year, month, day, hour, minute, 0);
        return true;
    }

    public static string ToPersianDigits(this string value)
        => string.Concat(value.Select(ch => ch is >= '0' and <= '9' ? (char)('۰' + (ch - '0')) : ch));

    public static string ToPersianNumber(this int value)
        => value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '٬').ToPersianDigits();
}