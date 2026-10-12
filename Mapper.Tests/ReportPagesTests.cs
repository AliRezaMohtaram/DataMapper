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
