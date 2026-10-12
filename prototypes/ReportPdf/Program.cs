using System.Diagnostics;
using ReportPdf;

// dotnet run -- [rows] [output folder] [chromium executable (optional: Playwright's own when omitted)]
int rows = args.Length > 0 ? int.Parse(args[0]) : 150;
string outDir = Path.GetFullPath(args.Length > 1 ? args[1] : "output");
string? chromium = args.Length > 2 ? args[2] : null;
string fonts = Path.Combine(AppContext.BaseDirectory, "fonts");
Directory.CreateDirectory(outDir);

var data = ReportData.Create(rows);

var watch = Stopwatch.StartNew();
await HtmlEngine.RenderAsync(data, fonts, Path.Combine(outDir, $"report-chromium-{rows}.pdf"), chromium);
Console.WriteLine($"Chromium: {watch.Elapsed.TotalSeconds:0.00}s");

watch.Restart();
await HtmlEngine.RenderBrowserPrintAsync(data, fonts, Path.Combine(outDir, $"report-browser-print-{rows}.pdf"), chromium);
Console.WriteLine($"Browser print (CSS page boxes): {watch.Elapsed.TotalSeconds:0.00}s");

watch.Restart();
QuestEngine.Render(data, fonts, Path.Combine(outDir, $"report-questpdf-{rows}.pdf"));
Console.WriteLine($"QuestPDF: {watch.Elapsed.TotalSeconds:0.00}s");
