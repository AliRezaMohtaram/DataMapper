using System.Globalization;
using System.Net;
using System.Text.Json;
using Acl.Core.Admin;
using Acl.Core.Model;
using Borc.DataMapper.Web.Modules;
using Microsoft.Extensions.DependencyInjection;
using OrgChart.Core.Admin;

namespace Mapper.Tests;

/// <summary>
/// The reports pages (MReporter.Razor in Mapper's layout): personal reports made with the form, run with their asked
/// conditions changed at run time, printed and exported; shared reports reach their audience with the granted scope.
/// Uses the data of <see cref="ReportDataTests"/> (rfin: 2 records in RFIN, rhr: 1 in RHR, rboss heads both).
/// </summary>
public sealed partial class ReportDataTests
{
    [Fact]
    public async Task A_user_saves_a_personal_report_and_runs_it_with_its_asked_condition_changed()
    {
        HttpClient fin = await app.SignInAsync("rfin", "Secret123");

        string index = await TextAsync(await fin.GetAsync("/Reports"));
        Assert.Contains("گزارش‌های من", index);
        Assert.Contains("هزینه‌های گزارش", await TextAsync(await fin.GetAsync("/Reports/New")));

        // the form: two columns, a fixed condition and an asked one, sorted by amount
        string form = await (await fin.GetAsync("/Reports/Edit?source=" + Uri.EscapeDataString(_source))).Content.ReadAsStringAsync();
        Assert.Contains("data-mr-report-form", form);
        string filter = JsonSerializer.Serialize(new
        {
            filter = new
            {
                conditions = new object[]
                {
                    new { column = "F004", @operator = "In", values = new[] { "EX1", "EX2" } },
                    new { column = "F002", @operator = "GreaterThan", values = new[] { "1000000" }, parameter = "p2" },
                },
            },
        });

        // the designer's live preview runs the unsaved design (the asked condition with its default), biggest first
        HttpResponseMessage preview = await fin.PostAsync("/Reports/Edit?handler=Preview", new FormUrlEncodedContent(
        [
            new("Source", _source), new("Columns", "F001"), new("Columns", "F002"),
            new("Sort1", "F002"), new("Sort1Desc", "true"), new("Filter", filter),
            new("__RequestVerificationToken", Token().Match(form).Groups[1].Value),
        ]));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using (JsonDocument shown = JsonDocument.Parse(await preview.Content.ReadAsStringAsync()))
        {
            Assert.Equal("۲", shown.RootElement.GetProperty("total").GetString());
            Assert.Equal(["F001", "F002"], shown.RootElement.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("key").GetString()));
            Assert.Contains("تعمیر پمپ", shown.RootElement.GetProperty("lines")[0].GetProperty("cells")[0].GetString());
            Assert.Equal("۴۵٬۰۰۰٬۰۰۰", shown.RootElement.GetProperty("lines")[0].GetProperty("cells")[1].GetString());
        }

        HttpResponseMessage saved = await fin.PostAsync("/Reports/Edit", new FormUrlEncodedContent(
        [
            new("Source", _source), new("Title", "هزینه‌های بزرگ من"), new("Kind", "Personal"),
            new("Columns", "F001"), new("Columns", "F002"), new("Columns", "sys.author"),
            new("Sort1", "F002"), new("Sort1Desc", "true"), new("Filter", filter),
            new("__RequestVerificationToken", Token().Match(form).Groups[1].Value),
        ]));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string runUrl = saved.Headers.Location!.OriginalString;
        Assert.StartsWith("/Reports/Run?id=", runUrl);

        // as saved: both of rfin's records, biggest first
        string run = await TextAsync(await fin.GetAsync(runUrl));
        Assert.Contains("۲ ردیف", run);
        Assert.True(run.IndexOf("تعمیر پمپ", StringComparison.Ordinal) < run.IndexOf("تنخواه دفتر", StringComparison.Ordinal));
        Assert.Contains("نوع یکی از تنخواه، تعمیرات", run); // the fixed condition, described
        Assert.Contains("۴۵٬۰۰۰٬۰۰۰", run);

        // the asked condition changed on the run page: only the big one
        string q = JsonSerializer.Serialize(new { filter = new { conditions = new object[] { new { column = "F002", @operator = "GreaterThan", values = new[] { "10000000" } } } } });
        run = await TextAsync(await fin.GetAsync(runUrl + "&run=true&q=" + Uri.EscapeDataString(q)));
        Assert.Contains("۱ ردیف", run);
        Assert.DoesNotContain("تنخواه دفتر", run);

        // the way the run page sends it now: the parameter as its own short query value, repeated in the print/Excel links;
        // an emptied parameter does not narrow
        run = await TextAsync(await fin.GetAsync(runUrl + "&p2=10000000"));
        Assert.Contains("۱ ردیف", run);
        Assert.Contains("p2=10000000", run);
        Assert.DoesNotContain("q=", run[run.IndexOf("handler=Excel", StringComparison.Ordinal)..][..60]);
        Assert.Contains("۲ ردیف", await TextAsync(await fin.GetAsync(runUrl + "&p2=")));

        // print view and Excel follow the same run
        string print = await TextAsync(await fin.GetAsync(runUrl.Replace("/Run?", "/Print?") + "&run=true&q=" + Uri.EscapeDataString(q)));
        Assert.Contains("@page", print);
        Assert.Contains("counter(pages, persian)", print);
        Assert.Contains("/font/Vazir.woff2", print);
        Assert.Contains("تعمیر پمپ", print);
        Assert.DoesNotContain("تنخواه دفتر", print);

        HttpResponseMessage excel = await fin.GetAsync(runUrl.Replace("/Run?", "/Run?handler=Excel&") + "&run=true&q=" + Uri.EscapeDataString(q));
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        using var book = new ClosedXML.Excel.XLWorkbook(new MemoryStream(await excel.Content.ReadAsByteArrayAsync()));
        Assert.Equal("هزینه‌های بزرگ من", book.Worksheet(1).Cell("A1").GetString());
        Assert.Equal(["شرح", "مبلغ", "ثبت‌کننده"], Enumerable.Range(1, 3).Select(c => book.Worksheet(1).Cell(5, c).GetString()));
        Assert.Equal("تعمیر پمپ", book.Worksheet(1).Cell(6, 1).GetString());
        Assert.True(book.Worksheet(1).Cell(7, 1).IsEmpty());

        // nobody else sees or runs it
        HttpClient hr = await app.SignInAsync("rhr", "Secret123");
        Assert.DoesNotContain("هزینه‌های بزرگ من", await TextAsync(await hr.GetAsync("/Reports")));
        Assert.Equal(HttpStatusCode.NotFound, (await hr.GetAsync(runUrl)).StatusCode);
    }

    [Fact]
    public async Task A_grouped_report_shows_group_totals_on_the_page_in_the_summary_and_in_Excel()
    {
        // rboss heads both units: all three records (EX1 5,000,000; EX2 45,000,000 and 12,000,000)
        HttpClient boss = await app.SignInAsync("rboss", "Secret123");
        string form = await (await boss.GetAsync("/Reports/Edit?source=" + Uri.EscapeDataString(_source))).Content.ReadAsStringAsync();
        string grouping = JsonSerializer.Serialize(new
        {
            groupBy = new[] { new { column = "F004" } },
            totals = new[] { new { column = "F002", kind = "Sum" }, new { column = "F002", kind = "Max" } },
        });
        HttpResponseMessage saved = await boss.PostAsync("/Reports/Edit", new FormUrlEncodedContent(
        [
            new("Source", _source), new("Title", "هزینه به تفکیک نوع"), new("Kind", "Personal"),
            new("Columns", "F004"), new("Columns", "F001"), new("Columns", "F002"), new("Grouping", grouping),
            new("__RequestVerificationToken", Token().Match(form).Groups[1].Value),
        ]));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string runUrl = saved.Headers.Location!.OriginalString;

        string run = await TextAsync(await boss.GetAsync(runUrl));
        Assert.Contains("mr-grp", run);
        Assert.True(run.IndexOf("تنخواه دفتر", StringComparison.Ordinal) < run.IndexOf("تعمیر پمپ", StringComparison.Ordinal)); // EX1 first
        Assert.Contains("جمع: ۵۷٬۰۰۰٬۰۰۰\nبیشینه: ۴۵٬۰۰۰٬۰۰۰", run); // EX2's total line, one total under the other
        Assert.Contains("جمع کل (۳ ردیف)", run);
        Assert.Contains("جمع: ۶۲٬۰۰۰٬۰۰۰", run);

        // summary only: the totals without the rows
        string reportId = runUrl[(runUrl.IndexOf('=') + 1)..];
        form = await (await boss.GetAsync("/Reports/Edit?id=" + reportId)).Content.ReadAsStringAsync();
        Assert.Contains("groupBy", System.Net.WebUtility.HtmlDecode(form)); // the designer gets the saved grouping
        string summary = JsonSerializer.Serialize(new { groupBy = new[] { new { column = "F004" } }, totals = new[] { new { column = "F002", kind = "Sum" } }, summaryOnly = true,
            chart = new { kind = "Pie", series = new[] { "F002:Sum" } } });
        Assert.Equal(HttpStatusCode.Redirect, (await boss.PostAsync("/Reports/Edit", new FormUrlEncodedContent(
        [
            new("Id", reportId), new("Title", "هزینه به تفکیک نوع"),
            new("Columns", "F004"), new("Columns", "F001"), new("Columns", "F002"), new("Grouping", summary),
            new("__RequestVerificationToken", Token().Match(form).Groups[1].Value),
        ]))).StatusCode);
        run = await TextAsync(await boss.GetAsync(runUrl));
        Assert.DoesNotContain("تعمیر پمپ", run);
        Assert.Contains("تعمیرات (۲ ردیف)", run);
        Assert.Contains("جمع: ۵٬۰۰۰٬۰۰۰", run);
        Assert.Contains("<svg class=\"mr-chart\"", run); // the chart: a slice per type with its share
        Assert.Contains("تعمیرات — ۵۷٬۰۰۰٬۰۰۰ (۹۱٫۹٪)", run);
        Assert.Contains("<svg class=\"mr-chart\"", await TextAsync(await boss.GetAsync(runUrl.Replace("/Run?", "/Print?"))));

        // Excel: the group totals as bold lines, a single total as a number
        HttpResponseMessage excel = await boss.GetAsync(runUrl.Replace("/Run?", "/Run?handler=Excel&"));
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        using var book = new ClosedXML.Excel.XLWorkbook(new MemoryStream(await excel.Content.ReadAsByteArrayAsync()));
        var sheet = book.Worksheet(1);
        Assert.Equal("تنخواه (۱ ردیف)", sheet.Cell(6, 1).GetString());
        Assert.Equal(5_000_000d, sheet.Cell(6, 3).GetDouble());
        Assert.True(sheet.Cell(6, 3).Style.Font.Bold);
        Assert.Equal(57_000_000d, sheet.Cell(7, 3).GetDouble());
        Assert.Equal("جمع کل (۳ ردیف)", sheet.Cell(8, 1).GetString());
    }

    [Fact]
    public async Task A_report_page_shows_its_blocks_and_the_columns_text_settings()
    {
        HttpClient boss = await app.SignInAsync("rboss", "Secret123");
        string form = await (await boss.GetAsync("/Reports/Edit?source=" + Uri.EscapeDataString(_source))).Content.ReadAsStringAsync();
        string grouping = JsonSerializer.Serialize(new { groupBy = new[] { new { column = "F004" } }, totals = new[] { new { column = "F002", kind = "Sum" } } });
        string layout = JsonSerializer.Serialize(new
        {
            blocks = new object[]
            {
                new { id = "h", kind = "Text", span = 12, text = "گزارش ماهانهٔ هزینه", style = "Heading" },
                new { id = "k", kind = "Kpi", span = 3, value = "F002:Sum" },
                new { id = "c", kind = "Chart", span = 9, chart = "Bar", series = new[] { "F002:Sum" } },
                new { id = "t", kind = "Table", span = 12 },
            },
            columns = new Dictionary<string, object> { ["F002"] = new { title = "مبلغ هزینه", align = "Center", bold = true, decimals = 0 } },
        });
        HttpResponseMessage saved = await boss.PostAsync("/Reports/Edit", new FormUrlEncodedContent(
        [
            new("Source", _source), new("Title", "صفحهٔ هزینه‌ها"), new("Kind", "Personal"),
            new("Columns", "F004"), new("Columns", "F001"), new("Columns", "F002"), new("Grouping", grouping), new("Layout", layout),
            new("__RequestVerificationToken", Token().Match(form).Groups[1].Value),
        ]));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string runUrl = saved.Headers.Location!.OriginalString;

        string run = await TextAsync(await boss.GetAsync(runUrl));
        Assert.Contains("<h2 class=\"mr-text-h mr-al-s\">گزارش ماهانهٔ هزینه</h2>", run);
        Assert.Contains("<b class=\"mr-kpi-value\">۶۲٬۰۰۰٬۰۰۰</b>", run); // the key figure: the sum over all rows
        Assert.Contains("جمع مبلغ", run); // its caption without a title
        Assert.Contains("mr-block mr-span-9 mr-block-chart", run);
        Assert.Contains("<svg class=\"mr-chart\"", run);
        Assert.Contains(">مبلغ هزینه</th>", run); // the column's own title
        Assert.Contains("class=\"mr-al-c mr-nums mr-bold\"", run);
        Assert.True(run.IndexOf("گزارش ماهانهٔ هزینه", StringComparison.Ordinal) < run.IndexOf("mr-kpi-value", StringComparison.Ordinal)); // in the layout's order

        // the designer gets the layout back; Excel uses the column's title
        string designer = await (await boss.GetAsync("/Reports/Edit?id=" + runUrl[(runUrl.IndexOf('=') + 1)..])).Content.ReadAsStringAsync();
        Assert.Contains("\"layout\":{\"blocks\":[{\"id\":\"h\"", designer); // the designer gets the layout back
        Assert.Contains("\"F002\":{\"title\":", designer);
        using var book = new ClosedXML.Excel.XLWorkbook(new MemoryStream(await (await boss.GetAsync(runUrl.Replace("/Run?", "/Run?handler=Excel&"))).Content.ReadAsByteArrayAsync()));
        Assert.Equal("مبلغ هزینه", book.Worksheet(1).Cell(5, 3).GetString());
    }

    [Fact]
    public async Task A_chart_with_its_own_data_draws_on_an_ungrouped_report()
    {
        HttpClient boss = await app.SignInAsync("rboss", "Secret123");
        string form = await (await boss.GetAsync("/Reports/Edit?source=" + Uri.EscapeDataString(_source))).Content.ReadAsStringAsync();
        string layout = JsonSerializer.Serialize(new
        {
            blocks = new object[]
            {
                // categories from a column the table does not show, the sum of another
                new { id = "c", kind = "Chart", span = 12, chart = "Bar", category = new { column = "F004" }, order = "ValueDesc", series = new[] { "F002:Sum" } },
                new { id = "t", kind = "Table", span = 12 },
            },
        });
        HttpResponseMessage saved = await boss.PostAsync("/Reports/Edit", new FormUrlEncodedContent(
        [
            new("Source", _source), new("Title", "نمودار مستقل"), new("Kind", "Personal"),
            new("Columns", "F001"), new("Layout", layout),
            new("__RequestVerificationToken", Token().Match(form).Groups[1].Value),
        ]));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string runUrl = saved.Headers.Location!.OriginalString;

        string run = await TextAsync(await boss.GetAsync(runUrl));
        Assert.Contains("<svg class=\"mr-chart\"", run);
        Assert.Contains("— جمع مبلغ: ", run); // a bar's tooltip names its series
        Assert.DoesNotContain("یک ستون را گروه‌بندی کنید", run);
        Assert.Contains("<svg class=\"mr-chart\"", await TextAsync(await boss.GetAsync(runUrl.Replace("/Run?", "/Print?"))));
    }

    [Fact]
    public async Task A_print_layout_in_bands_is_designed_saved_and_printed()
    {
        HttpClient boss = await app.SignInAsync("rboss", "Secret123");
        string form = await (await boss.GetAsync("/Reports/Edit?source=" + Uri.EscapeDataString(_source))).Content.ReadAsStringAsync();
        string grouping = JsonSerializer.Serialize(new { groupBy = new[] { new { column = "F004" } }, totals = new[] { new { column = "F002", kind = "Sum" } } });
        HttpResponseMessage saved = await boss.PostAsync("/Reports/Edit", new FormUrlEncodedContent(
        [
            new("Source", _source), new("Title", "چاپ باندی"), new("Kind", "Personal"),
            new("Columns", "F004"), new("Columns", "F002"), new("Grouping", grouping),
            new("__RequestVerificationToken", Token().Match(form).Groups[1].Value),
        ]));
        string runUrl = saved.Headers.Location!.OriginalString;
        string id = runUrl[(runUrl.IndexOf('=') + 1)..];

        // the designer starts from the default layout (bands for the group, totals)
        string designer = await TextAsync(await boss.GetAsync("/Reports/PrintDesign?id=" + id));
        Assert.Contains("data-mr-print-designer", designer);
        Assert.Contains("\"saved\":false", designer);
        Assert.Contains("\"kind\":\"GroupFooter\"", designer);

        string layout = JsonSerializer.Serialize(new
        {
            paper = "A4",
            landscape = false,
            pageNumberPlace = "TopStart",
            bands = new object[]
            {
                new
                {
                    kind = "ReportTitle", height = 12, elements = new object[]
                    {
                        new { kind = "System", system = "Title", x = 0, y = 0, width = 100, height = 8, fontSize = 14, bold = true },
                        new { kind = "Image", image = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==", x = 150, y = 0, width = 30, height = 10 },
                    },
                },
                new { kind = "GroupHeader", level = 1, height = 7, elements = new object[] { new { kind = "Field", column = "F004", x = 0, y = 0, width = 60, height = 6 } } },
                new { kind = "Data", height = 6, elements = new object[] { new { kind = "Field", column = "F002", x = 0, y = 0, width = 40, height = 6 } } },
                new { kind = "Summary", height = 8, elements = new object[] { new { kind = "Total", value = "F002:Sum", showLabel = true, x = 0, y = 0, width = 60, height = 6 } } },
            },
        });
        HttpResponseMessage design = await boss.PostAsync("/Reports/PrintDesign?id=" + id, new FormUrlEncodedContent(
        [
            new("Id", id), new("Print", layout), new("__RequestVerificationToken", Token().Match(designer).Groups[1].Value),
        ]));
        Assert.Contains("\"ok\":true", await design.Content.ReadAsStringAsync());

        string printed = await TextAsync(await boss.GetAsync(runUrl.Replace("/Run?", "/Print?")));
        Assert.Contains("class=\"pb-paper\"", printed);
        Assert.Contains("size: A4 portrait", printed);
        Assert.Contains("data-band=\"GroupHeader1\"", printed);
        Assert.Contains("<span>جمع: ۶۲٬۰۰۰٬۰۰۰</span>", printed); // the summary's total over every row
        Assert.Contains("<span>چاپ باندی</span>", printed);
        Assert.Contains("<img alt=\"\" src=\"data:image/png;base64,iVBOR", printed); // the logo
        Assert.Contains("@top-right {", printed); // the page number where the designer put it (start = right)

        // saving the report in its designer keeps the print layout
        string edit = await (await boss.GetAsync("/Reports/Edit?id=" + id)).Content.ReadAsStringAsync();
        await boss.PostAsync("/Reports/Edit", new FormUrlEncodedContent(
        [
            new("Id", id), new("Source", _source), new("Title", "چاپ باندی"), new("Kind", "Personal"),
            new("Columns", "F004"), new("Columns", "F002"), new("Grouping", grouping),
            new("__RequestVerificationToken", Token().Match(edit).Groups[1].Value),
        ]));
        Assert.Contains("\"saved\":true", await TextAsync(await boss.GetAsync("/Reports/PrintDesign?id=" + id)));

        // removed: the print view shows the page's blocks again
        string again = await TextAsync(await boss.GetAsync("/Reports/PrintDesign?id=" + id));
        HttpResponseMessage removed = await boss.PostAsync("/Reports/PrintDesign?handler=Remove&id=" + id, new FormUrlEncodedContent(
        [
            new("__RequestVerificationToken", Token().Match(again).Groups[1].Value),
        ]));
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.DoesNotContain("pb-paper", await TextAsync(await boss.GetAsync(runUrl.Replace("/Run?", "/Print?"))));
    }

    [Fact]
    public async Task A_shared_report_reaches_its_audience_with_the_granted_scope()
    {
        // a user who may run reports but not make shared ones, in HR
        long limited;
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            IServiceProvider sp = scope.ServiceProvider;
            await sp.GetRequiredService<IOrgChartAdministration>().CreatePositionAsync(new PositionInput("RP-HR2", "کارشناس دوم منابع انسانی", "RHR"));
            limited = await sp.GetRequiredService<Borc.Users.Services.IUserAdministration>()
                .CreateAsync(new Borc.Users.Services.UserInput("rlim", "کاربر محدود", null, false, "Secret123"));
            string id = limited.ToString(CultureInfo.InvariantCulture);
            await sp.GetRequiredService<IOrgChartAdministration>().AssignAsync(new AssignmentInput("RP-HR2", id, ValidFrom: DateTime.UtcNow.AddDays(-1)));

            var roles = sp.GetRequiredService<IRoleAdministration>();
            var assignments = sp.GetRequiredService<IAssignmentAdministration>();
            var resources = await assignments.GetResourceOptionsAsync();
            int role = await roles.CreateRoleAsync(new RoleInput { Name = "Report-runner", IsActive = true });
            await roles.SetPermissionsAsync(role,
            [
                new PermissionChange(resources.Single(r => r.Key == MapperResources.Reports).Id, WellKnownActions.View, PermissionEffect.Allow),
                new PermissionChange(resources.Single(r => r.Key == MapperResources.DataRecords).Id, WellKnownActions.View, PermissionEffect.Allow),
            ]);
            await assignments.AddUserRoleAsync(id, new UserRoleInput { RoleId = role });
        }

        HttpClient limitedClient = await app.SignInAsync("rlim", "Secret123");
        Assert.DoesNotContain("آماده (اشتراکی)", await TextAsync(await limitedClient.GetAsync($"/Reports/Edit?source={Uri.EscapeDataString(_source)}"))); // personal only

        // the head makes a shared report for HR showing every row
        HttpClient boss = await app.SignInAsync("rboss", "Secret123");
        string form = await (await boss.GetAsync("/Reports/Edit?source=" + Uri.EscapeDataString(_source))).Content.ReadAsStringAsync();
        Assert.Contains("آماده (اشتراکی)", System.Net.WebUtility.HtmlDecode(form));
        HttpResponseMessage saved = await boss.PostAsync("/Reports/Edit", new FormUrlEncodedContent(
        [
            new("Source", _source), new("Title", "همهٔ هزینه‌ها برای منابع انسانی"), new("Kind", "Shared"),
            new("Columns", "F001"), new("Columns", "sys.unit"), new("IsActive", "true"), new("UseSourceAudience", "false"),
            new("AudienceUnits", "RHR"), new("ScopeKind", "All"),
            new("__RequestVerificationToken", Token().Match(form).Groups[1].Value),
        ]));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        string runUrl = saved.Headers.Location!.OriginalString;

        // rlim (in HR) sees it and gets every unit's rows; they cannot edit it and have no Excel
        string index = await TextAsync(await limitedClient.GetAsync("/Reports"));
        Assert.Contains("همهٔ هزینه‌ها برای منابع انسانی", index);
        Assert.DoesNotContain("/Reports/Edit?id=", index);
        string run = await TextAsync(await limitedClient.GetAsync(runUrl));
        Assert.Contains("۳ ردیف", run);
        Assert.Contains("همهٔ داده‌ها", run);
        Assert.DoesNotContain("handler=Excel", run);
        HttpResponseMessage denied = await limitedClient.GetAsync(runUrl.Replace("/Run?", "/Run?handler=Excel&"));
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode); // to the access-denied page
        Assert.Contains("AccessDenied", denied.Headers.Location!.OriginalString);
        string reportId = runUrl[(runUrl.IndexOf('=') + 1)..];
        Assert.Equal(HttpStatusCode.NotFound, (await limitedClient.GetAsync("/Reports/Edit?id=" + reportId)).StatusCode);

        // rfin is not in the audience — but may manage reports, so they can still run it from the managers' list
        string finIndex = await TextAsync(await (await app.SignInAsync("rfin", "Secret123")).GetAsync("/Reports"));
        Assert.Contains("همهٔ هزینه‌ها برای منابع انسانی", finIndex);

        // the grant is audited
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            var audit = scope.ServiceProvider.GetRequiredService<MReporter.EFCore.MReporterDbContext>().AuditLogs;
            Assert.Contains(audit, a => a.Operation == "ReportRunGranted" && a.ActorUserId == limited.ToString(CultureInfo.InvariantCulture));
        }
    }
}
