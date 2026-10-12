using System.Globalization;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acl.Core.Admin;
using Acl.Core.Model;
using Borc.DataMapper.Application.DataSources.CreateDataSource;
using Borc.DataMapper.Application.TemplateFields.CreateTemplateField;
using Borc.DataMapper.Application.Templates.CreateTemplate;
using Borc.DataMapper.Application.Templates.SetTemplateUnits;
using Borc.DataMapper.Application.TemplateVersions.CreateTemplateVersion;
using Borc.DataMapper.Application.TemplateVersions.PublishTemplateVersion;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Web.Modules;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MReporter.Core.Execution;
using MReporter.Core.Model;
using MReporter.Core.Queries;
using MReporter.Core.Reports;
using OrgChart.Core.Admin;
using SortKey = MReporter.Core.Queries.SortKey;

namespace Mapper.Tests;

/// <summary>
/// Report queries on Mapper's records (MReporter + MapperReportData): fields read from the record JSON, filters, option
/// titles, and the same visibility as the lists — own rows, unit heads — unless a shared report grants a unit scope.
/// Chart: HQ (head "rboss") → FIN ("rfin"), HR ("rhr").
/// </summary>
public sealed partial class ReportDataTests(MapperApp app) : IClassFixture<MapperApp>, IAsyncLifetime
{
    private static readonly SemaphoreSlim Setup = new(1, 1);
    private static (long Fin, long Hr, long Boss, long Version, string Source)? _state;

    private long _fin, _hr, _boss, _version;
    private string _source = null!;

    // the fixture (one app) is shared by the tests of this class: set the data up once
    public async Task InitializeAsync()
    {
        await Setup.WaitAsync();
        try
        {
            _state ??= await CreateAsync();
            (_fin, _hr, _boss, _version, _source) = _state.Value;
        }
        finally
        {
            Setup.Release();
        }
    }

    private async Task<(long, long, long, long, string)> CreateAsync()
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;
        var chart = sp.GetRequiredService<IOrgChartAdministration>();
        ISender sender = sp.GetRequiredService<ISender>();

        await chart.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput("RDEP", "اداره"));
        await chart.CreateUnitAsync(new UnitInput("RHQ", "ستاد", "RDEP"));
        await chart.CreateUnitAsync(new UnitInput("RFIN", "مالی", "RDEP", "RHQ"));
        await chart.CreateUnitAsync(new UnitInput("RHR", "منابع انسانی", "RDEP", "RHQ"));
        await chart.CreatePositionAsync(new PositionInput("RP-HQ", "مدیر ستاد", "RHQ"));
        await chart.SetUnitManagerAsync("RHQ", "RP-HQ");
        await chart.CreatePositionAsync(new PositionInput("RP-FIN", "کارشناس مالی", "RFIN"));
        await chart.CreatePositionAsync(new PositionInput("RP-HR", "کارشناس منابع انسانی", "RHR"));

        (_fin, _hr, _boss) = (await UserAsync(sp, "rfin", "RP-FIN"), await UserAsync(sp, "rhr", "RP-HR"), await UserAsync(sp, "rboss", "RP-HQ"));

        long kinds = (await sender.Send(new CreateDataSourceCommand(null, "نوع هزینهٔ گزارش", DataSourceType.StaticList, ItemsText: "EX1 | تنخواه\nEX2 | تعمیرات"))).Data;
        long template = (await sender.Send(new CreateTemplateCommand(null, "هزینه‌های گزارش", null))).Data;
        _version = (await sender.Send(new CreateTemplateVersionCommand(template))).Data;
        await sender.Send(new CreateTemplateFieldCommand(_version, null, "شرح", FieldDataType.Text, "NVARCHAR2", true, null, 200, null, null, null, null, null, null));
        await sender.Send(new CreateTemplateFieldCommand(_version, null, "مبلغ", FieldDataType.Number, "NUMBER", true, null, null, 18, 0, null, null, null, null));
        await sender.Send(new CreateTemplateFieldCommand(_version, null, "تاریخ", FieldDataType.Date, "DATE", true, null, null, null, null, null, null, null, null));
        await sender.Send(new CreateTemplateFieldCommand(_version, null, "نوع", FieldDataType.Text, "VARCHAR2", false, null, 20, null, null, null, null, kinds, null));
        await sender.Send(new PublishTemplateVersionCommand(_version));
        await sender.Send(new SetTemplateUnitsCommand(template, ["RFIN", "RHR"]));
        _source = "mapper.records:" + (await sender.Send(new Borc.DataMapper.Application.Templates.GetTemplate.GetTemplateQuery(template))).Data!.Code;

        HttpClient fin = await app.SignInAsync("rfin", "Secret123");
        await SaveAsync(fin, "تعمیر پمپ", "45000000", "2026-09-25", "EX2");
        await SaveAsync(fin, "تنخواه دفتر", "5000000", "2026-10-05", "EX1");
        await SaveAsync(await app.SignInAsync("rhr", "Secret123"), "تعمیر کولر", "12000000", "2026-10-07", "EX2");
        return (_fin, _hr, _boss, _version, _source);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_user_queries_their_own_records_with_filters_and_titles()
    {
        var query = new ReportQuery
        {
            Columns = ["F001", "F002", "F003", "F004", "sys.unit", "sys.author"],
            Filter = new FilterGroup { Conditions = [new FilterCondition { Column = "F002", Operator = FilterOperator.GreaterThan, Values = ["1000000"] }] },
            Sort = [new SortKey("F002", Descending: true)],
        };

        ReportPage page = await AsUserAsync(_fin, sp => sp.GetRequiredService<IReportRunner>().RunAsync(RunRequest.AdHoc(_source, query), 1, 20));

        Assert.Equal(2, page.Total); // rhr's record is not theirs
        object?[] first = [.. page.Rows[0].Values];
        Assert.Equal("تعمیر پمپ", first[0]);
        Assert.Equal(45000000m, first[1]);
        Assert.Equal(new DateOnly(2026, 9, 25), first[2]);
        Assert.Equal(new ReportListValue("EX2", "تعمیرات"), first[3]);
        Assert.Equal(new ReportListValue("RFIN", "مالی"), first[4]);
        Assert.Equal("کاربر rfin", ((ReportListValue)first[5]!).Label);

        query.Filter.Conditions.Add(new FilterCondition { Column = "F001", Operator = FilterOperator.Contains, Values = ["تنخواه"] });
        page = await AsUserAsync(_fin, sp => sp.GetRequiredService<IReportRunner>().RunAsync(RunRequest.AdHoc(_source, query), 1, 20));
        Assert.Equal("تنخواه دفتر", Assert.Single(page.Rows).Values[0]);

        query.Filter.Conditions = [new FilterCondition { Column = "F003", Operator = FilterOperator.Between, Values = ["2026-10-01", "2026-10-31"] },
            new FilterCondition { Column = "F004", Operator = FilterOperator.In, Values = ["EX1"] }];
        page = await AsUserAsync(_fin, sp => sp.GetRequiredService<IReportRunner>().RunAsync(RunRequest.AdHoc(_source, query), 1, 20));
        Assert.Equal("تنخواه دفتر", Assert.Single(page.Rows).Values[0]);
    }

    [Fact]
    public async Task The_head_above_sees_every_unit_below()
    {
        ReportPage page = await AsUserAsync(_boss, sp => sp.GetRequiredService<IReportRunner>().RunAsync(RunRequest.AdHoc(_source, new ReportQuery()), 1, 20));
        Assert.Equal(3, page.Total);

        page = await AsUserAsync(_hr, sp => sp.GetRequiredService<IReportRunner>().RunAsync(RunRequest.AdHoc(_source, new ReportQuery()), 1, 20));
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task A_shared_report_may_grant_another_units_rows()
    {
        int id = await AsUserAsync(_boss, async sp =>
        {
            var admin = sp.GetRequiredService<IReportAdministration>();
            int report = await admin.CreateSharedAsync(new ReportInput("هزینه‌های منابع انسانی", null, _source, new ReportQuery { Columns = ["F001"] }));
            await admin.SetAudienceAsync(report, [new AudienceInput(AudienceKind.User, _fin.ToString(CultureInfo.InvariantCulture))], false);
            await admin.SetScopeAsync(report, ReportScopeKind.Units, [new ScopeUnitInput("RHR")]);
            return report;
        });

        ReportPage page = await AsUserAsync(_fin, sp => sp.GetRequiredService<IReportRunner>().RunAsync(RunRequest.ForReport(id), 1, 20));
        Assert.Equal("تعمیر کولر", Assert.Single(page.Rows).Values[0]); // rhr's row, not rfin's own

        // export streams the same rows
        int streamed = await AsUserAsync(_fin, async sp =>
        {
            var (_, rows) = await sp.GetRequiredService<IReportRunner>().StreamAsync(RunRequest.ForReport(id));
            int n = 0;
            await foreach (var _ in rows) n++;
            return n;
        });
        Assert.Equal(1, streamed);
    }

    [Fact]
    public async Task The_sources_are_the_templates_and_the_imports()
    {
        var sources = await AsUserAsync(_boss, sp => sp.GetRequiredService<MReporter.Core.Abstractions.IReportSourceCatalog>().GetSourcesAsync());
        var records = Assert.Single(sources, s => s.Key == _source);
        Assert.Equal(["شرح", "مبلغ", "تاریخ", "نوع"], records.Columns.Where(c => !c.IsSystem).Select(c => c.Title));
        Assert.Equal(["RFIN", "RHR"], records.DefaultAudienceUnitKeys!.Order());
        Assert.Contains(sources, s => s.Key == "mapper.imports");

        ReportPage imports = await AsUserAsync(_boss, sp => sp.GetRequiredService<IReportRunner>().RunAsync(RunRequest.AdHoc("mapper.imports", new ReportQuery()), 1, 20));
        Assert.Equal(0, imports.Total);
    }

    [Fact]
    public async Task The_records_page_filters_with_the_advanced_filter()
    {
        HttpClient fin = await app.SignInAsync("rfin", "Secret123");
        string Url(object query) => $"/DataRecords/Version?templateVersionId={_version}&q=" + Uri.EscapeDataString(JsonSerializer.Serialize(query));

        // amount > 10,000,000 and kind is "repairs": only the pump (the other repair is rhr's, not visible to rfin)
        string page = await TextAsync(await fin.GetAsync(Url(new
        {
            filter = new
            {
                conditions = new object[]
                {
                    new { column = "F002", @operator = "GreaterThan", values = new[] { "10000000" } },
                    new { column = "F004", @operator = "In", values = new[] { "EX2" } },
                },
            },
        })));
        Assert.Contains("تعمیر پمپ", page);
        Assert.DoesNotContain("تنخواه دفتر", page);
        Assert.DoesNotContain("تعمیر کولر", page);
        Assert.Contains("data-mr-filter", page);           // the filter component is on the page
        Assert.Contains("تعمیرات", page);                 // and titles the chosen option for its chip

        // the old search text still works (quick search over the text fields)
        page = await TextAsync(await fin.GetAsync($"/DataRecords/Version?templateVersionId={_version}&search=" + Uri.EscapeDataString("تنخواه")));
        Assert.Contains("تنخواه دفتر", page);
        Assert.DoesNotContain("تعمیر پمپ", page);

        // a wrong filter is reported and ignored
        page = await TextAsync(await fin.GetAsync(Url(new { filter = new { conditions = new object[] { new { column = "F002", @operator = "Contains", values = new[] { "x" } } } } })));
        Assert.Contains("فیلتر اعمال نشد", page);
        Assert.Contains("تعمیر پمپ", page);
    }

    [Fact]
    public async Task List_options_come_from_the_modules_endpoint_for_signed_in_users()
    {
        HttpClient fin = await app.SignInAsync("rfin", "Secret123");
        string url = $"/_mreporter/options?source={Uri.EscapeDataString(_source)}&column=F004&q=" + Uri.EscapeDataString("تعمیر");
        using JsonDocument options = JsonDocument.Parse(await (await fin.GetAsync(url)).Content.ReadAsStringAsync());
        var item = Assert.Single(options.RootElement.EnumerateArray());
        Assert.Equal("EX2", item.GetProperty("value").GetString());
        Assert.Equal("تعمیرات", item.GetProperty("label").GetString());

        using JsonDocument units = JsonDocument.Parse(await (await fin.GetAsync($"/_mreporter/options?source={Uri.EscapeDataString(_source)}&column=sys.unit&q=" + Uri.EscapeDataString("مالی"))).Content.ReadAsStringAsync());
        Assert.Contains(units.RootElement.EnumerateArray(), u => u.GetProperty("value").GetString() == "RFIN");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await fin.GetAsync($"/_mreporter/options?source={Uri.EscapeDataString(_source)}&column=F002")).StatusCode); // not a list
        var anonymous = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.NotEqual(System.Net.HttpStatusCode.OK, (await anonymous.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task The_imports_page_filters_with_the_advanced_filter()
    {
        HttpClient boss = await app.SignInAsync("rboss", "Secret123");
        string q = JsonSerializer.Serialize(new { filter = new { conditions = new object[] { new { column = "imp.total", @operator = "GreaterThan", values = new[] { "100" } } } } });
        HttpResponseMessage response = await boss.GetAsync("/Imports?q=" + Uri.EscapeDataString(q));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        string page = await TextAsync(response);
        Assert.Contains("data-mr-filter", page);
        Assert.DoesNotContain("فیلتر اعمال نشد", page);
    }

    [Fact]
    public async Task The_records_page_exports_the_filtered_rows_to_excel()
    {
        HttpClient boss = await app.SignInAsync("rboss", "Secret123");
        string q = JsonSerializer.Serialize(new { filter = new { conditions = new object[] { new { column = "F004", @operator = "In", values = new[] { "EX2" } } } } });

        // the button is on the page, with the filter
        string page = await TextAsync(await boss.GetAsync($"/DataRecords/Version?templateVersionId={_version}&q=" + Uri.EscapeDataString(q)));
        Assert.Contains("/DataRecords/Export?templateVersionId=" + _version, page);

        HttpResponseMessage response = await boss.GetAsync($"/DataRecords/Export?templateVersionId={_version}&q=" + Uri.EscapeDataString(q));
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(MReporter.Razor.Export.ReportExcelExporter.ContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".xlsx", response.Content.Headers.ContentDisposition?.FileNameStar);

        using var book = new ClosedXML.Excel.XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var sheet = book.Worksheet(1);
        Assert.True(sheet.RightToLeft);
        Assert.StartsWith("هزینه‌های گزارش - نسخهٔ 1", sheet.Cell("A1").GetString());
        Assert.Equal("شرط‌ها: نوع یکی از تعمیرات", sheet.Cell("A2").GetString());
        Assert.Equal(["شرح", "مبلغ", "تاریخ", "نوع", "واحد سازمانی", "ثبت‌کننده"], Enumerable.Range(1, 6).Select(c => sheet.Cell(5, c).GetString()));
        var rows = Enumerable.Range(6, 2).Select(r => (Text: sheet.Cell(r, 1).GetString(), Amount: sheet.Cell(r, 2).GetDouble(), Date: sheet.Cell(r, 3).GetString(), Kind: sheet.Cell(r, 4).GetString()))
            .OrderBy(r => r.Text).ToList();
        Assert.Equal(("تعمیر پمپ", 45000000d, "1405/07/03", "تعمیرات"), rows[0]); // both units below the head
        Assert.Equal(("تعمیر کولر", 12000000d, "1405/07/15", "تعمیرات"), rows[1]);
        Assert.True(sheet.Cell(8, 1).IsEmpty());

        // another user exports only what they see
        HttpClient hr = await app.SignInAsync("rhr", "Secret123");
        using var own = new ClosedXML.Excel.XLWorkbook(new MemoryStream(await hr.GetByteArrayAsync($"/DataRecords/Export?templateVersionId={_version}")));
        Assert.Equal("بدون شرط (همهٔ ردیف‌هایی که اجازهٔ دیدنشان را دارید)", own.Worksheet(1).Cell("A2").GetString());
        Assert.Equal("تعمیر کولر", own.Worksheet(1).Cell(6, 1).GetString());
        Assert.True(own.Worksheet(1).Cell(7, 1).IsEmpty());

        // imports
        HttpResponseMessage imports = await boss.GetAsync("/Imports/Export");
        Assert.Equal(System.Net.HttpStatusCode.OK, imports.StatusCode);
        using var importBook = new ClosedXML.Excel.XLWorkbook(new MemoryStream(await imports.Content.ReadAsByteArrayAsync()));
        Assert.Equal("ایمپورت‌ها", importBook.Worksheet(1).Cell("A1").GetString());
    }

    private static async Task<string> TextAsync(HttpResponseMessage response) => System.Net.WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    /// <summary>Runs work in a scope that looks like a request of the user (visibility loaded as the middleware does).</summary>
    private async Task<T> AsUserAsync<T>(long userId, Func<IServiceProvider, Task<T>> work)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture))], "test")),
            RequestServices = scope.ServiceProvider,
        };
        try
        {
            await scope.ServiceProvider.GetRequiredService<DataVisibility>().LoadAsync();
            return await work(scope.ServiceProvider);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    private async Task SaveAsync(HttpClient client, string text, string amount, string date, string kind)
    {
        string page = await (await client.GetAsync($"/DataRecords/Create?versionId={_version}")).Content.ReadAsStringAsync();
        using HttpRequestMessage request = new(HttpMethod.Post, "/DataRecords/Save")
        {
            Content = JsonContent.Create(new { versionId = _version, values = new Dictionary<string, string?> { ["F001"] = text, ["F002"] = amount, ["F003"] = date, ["F004"] = kind } }),
        };
        request.Headers.Add("RequestVerificationToken", Token().Match(page).Groups[1].Value);
        using JsonDocument saved = JsonDocument.Parse(await (await client.SendAsync(request)).Content.ReadAsStringAsync());
        Assert.True(saved.RootElement.GetProperty("success").GetBoolean(), saved.RootElement.ToString());
    }

    private static async Task<long> UserAsync(IServiceProvider sp, string userName, string position)
    {
        long id = await sp.GetRequiredService<Borc.Users.Services.IUserAdministration>()
            .CreateAsync(new Borc.Users.Services.UserInput(userName, "کاربر " + userName, null, false, "Secret123"));
        await sp.GetRequiredService<IOrgChartAdministration>().AssignAsync(new AssignmentInput(position, id.ToString(CultureInfo.InvariantCulture), ValidFrom: DateTime.UtcNow.AddDays(-1)));

        IRoleAdministration roles = sp.GetRequiredService<IRoleAdministration>();
        IAssignmentAdministration assignments = sp.GetRequiredService<IAssignmentAdministration>();
        ResourceOption root = (await assignments.GetResourceOptionsAsync()).Single(r => r.Key == "Mapper");
        int role = await roles.CreateRoleAsync(new RoleInput { Name = "Report-" + userName, IsActive = true });
        await roles.SetPermissionsAsync(role, root.Actions.Select(a => new PermissionChange(root.Id, a, PermissionEffect.Allow)).ToList());
        await assignments.AddUserRoleAsync(id.ToString(CultureInfo.InvariantCulture), new UserRoleInput { RoleId = role });
        return id;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex Token();
}
