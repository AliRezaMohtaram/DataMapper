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
    public async Task Dashboard_shows_the_user_and_the_module_menu()
    {
        HttpClient client = await app.AdminAsync();

        HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string html = await TextAsync(response);
        Assert.Contains("مدیر سامانه", html);
        Assert.Contains("href=\"/Users\"", html);
        Assert.Contains("href=\"/Acl/Roles\"", html);       // super admin: Acl.Admin
        Assert.Contains("href=\"/OrgChart/My\"", html);
        Assert.DoesNotContain("href=\"/OrgChart\"", html);  // no role with Mapper.OrgChart yet
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
    public async Task Org_chart_needs_the_mapper_orgchart_permission()
    {
        HttpClient client = await app.AdminAsync();

        HttpResponseMessage response = await client.GetAsync("/OrgChart");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task A_role_with_the_org_chart_permission_opens_the_chart_and_its_forms()
    {
        HttpClient client = await app.AdminAsync(); // signs in once, so Acl has synced its resources by now
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            IRoleAdministration roles = scope.ServiceProvider.GetRequiredService<IRoleAdministration>();
            IAssignmentAdministration assignments = scope.ServiceProvider.GetRequiredService<IAssignmentAdministration>();
            int resource = (await assignments.GetResourceOptionsAsync()).Single(r => r.Key == "Mapper.OrgChart").Id;
            int role = await roles.CreateRoleAsync(new RoleInput { Name = "OrgEditors", IsActive = true });
            await roles.SetPermissionsAsync(role,
            [
                new PermissionChange(resource, WellKnownActions.View, PermissionEffect.Allow),
                new PermissionChange(resource, WellKnownActions.Edit, PermissionEffect.Allow),
            ]);
            await assignments.AddUserRoleAsync("1", new UserRoleInput { RoleId = role });
        }

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
