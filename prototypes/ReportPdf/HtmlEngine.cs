using System.Net;
using System.Text;
using Microsoft.Playwright;

namespace ReportPdf;

/// <summary>
/// Engine A: the report as HTML/CSS (the same markup a browser previews and prints), turned into PDF by headless
/// Chromium. Table headers repeat on every page by themselves (thead); the page header/footer are Chromium's templates.
/// </summary>
public static class HtmlEngine
{
    public static string BuildHtml(ReportData data, string fontsDir)
    {
        string font = Convert.ToBase64String(File.ReadAllBytes(Path.Combine(fontsDir, "Vazir.ttf")));
        var h = new StringBuilder();
        h.Append($$"""
            <!doctype html><html lang="fa" dir="rtl"><head><meta charset="utf-8"><style>
            @font-face { font-family: Vazir; src: url(data:font/ttf;base64,{{font}}) format('truetype'); }
            @page { size: A4; margin: 22mm 12mm 16mm 12mm;
              @top-right { content: "{{E(data.Title)}}"; font-family: Vazir; font-size: 8pt; color: #64748b; }
              @top-left { content: "شرکت نمونه"; font-family: Vazir; font-size: 8pt; color: #64748b; }
              @bottom-center { content: "صفحهٔ " counter(page, persian) " از " counter(pages, persian); font-family: Vazir; font-size: 8pt; color: #64748b; } }
            * { box-sizing: border-box; }
            body { font-family: Vazir, sans-serif; font-size: 9pt; color: #1f2937; margin: 0; }
            .head { display: flex; justify-content: space-between; align-items: flex-start; border-bottom: 2px solid #1d4ed8; padding-bottom: 6px; margin-bottom: 8px; }
            .logo { width: 22mm; height: 14mm; border: 1px dashed #94a3b8; border-radius: 4px; display: grid; place-items: center; color: #64748b; font-size: 8pt; }
            h1 { font-size: 15pt; margin: 0 0 2px; color: #1e3a8a; }
            .meta { color: #475569; font-size: 8.5pt; line-height: 1.7; }
            table { width: 100%; border-collapse: collapse; }
            thead { display: table-header-group; }
            th { background: #1e3a8a; color: #fff; font-weight: 700; padding: 5px 6px; text-align: right; }
            td { padding: 4px 6px; border-bottom: 1px solid #e2e8f0; vertical-align: top; }
            tr { break-inside: avoid; }
            tbody tr:nth-child(even) td { background: #f8fafc; }
            .num { text-align: left; white-space: nowrap; font-variant-numeric: tabular-nums; }
            .group td { background: #dbeafe !important; font-weight: 700; color: #1e3a8a; border-top: 1px solid #93c5fd; }
            .sub td { background: #eff6ff !important; font-weight: 700; border-bottom: 1px solid #93c5fd; }
            .total td { background: #1e3a8a !important; color: #fff; font-weight: 700; font-size: 10pt; }
            .sign { margin-top: 14mm; display: flex; justify-content: space-around; text-align: center; color: #475569; break-inside: avoid; }
            </style></head><body>
            <div class="head"><div><h1>{{E(data.Title)}}</h1>
            <div class="meta">بازه: {{E(data.Period)}} · واحد: {{E(data.Unit)}}<br>تهیه‌کننده: {{E(data.PrintedBy)}} · تاریخ چاپ: {{Fa.DateTime(data.PrintedAt)}}</div></div>
            <div class="logo">لوگو</div></div>
            <table><thead><tr><th style="width:6%">ردیف</th><th style="width:12%">تاریخ</th><th style="width:16%">نوع هزینه</th><th>شرح</th><th style="width:13%">ثبت‌کننده</th><th style="width:17%" class="num">مبلغ (ریال)</th></tr></thead><tbody>
            """);
        int n = 0;
        foreach (var g in data.Groups)
        {
            h.Append($"<tr class=\"group\"><td colspan=\"6\">مرکز هزینه: {E(g.Key)} ({Fa.Number(g.Count())} ردیف)</td></tr>");
            foreach (var r in g)
                h.Append($"<tr><td>{Fa.Number(++n)}</td><td>{Fa.Date(r.Date)}</td><td>{E(r.Kind)}</td><td>{E(Fa.Digits(r.Description))}</td><td>{E(r.Author)}</td><td class=\"num\">{Fa.Money(r.Amount)}</td></tr>");
            h.Append($"<tr class=\"sub\"><td colspan=\"5\">جمع {E(g.Key)}</td><td class=\"num\">{Fa.Money(g.Sum(r => r.Amount))}</td></tr>");
        }
        h.Append($"<tr class=\"total\"><td colspan=\"5\">جمع کل ({Fa.Number(data.Rows.Count)} ردیف)</td><td class=\"num\">{Fa.Money(data.Rows.Sum(r => r.Amount))}</td></tr>");
        h.Append("</tbody></table><div class=\"sign\"><div>تهیه‌کننده<br><br>امضا</div><div>رئیس حسابداری<br><br>امضا</div><div>معاون مالی<br><br>امضا</div></div></body></html>");
        return h.ToString();
    }

    /// <summary>
    /// What a user gets from the browser's own "Print → Save as PDF" (Chrome/Edge 131+): page header, footer and
    /// Persian page numbers come from the CSS @page margin boxes above, with the page's web font — nothing is installed
    /// on the server or the client.
    /// </summary>
    public static async Task RenderBrowserPrintAsync(ReportData data, string fontsDir, string pdfPath, string? chromiumPath)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = chromiumPath, Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(BuildHtml(data, fontsDir));
        await page.PdfAsync(new PagePdfOptions { Path = pdfPath, PrintBackground = true, PreferCSSPageSize = true });
    }

    public static async Task RenderAsync(ReportData data, string fontsDir, string pdfPath, string? chromiumPath)
    {
        string html = BuildHtml(data, fontsDir);
        File.WriteAllText(Path.ChangeExtension(pdfPath, ".html"), html);

        // Header/footer templates cannot load web fonts (neither their own nor the page's): they need fonts installed on
        // the server. Chromium fills page numbers with Latin digits; "Vazir FD" (fonts/VazirFD.ttf, a copy of Vazir whose
        // 0-9 draw Persian digits) shows them in Persian. Install fonts/*.ttf on the server for this engine.
        string style = "";
        string header = style + $"<div dir=\"rtl\" style=\"font-family:'Vazir FD';font-size:8px;color:#64748b;width:100%;padding:0 12mm;display:flex;justify-content:space-between\"><span>{E(data.Title)}</span><span>شرکت نمونه</span></div>";
        string footer = style + "<div dir=\"rtl\" style=\"font-family:'Vazir FD';font-size:8px;color:#64748b;width:100%;padding:0 12mm;text-align:center\">صفحهٔ <span class=\"pageNumber\"></span> از <span class=\"totalPages\"></span></div>";

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { ExecutablePath = chromiumPath, Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);
        await page.PdfAsync(new PagePdfOptions
        {
            Path = pdfPath,
            Format = "A4",
            PrintBackground = true,
            DisplayHeaderFooter = true,
            HeaderTemplate = header,
            FooterTemplate = footer,
            Margin = new Margin { Top = "22mm", Bottom = "16mm", Left = "12mm", Right = "12mm" },
        });
    }

    private static string E(string s) => WebUtility.HtmlEncode(s);
}
