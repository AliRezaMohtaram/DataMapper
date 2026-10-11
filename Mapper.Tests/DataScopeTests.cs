using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Acl.Core.Admin;
using Acl.Core.Model;
using Borc.DataMapper.Application.Templates.SetTemplateUnits;
using Borc.DataMapper.Application.TemplateFields.CreateTemplateField;
using Borc.DataMapper.Application.Templates.CreateTemplate;
using Borc.DataMapper.Application.TemplateVersions.CreateTemplateVersion;
using Borc.DataMapper.Application.TemplateVersions.PublishTemplateVersion;
using Borc.DataMapper.Domain.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OrgChart.Core.Admin;

namespace Mapper.Tests;

/// <summary>
/// Who enters and who sees what (user decisions): templates are visible to everyone with the page permission; only
/// members of a template's units enter records with it; a record is seen by whoever entered it, by the head of its
/// unit or of a unit above (org chart), and through Acl data-scope rules. Chart: HQ (head P-HQ = "boss") → FIN, HR.
/// "fin1" (FIN) and "hr1" (HR) have every permission on Mapper but no data-scope rule. The template allows FIN only.
/// </summary>
public sealed partial class DataScopeTests : IClassFixture<MapperApp>, IAsyncLifetime
{
    private readonly MapperApp _app;
    private long _version;

    public DataScopeTests(MapperApp app) => _app = app;

    public async Task InitializeAsync()
    {
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;
        var chart = sp.GetRequiredService<IOrgChartAdministration>();
        ISender sender = sp.GetRequiredService<ISender>();
        if ((await sp.GetRequiredService<OrgChart.Core.Chart.IOrgChartReader>().GetSnapshotAsync()).FindUnit("HQ") is not null)
        {
            _version = (await sender.Send(new Borc.DataMapper.Application.Imports.GetImportLookups.GetImportLookupsQuery(ForEntry: false))).Data!.Versions.Single().Id;
            return; // the fixture is shared by the tests of this class
        }

        await chart.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput("DEP", "اداره"));
        await chart.CreateUnitAsync(new UnitInput("HQ", "ستاد", "DEP"));
        await chart.CreateUnitAsync(new UnitInput("FIN", "مالی", "DEP", "HQ"));
        await chart.CreateUnitAsync(new UnitInput("HR", "منابع انسانی", "DEP", "HQ"));
        await chart.CreatePositionAsync(new PositionInput("P-HQ", "مدیر ستاد", "HQ"));
        await chart.SetUnitManagerAsync("HQ", "P-HQ");
        await chart.CreatePositionAsync(new PositionInput("P-FIN", "کارشناس مالی", "FIN"));
        await chart.CreatePositionAsync(new PositionInput("P-HR", "کارشناس منابع انسانی", "HR"));

        foreach ((string user, string position) in new[] { ("fin1", "P-FIN"), ("hr1", "P-HR"), ("boss", "P-HQ") })
        {
            long id = await NewUserAsync(sp, user);
            await chart.AssignAsync(new AssignmentInput(position, Id(id), ValidFrom: DateTime.UtcNow.AddDays(-1)));
            await RoleAsync(sp, "Staff-" + user, id);
        }

        long template = (await sender.Send(new CreateTemplateCommand(null, "قالب مالی", null))).Data;
        _version = (await sender.Send(new CreateTemplateVersionCommand(template))).Data;
        await sender.Send(new CreateTemplateFieldCommand(_version, null, "عنوان", FieldDataType.Text, "VARCHAR2", true, null, 100, null, null, null, null, null, null));
        await sender.Send(new PublishTemplateVersionCommand(_version));
        await sender.Send(new SetTemplateUnitsCommand(template, ["FIN"]));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Only_members_of_the_templates_units_enter_records_and_see_it_offered()
    {
        HttpClient fin1 = await _app.SignInAsync("fin1", "Secret123");
        HttpClient hr1 = await _app.SignInAsync("hr1", "Secret123");

        // Definitions are not limited by unit
        Assert.Contains("قالب مالی", await TextAsync(await hr1.GetAsync("/Templates")));

        // The template picker offers it only to FIN
        Assert.Contains("قالب مالی", await TextAsync(await fin1.SendAsync(Modal("/DataRecords/Create"))));
        Assert.DoesNotContain("قالب مالی", await TextAsync(await hr1.SendAsync(Modal("/DataRecords/Create"))));

        using JsonDocument denied = await SaveAsync(hr1, "از منابع انسانی");
        Assert.False(denied.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("واحدهای مجاز", denied.RootElement.GetProperty("message").GetString());

        using JsonDocument saved = await SaveAsync(fin1, "از مالی");
        Assert.True(saved.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Records_are_seen_by_their_author_and_the_heads_above_them()
    {
        HttpClient fin1 = await _app.SignInAsync("fin1", "Secret123");
        using (JsonDocument saved = await SaveAsync(fin1, "رکورد دیدنی"))
        {
            Assert.True(saved.RootElement.GetProperty("success").GetBoolean());
        }

        Assert.Contains("رکورد دیدنی", await TextAsync(await fin1.GetAsync($"/DataRecords/Version?templateVersionId={_version}")));

        HttpClient hr1 = await _app.SignInAsync("hr1", "Secret123");
        Assert.DoesNotContain("رکورد دیدنی", await TextAsync(await hr1.GetAsync($"/DataRecords/Version?templateVersionId={_version}")));
        Assert.DoesNotContain("data-row-href", await TextAsync(await hr1.GetAsync("/DataRecords")));

        // The head of HQ sees FIN's records and who entered them …
        HttpClient boss = await _app.SignInAsync("boss", "Secret123");
        string overview = await TextAsync(await boss.GetAsync("/DataRecords"));
        Assert.Contains("کاربر fin1", overview);
        Assert.Contains("فقط داده‌های من", overview);
        Assert.Contains("رکورد دیدنی", await TextAsync(await boss.GetAsync($"/DataRecords/Version?templateVersionId={_version}")));

        // … unless they switch to "only my data"
        boss.DefaultRequestHeaders.Add("Cookie", "mapper.view=mine");
        Assert.DoesNotContain("رکورد دیدنی", await TextAsync(await boss.GetAsync($"/DataRecords/Version?templateVersionId={_version}")));

        // No toggle for someone who only sees their own rows
        Assert.DoesNotContain("فقط داده‌های من", await TextAsync(await hr1.GetAsync("/DataRecords")));
    }

    [Fact]
    public async Task A_role_bound_to_a_unit_with_sub_units_reaches_people_in_the_sub_units()
    {
        await using (AsyncServiceScope scope = _app.Services.CreateAsyncScope())
        {
            IServiceProvider sp = scope.ServiceProvider;
            var chart = sp.GetRequiredService<IOrgChartAdministration>();
            string position = await chart.CreatePositionAsync(new PositionInput(null, "حسابدار", "FIN"));
            long user = await NewUserAsync(sp, "subunit1");
            await chart.AssignAsync(new AssignmentInput(position, Id(user), ValidFrom: DateTime.UtcNow.AddDays(-1)));

            IRoleAdministration roles = sp.GetRequiredService<IRoleAdministration>();
            IAssignmentAdministration assignments = sp.GetRequiredService<IAssignmentAdministration>();
            ResourceOption records = (await assignments.GetResourceOptionsAsync()).Single(r => r.Key == "Mapper.DataRecords");
            int role = await roles.CreateRoleAsync(new RoleInput { Name = "HqRecords", IsActive = true });
            await roles.SetPermissionsAsync(role, [new PermissionChange(records.Id, WellKnownActions.View, PermissionEffect.Allow)]);
            await assignments.AddOrgUnitRoleAsync(role, new OrgUnitRoleInput { OrgUnitKey = "HQ", IncludeSubUnits = true });
        }

        HttpClient client = await _app.SignInAsync("subunit1", "Secret123");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/DataRecords")).StatusCode);
    }

    [Fact]
    public async Task A_head_editing_a_subordinates_record_is_shown_as_the_last_editor()
    {
        HttpClient fin1 = await _app.SignInAsync("fin1", "Secret123");
        string detailUrl;
        using (JsonDocument saved = await SaveAsync(fin1, "رکورد برای ویرایش"))
        {
            detailUrl = saved.RootElement.GetProperty("redirect").GetString()!;
        }
        long id = long.Parse(detailUrl[(detailUrl.LastIndexOf('/') + 1)..], CultureInfo.InvariantCulture);

        HttpClient boss = await _app.SignInAsync("boss", "Secret123");
        using (JsonDocument edited = await SaveAsync(boss, "ویرایش‌شده توسط مدیر", id))
        {
            Assert.True(edited.RootElement.GetProperty("success").GetBoolean());
        }

        string detail = await TextAsync(await boss.GetAsync(detailUrl));
        Assert.Matches("ثبت‌کننده</dt><dd>کاربر fin1</dd>", detail);
        Assert.Matches("آخرین ویرایش‌کننده</dt><dd>کاربر boss</dd>", detail);

        // the version table names the editor under the author
        Assert.Contains("ویرایش: کاربر boss", await TextAsync(await boss.GetAsync($"/DataRecords/Version?templateVersionId={_version}")));
    }

    private static HttpRequestMessage Modal(string url) => new(HttpMethod.Get, url) { Headers = { { "X-MX-Modal", "1" } } };

    /// <summary>Saves a record through the form's JSON endpoint, as record-form.js does.</summary>
    private async Task<JsonDocument> SaveAsync(HttpClient client, string title, long? recordId = null)
    {
        string page = await (await client.GetAsync($"/DataRecords/Create?versionId={_version}")).Content.ReadAsStringAsync();
        string token = Token().Match(page).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "no record form");
        using HttpRequestMessage request = new(HttpMethod.Post, "/DataRecords/Save")
        {
            Content = JsonContent.Create(new { versionId = _version, recordId, values = new Dictionary<string, string?> { ["F001"] = title } }),
        };
        request.Headers.Add("RequestVerificationToken", token);
        HttpResponseMessage response = await client.SendAsync(request);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static async Task<long> NewUserAsync(IServiceProvider sp, string userName) =>
        await sp.GetRequiredService<Borc.Users.Services.IUserAdministration>()
            .CreateAsync(new Borc.Users.Services.UserInput(userName, "کاربر " + userName, null, false, "Secret123"));

    /// <summary>A role with every action on Mapper (no data-scope rule), given to the user.</summary>
    private static async Task RoleAsync(IServiceProvider sp, string name, long userId)
    {
        IRoleAdministration roles = sp.GetRequiredService<IRoleAdministration>();
        IAssignmentAdministration assignments = sp.GetRequiredService<IAssignmentAdministration>();
        ResourceOption root = (await assignments.GetResourceOptionsAsync()).Single(r => r.Key == "Mapper");
        int role = await roles.CreateRoleAsync(new RoleInput { Name = name, IsActive = true });
        await roles.SetPermissionsAsync(role, root.Actions.Select(a => new PermissionChange(root.Id, a, PermissionEffect.Allow)).ToList());
        await assignments.AddUserRoleAsync(Id(userId), new UserRoleInput { RoleId = role });
    }

    private static async Task<string> TextAsync(HttpResponseMessage response) => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex Token();
}
