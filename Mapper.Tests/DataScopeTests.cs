using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Acl.Core.Admin;
using Acl.Core.Model;
using Borc.DataMapper.Domain.Templates;
using Borc.DataMapper.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrgChart.Core.Admin;

namespace Mapper.Tests;

/// <summary>
/// Rows are filtered by org unit with Acl's data scope: HQ → FIN, HQ → HR. "fin1" (FIN, scope OrgUnit) sees FIN and
/// public rows; "boss" (HQ, scope OrgUnitAndChildren) sees HQ, FIN and HR; public (no unit) rows are seen by all.
/// </summary>
public sealed partial class DataScopeTests : IClassFixture<MapperApp>, IAsyncLifetime
{
    private readonly MapperApp _app;
    private long _finTemplate;
    private long _hrTemplate;

    public DataScopeTests(MapperApp app) => _app = app;

    public async Task InitializeAsync()
    {
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        IServiceProvider sp = scope.ServiceProvider;
        var chart = sp.GetRequiredService<IOrgChartAdministration>();
        if ((await sp.GetRequiredService<OrgChart.Core.Chart.IOrgChartReader>().GetSnapshotAsync()).FindUnit("HQ") is not null)
        {
            await LoadIdsAsync(sp);
            return; // the fixture is shared by the tests of this class
        }

        await chart.CreateTypeAsync(OrgTypeKind.Unit, new OrgTypeInput("DEP", "اداره"));
        await chart.CreateUnitAsync(new UnitInput("HQ", "ستاد", "DEP"));
        await chart.CreateUnitAsync(new UnitInput("FIN", "مالی", "DEP", "HQ"));
        await chart.CreateUnitAsync(new UnitInput("HR", "منابع انسانی", "DEP", "HQ"));
        await chart.CreatePositionAsync(new PositionInput("P-HQ", "مدیر", "HQ"));
        await chart.CreatePositionAsync(new PositionInput("P-FIN", "کارشناس مالی", "FIN"));

        long fin1 = await NewUserAsync(sp, "fin1");
        long boss = await NewUserAsync(sp, "boss");
        await chart.AssignAsync(new AssignmentInput("P-FIN", Id(fin1), ValidFrom: DateTime.UtcNow.AddDays(-1)));
        await chart.AssignAsync(new AssignmentInput("P-HQ", Id(boss), ValidFrom: DateTime.UtcNow.AddDays(-1)));
        await RoleAsync(sp, "FinStaff", DataScopeType.OrgUnit, fin1);
        await RoleAsync(sp, "Managers", DataScopeType.OrgUnitAndChildren, boss);

        BorcDataMapperDbContext db = sp.GetRequiredService<BorcDataMapperDbContext>();
        foreach ((string code, string? unit) in new[] { ("T-FIN", "FIN"), ("T-HR", "HR"), ("T-HQ", "HQ"), ("T-PUB", (string?)null) })
        {
            Template t = Template.Create(code, "قالب " + code);
            t.AssignOrgUnit(unit);
            db.Templates.Add(t);
        }

        await db.SaveChangesAsync();
        await LoadIdsAsync(sp);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Unit_scope_shows_own_unit_and_public_rows()
    {
        HttpClient client = await _app.SignInAsync("fin1", "Secret123");

        string list = await TextAsync(await client.GetAsync("/Templates"));

        Assert.Contains("T-FIN", list);
        Assert.Contains("T-PUB", list);
        Assert.DoesNotContain("T-HR", list);
        Assert.DoesNotContain("T-HQ", list);
        HttpResponseMessage hidden = await client.GetAsync($"/Templates/Detail/{_hrTemplate}");
        Assert.NotEqual(HttpStatusCode.OK, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Templates/Detail/{_finTemplate}")).StatusCode);
    }

    [Fact]
    public async Task Unit_and_children_scope_shows_the_sub_units()
    {
        HttpClient client = await _app.SignInAsync("boss", "Secret123");

        string list = await TextAsync(await client.GetAsync("/Templates"));

        foreach (string code in new[] { "T-FIN", "T-HR", "T-HQ", "T-PUB" })
        {
            Assert.Contains(code, list);
        }
    }

    [Fact]
    public async Task New_rows_get_the_chosen_unit_and_other_units_are_refused()
    {
        HttpClient client = await _app.SignInAsync("fin1", "Secret123");

        string form = await TextAsync(await client.GetAsync("/Templates/Create"));
        Assert.Contains("name=\"OrgUnitKey\"", form);
        Assert.DoesNotContain("value=\"HR\"", form);

        HttpResponseMessage refused = await SubmitAsync(client, "/Templates/Create", [new("Code", "T-NEW1"), new("Name", "x"), new("OrgUnitKey", "HR")]);
        Assert.Contains("واحد سازمانی انتخاب‌شده مجاز نیست", await TextAsync(refused));

        HttpResponseMessage created = await SubmitAsync(client, "/Templates/Create", [new("Code", "T-NEW2"), new("Name", "y"), new("OrgUnitKey", "FIN")]);
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Equal("FIN", await UnitOfAsync("T-NEW2"));
        Assert.Null(await UnitOfAsync("T-NEW1"));
    }

    [Fact]
    public async Task Change_unit_moves_a_row_within_the_users_choices()
    {
        HttpClient boss = await _app.SignInAsync("boss", "Secret123");
        long id = await IdOfAsync("T-HQ");

        HttpResponseMessage moved = await SubmitAsync(boss, $"/OrgUnits/Change?kind=Template&id={id}", [new("orgUnitKey", "HR")]);
        Assert.Equal(HttpStatusCode.Redirect, moved.StatusCode);
        Assert.Equal("HR", await UnitOfAsync("T-HQ"));

        HttpClient fin1 = await _app.SignInAsync("fin1", "Secret123");
        Assert.NotEqual(HttpStatusCode.OK, (await fin1.GetAsync($"/OrgUnits/Change?kind=Template&id={id}")).StatusCode);
        await SetUnitAsync("T-HQ", "HQ");
    }

    private async Task LoadIdsAsync(IServiceProvider sp)
    {
        _finTemplate = await IdOfAsync("T-FIN");
        _hrTemplate = await IdOfAsync("T-HR");
    }

    private async Task<long> IdOfAsync(string code)
    {
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BorcDataMapperDbContext>().Templates.Where(t => t.Code == code).Select(t => t.Id).SingleAsync();
    }

    private async Task<string?> UnitOfAsync(string code)
    {
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<BorcDataMapperDbContext>().Templates.Where(t => t.Code == code)
            .Select(t => t.OrgUnitKey).SingleOrDefaultAsync();
    }

    private async Task SetUnitAsync(string code, string unit)
    {
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
        BorcDataMapperDbContext db = scope.ServiceProvider.GetRequiredService<BorcDataMapperDbContext>();
        (await db.Templates.SingleAsync(t => t.Code == code)).AssignOrgUnit(unit);
        await db.SaveChangesAsync();
    }

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static async Task<long> NewUserAsync(IServiceProvider sp, string userName) =>
        await sp.GetRequiredService<Borc.Users.Services.IUserAdministration>()
            .CreateAsync(new Borc.Users.Services.UserInput(userName, "کاربر " + userName, null, false, "Secret123"));

    /// <summary>A role with every action on Mapper and one data scope rule there, given to the user.</summary>
    private static async Task RoleAsync(IServiceProvider sp, string name, DataScopeType scope, long userId)
    {
        IRoleAdministration roles = sp.GetRequiredService<IRoleAdministration>();
        IAssignmentAdministration assignments = sp.GetRequiredService<IAssignmentAdministration>();
        ResourceOption root = (await assignments.GetResourceOptionsAsync()).Single(r => r.Key == "Mapper");
        int role = await roles.CreateRoleAsync(new RoleInput { Name = name, IsActive = true });
        await roles.SetPermissionsAsync(role, root.Actions.Select(a => new PermissionChange(root.Id, a, PermissionEffect.Allow)).ToList());
        await sp.GetRequiredService<IDataScopeAdministration>().AddRoleRuleAsync(role, new DataScopeRuleInput { ResourceId = root.Id, ScopeType = scope });
        await assignments.AddUserRoleAsync(Id(userId), new UserRoleInput { RoleId = role });
    }

    private static async Task<string> TextAsync(HttpResponseMessage response) => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    private static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, string url, IEnumerable<KeyValuePair<string, string>> fields)
    {
        string html = await (await client.GetAsync(url)).Content.ReadAsStringAsync();
        string token = Token().Match(html).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), $"no form at {url}");
        return await client.PostAsync(url, new FormUrlEncodedContent(fields.Append(new("__RequestVerificationToken", token))));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex Token();
}
