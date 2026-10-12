using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ReportPdf;

/// <summary>
/// Engine B: the same report laid out directly in .NET with QuestPDF (no browser). Right-to-left content direction,
/// table header repeated per page, page numbers formatted with Persian digits.
/// </summary>
public static class QuestEngine
{
    private static readonly string Blue = "#1E3A8A";

    public static void Render(ReportData data, string fontsDir, string pdfPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Drawing.FontManager.RegisterFontWithCustomName("Vazir", File.OpenRead(Path.Combine(fontsDir, "Vazir.ttf")));

        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(12, Unit.Millimetre);
            page.MarginVertical(12, Unit.Millimetre);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(t => t.FontFamily("Vazir").FontSize(9).FontColor("#1F2937"));

            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(data.Title).FontSize(15).Bold().FontColor(Blue);
                        c.Item().Text($"بازه: {data.Period} · واحد: {data.Unit}").FontColor("#475569").FontSize(8.5f);
                        c.Item().Text($"تهیه‌کننده: {data.PrintedBy} · تاریخ چاپ: {Fa.DateTime(data.PrintedAt)}").FontColor("#475569").FontSize(8.5f);
                    });
                    row.ConstantItem(22, Unit.Millimetre).Height(14, Unit.Millimetre).Border(0.5f).BorderColor("#94A3B8")
                        .AlignCenter().AlignMiddle().Text("لوگو").FontSize(8).FontColor("#64748B");
                });
                col.Item().PaddingTop(4).PaddingBottom(6).LineHorizontal(2).LineColor("#1D4ED8");
            });

            page.Content().Column(col =>
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(6); c.RelativeColumn(12); c.RelativeColumn(16); c.RelativeColumn(36); c.RelativeColumn(13); c.RelativeColumn(17);
                    });

                    table.Header(h =>
                    {
                        foreach (var title in new[] { "ردیف", "تاریخ", "نوع هزینه", "شرح", "ثبت‌کننده", "مبلغ (ریال)" })
                            h.Cell().Background(Blue).Padding(5).Text(title).FontColor(Colors.White).Bold();
                    });

                    int n = 0;
                    foreach (var g in data.Groups)
                    {
                        table.Cell().ColumnSpan(6).Background("#DBEAFE").Padding(4)
                            .Text($"مرکز هزینه: {g.Key} ({Fa.Number(g.Count())} ردیف)").Bold().FontColor(Blue);
                        foreach (var r in g)
                        {
                            string bg = ++n % 2 == 0 ? "#F8FAFC" : "#FFFFFF";
                            Cell(table, bg).Text(Fa.Number(n));
                            Cell(table, bg).Text(Fa.Date(r.Date));
                            Cell(table, bg).Text(r.Kind);
                            Cell(table, bg).Text(Fa.Digits(r.Description));
                            Cell(table, bg).Text(r.Author);
                            Cell(table, bg).AlignLeft().Text(Fa.Money(r.Amount));
                        }

                        table.Cell().ColumnSpan(5).Background("#EFF6FF").Padding(4).Text($"جمع {g.Key}").Bold();
                        table.Cell().Background("#EFF6FF").Padding(4).AlignLeft().Text(Fa.Money(g.Sum(r => r.Amount))).Bold();
                    }

                    table.Cell().ColumnSpan(5).Background(Blue).Padding(5).Text($"جمع کل ({Fa.Number(data.Rows.Count)} ردیف)").Bold().FontColor(Colors.White).FontSize(10);
                    table.Cell().Background(Blue).Padding(5).AlignLeft().Text(Fa.Money(data.Rows.Sum(r => r.Amount))).Bold().FontColor(Colors.White).FontSize(10);
                });

                col.Item().PaddingTop(14, Unit.Millimetre).ShowEntire().Row(row =>
                {
                    foreach (var who in new[] { "تهیه‌کننده", "رئیس حسابداری", "معاون مالی" })
                        row.RelativeItem().AlignCenter().Text($"{who}\n\nامضا").FontColor("#475569");
                });
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor("#64748B"));
                t.Span("صفحهٔ ");
                t.CurrentPageNumber().Format(p => Fa.Number(p ?? 0));
                t.Span(" از ");
                t.TotalPages().Format(p => Fa.Number(p ?? 0));
            });
        })).GeneratePdf(pdfPath);
    }

    private static IContainer Cell(TableDescriptor table, string background) =>
        table.Cell().Background(background).BorderBottom(0.5f).BorderColor("#E2E8F0").PaddingVertical(4).PaddingHorizontal(6);
}
