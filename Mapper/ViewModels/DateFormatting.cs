using System.Globalization;

namespace Borc.DataMapper.Web.ViewModels;

public static class DateFormatting
{
    private static readonly PersianCalendar Pc = new();

    /// <summary>تاریخ UTC را به شمسی و ساعت محلی تبدیل می‌کند: 1405/07/11 14:30</summary>
    public static string ToJalali(this DateTime utc)
    {
        var d = DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();
        return $"{Pc.GetYear(d):0000}/{Pc.GetMonth(d):00}/{Pc.GetDayOfMonth(d):00} {d:HH:mm}";
    }

    public static string ToJalali(this DateTime? utc)
        => utc.HasValue ? utc.Value.ToJalali() : "—";
}