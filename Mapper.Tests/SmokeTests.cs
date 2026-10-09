using System.Net;
using Acl.Core.Admin;
using Acl.Core.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Mapper.Tests;

public sealed class SmokeTests(MapperApp app) : IClassFixture<MapperApp>
{
    private static async Task<string> TextAsync(HttpResponseMessage response) => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task Anonymous_requests_go_to_the_sign_in_page()
    {
        HttpResponseMessage response = await app.CreateClient(new() { AllowAutoRedirect = false }).GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Bootstrap_role_gives_the_admin_every_mapper_page()
    {
        HttpClient client = await app.AdminAsync();

        HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = await TextAsync(response);
        Assert.Contains("مدیر سامانه", html);
        foreach (string link in new[] { "/Users", "/Acl/Roles", "/OrgChart/My", "/OrgChart", "/Templates", "/DataSources", "/MappingProfiles", "/Imports", "/DataRecords" })
        {
            Assert.Contains($"href=\"{link}\"", html);
        }

        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        RoleListItem role = Assert.Single((await scope.ServiceProvider.GetRequiredService<IRoleAdministration>().ListRolesAsync()),
            r => r.Name == Borc.DataMapper.Web.Modules.MapperAccessBootstrapper.RoleName);
        Assert.Equal(1, role.DirectUserCount);
    }

    [Fact]
    public async Task A_user_without_roles_sees_only_the_dashboard_and_is_denied_elsewhere()
    {
        HttpClient client = await NewUserAsync("plain");

        string html = await TextAsync(await client.GetAsync("/"));
        Assert.Contains("href=\"/OrgChart/My\"", html);
        foreach (string link in new[] { "/Templates", "/DataSources", "/Imports", "/DataRecords", "/OrgChart", "/Acl/Roles", "/Users", "/Imports/Upload" })
        {
            Assert.DoesNotContain($"href=\"{link}\"", html);
        }

        foreach (string path in new[] { "/Templates", "/DataSources", "/MappingProfiles", "/Imports", "/DataRecords", "/OrgChart" })
        {
            HttpResponseMessage response = await client.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location!.PathAndQuery.StartsWith("/Account/AccessDenied"),
                $"{path}: {response.StatusCode} {response.Headers.Location}");
        }
    }

    [Fact]
    public async Task View_only_role_opens_the_list_but_not_the_forms()
    {
        await GrantAsync("viewer", "DataSourceViewers", ("Mapper.DataSources", WellKnownActions.View));
        HttpClient client = await app.SignInAsync("viewer", "Secret123");

        HttpResponseMessage list = await client.GetAsync("/DataSources");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.DoesNotContain("href=\"/DataSources/Create\"", await TextAsync(list));

        HttpResponseMessage form = await client.GetAsync("/DataSources/Create");
        Assert.Equal(HttpStatusCode.Redirect, form.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", form.Headers.Location!.PathAndQuery);
    }

    /// <summary>Creates a user (password Secret123) and returns a client signed in as them.</summary>
    private async Task<HttpClient> NewUserAsync(string userName)
    {
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<Borc.Users.Services.IUserAdministration>()
                .CreateAsync(new Borc.Users.Services.UserInput(userName, "کاربر " + userName, null, false, "Secret123"));
        }

        return await app.SignInAsync(userName, "Secret123");
    }

    /// <summary>Creates the user and a role allowing the given (resource, action) pairs, and gives it to them.</summary>
    private async Task GrantAsync(string userName, string roleName, params (string Resource, string Action)[] grants)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        long id = await scope.ServiceProvider.GetRequiredService<Borc.Users.Services.IUserAdministration>()
            .CreateAsync(new Borc.Users.Services.UserInput(userName, "کاربر " + userName, null, false, "Secret123"));
        IRoleAdministration roles = scope.ServiceProvider.GetRequiredService<IRoleAdministration>();
        IAssignmentAdministration assignments = scope.ServiceProvider.GetRequiredService<IAssignmentAdministration>();
        IReadOnlyList<ResourceOption> resources = await assignments.GetResourceOptionsAsync();
        int role = await roles.CreateRoleAsync(new RoleInput { Name = roleName, IsActive = true });
        await roles.SetPermissionsAsync(role, grants
            .Select(g => new PermissionChange(resources.Single(r => r.Key == g.Resource).Id, g.Action, PermissionEffect.Allow)).ToList());
        await assignments.AddUserRoleAsync(id.ToString(System.Globalization.CultureInfo.InvariantCulture), new UserRoleInput { RoleId = role });
    }

    [Theory]
    [InlineData("/Acl/Roles")]
    [InlineData("/Acl/Users")]
    [InlineData("/Acl/Audit")]
    [InlineData("/OrgChart/My")]
    [InlineData("/Users")]
    public async Task Module_pages_render_in_the_mapper_layout(string path)
    {
        HttpClient client = await app.AdminAsync();

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = await TextAsync(response);
        Assert.Contains("class=\"sidebar\"", html);                 // Mapper's _Layout
        Assert.Contains("href=\"/Templates\"", html);             // controller links work from area pages too
    }

    [Fact]
    public async Task A_role_with_the_org_chart_permission_opens_the_chart_and_its_forms()
    {
        await GrantAsync("charter", "OrgEditors", ("Mapper.OrgChart", WellKnownActions.View), ("Mapper.OrgChart", WellKnownActions.Edit));
        HttpClient client = await app.SignInAsync("charter", "Secret123");

        string dashboard = await TextAsync(await client.GetAsync("/"));
        Assert.Contains("href=\"/OrgChart\"", dashboard);

        foreach (string path in new[] { "/OrgChart", "/OrgChart/Types", "/OrgChart/Units/Edit", "/OrgChart/Audit" })
        {
            HttpResponseMessage response = await client.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {response.StatusCode}");
            Assert.Contains("class=\"sidebar\"", await TextAsync(response));
        }
    }

    [Fact]
    public async Task Deactivating_a_user_vacates_their_positions_in_the_chart()
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<Borc.Users.Services.IUserAdministration>();
        var chart = scope.ServiceProvider.GetRequiredService<OrgChart.Core.Admin.IOrgChartAdministration>();
        var reader = scope.ServiceProvider.GetRequiredService<OrgChart.Core.Chart.IOrgChartReader>();

        long id = await users.CreateAsync(new Borc.Users.Services.UserInput("leaver", "کاربر رفتنی", null, false, "Secret123"));
        string userId = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await chart.CreateTypeAsync(OrgChart.Core.Admin.OrgTypeKind.Unit, new OrgChart.Core.Admin.OrgTypeInput("DEP-X", "اداره"));
        await chart.CreateUnitAsync(new OrgChart.Core.Admin.UnitInput("U-X", "واحد آزمایشی", "DEP-X"));
        await chart.CreatePositionAsync(new OrgChart.Core.Admin.PositionInput("P-X", "کارشناس", "U-X"));
        await chart.AssignAsync(new OrgChart.Core.Admin.AssignmentInput("P-X", userId, ValidFrom: DateTime.UtcNow.AddDays(-1)));
        Assert.NotEmpty(await reader.GetHoldersAsync(["P-X"], DateTime.UtcNow));

        await users.SetActiveAsync(id, false);

        Assert.Empty(await reader.GetHoldersAsync(["P-X"], DateTime.UtcNow.AddSeconds(1)));
    }
}
