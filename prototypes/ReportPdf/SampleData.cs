using System.Globalization;

namespace ReportPdf;

/// <summary>An expense report: rows grouped by cost center, with subtotals and a grand total.</summary>
public sealed record Expense(DateTime Date, string CostCenter, string Kind, decimal Amount, string Description, string Author);

public sealed record ReportData(string Title, string Period, string Unit, string PrintedBy, DateTime PrintedAt, IReadOnlyList<Expense> Rows)
{
    public IEnumerable<IGrouping<string, Expense>> Groups => Rows.GroupBy(r => r.CostCenter).OrderBy(g => g.Key, StringComparer.Ordinal);

    public static ReportData Create(int count)
    {
        var random = new Random(1405);
        string[] centers = ["دفتر مرکزی", "خط تولید", "آزمایشگاه", "انبار", "فناوری اطلاعات"];
        string[] kinds = ["تنخواه", "خرید مواد اولیه", "تعمیر و نگهداری", "ایاب و ذهاب", "آموزش", "پذیرایی"];
        string[] notes =
        [
            "تعمیر پمپ خط ۲ طبق درخواست PO-1402",
            "خرید ورق گالوانیزه (۲ تن) از تأمین‌کننده",
            "سرویس دوره‌ای کمپرسور Atlas Copco GA-37",
            "پذیرایی جلسهٔ هیئت‌مدیره",
            "دورهٔ آموزشی ISO 9001:2015 برای ۱۲ نفر",
            "کرایهٔ حمل بار به انبار شمارهٔ ۳",
            "تمدید لایسنس نرم‌افزار حسابداری",
        ];
        string[] people = ["رضا حسینی", "زهرا محمدی", "فاطمه موسوی", "مریم رضایی"];
        var start = new DateTime(2026, 9, 23);
        return new ReportData(
            "گزارش هزینه‌ها به تفکیک مرکز هزینه",
            "از ۱۴۰۵/۰۷/۰۱ تا ۱۴۰۵/۰۷/۳۰",
            "معاونت مالی و اداری (با زیرمجموعه)",
            "حسین کریمی",
            new DateTime(2026, 10, 12, 10, 30, 0),
            Enumerable.Range(0, count).Select(i => new Expense(
                start.AddDays(random.Next(0, 30)),
                centers[random.Next(centers.Length)],
                kinds[random.Next(kinds.Length)],
                random.Next(5, 2500) * 100_000m,
                notes[random.Next(notes.Length)],
                people[random.Next(people.Length)]))
                .OrderBy(e => e.Date).ToList());
    }
}

/// <summary>Persian digits, thousands separators and Jalali dates.</summary>
public static class Fa
{
    private static readonly PersianCalendar Calendar = new();

    public static string Digits(string text)
    {
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (chars[i] is >= '0' and <= '9') chars[i] = (char)('۰' + (chars[i] - '0'));
        return new string(chars).Replace(',', '٬');
    }

    public static string Money(decimal value) => Digits(value.ToString("#,0", CultureInfo.InvariantCulture));

    public static string Number(int value) => Digits(value.ToString(CultureInfo.InvariantCulture));

    public static string Date(DateTime d) =>
        Digits($"{Calendar.GetYear(d):0000}/{Calendar.GetMonth(d):00}/{Calendar.GetDayOfMonth(d):00}");

    public static string DateTime(DateTime d) => Date(d) + " " + Digits(d.ToString("HH:mm", CultureInfo.InvariantCulture));
}
