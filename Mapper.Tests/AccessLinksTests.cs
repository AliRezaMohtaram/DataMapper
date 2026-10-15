using System.Net;
using System.Text.RegularExpressions;
using Acl.Core.Admin;
using Acl.Core.Model;
using Microsoft.Extensions.DependencyInjection;

namespace Mapper.Tests;

/// <summary>
/// User decision: a user never sees a link or button they cannot use ("access denied" must not be reachable by
/// clicking). Crawls every page a restricted user can reach and requests every link on it.
/// </summary>
public sealed partial class AccessLinksTests(MapperApp app) : IClassFixture<MapperApp>
{
    [Theory]
    [InlineData("viewer-records", "Mapper.DataRecords", new[] { WellKnownActions.View })]
    [InlineData("viewer-templates", "Mapper.Templates", new[] { WellKnownActions.View })]
    [InlineData("creator-imports", "Mapper.Imports", new[] { WellKnownActions.View, WellKnownActions.Create })]
    [InlineData("no-role", null, new string[0])]
    public async Task No_visible_link_leads_to_access_denied(string userName, string? resource, string[] actions)
    {
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            IServiceProvider sp = scope.ServiceProvider;
            long id = await sp.GetRequiredService<Borc.Users.Services.IUserAdministration>()
                .CreateAsync(new Borc.Users.Services.UserInput(userName, "کاربر " + userName, null, false, "Secret123"));
            if (resource is not null)
            {
                IRoleAdministration roles = sp.GetRequiredService<IRoleAdministration>();
                IAssignmentAdministration assignments = sp.GetRequiredService<IAssignmentAdministration>();
                ResourceOption option = (await assignments.GetResourceOptionsAsync()).Single(r => r.Key == resource);
                int role = await roles.CreateRoleAsync(new RoleInput { Name = "Only-" + userName, IsActive = true });
                await roles.SetPermissionsAsync(role, actions.Select(a => new PermissionChange(option.Id, a, PermissionEffect.Allow)).ToList());
                await assignments.AddUserRoleAsync(id.ToString(System.Globalization.CultureInfo.InvariantCulture), new UserRoleInput { RoleId = role });
            }
        }

        HttpClient client = await app.SignInAsync(userName, "Secret123");
        Queue<string> pages = new(["/"]);
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase) { "/" };
        List<string> denied = [];

        while (pages.TryDequeue(out string? page) && seen.Count < 200)
        {
            HttpResponseMessage response = await client.GetAsync(page);
            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                if (response.Headers.Location!.OriginalString.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase))
                {
                    denied.Add(page);
                }

                continue;
            }

            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "text/html")
            {
                continue;
            }

            string html = await response.Content.ReadAsStringAsync();
            foreach (Match link in Href().Matches(html))
            {
                string url = WebUtility.HtmlDecode(link.Groups[1].Value);
                if (url.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase) || url.StartsWith("/_content/", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) || url.StartsWith("/css/", StringComparison.OrdinalIgnoreCase)
                    || url.StartsWith("/js/", StringComparison.OrdinalIgnoreCase) || url.Contains("/Download/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (seen.Add(url))
                {
                    pages.Enqueue(url);
                }
            }
        }

        Assert.True(denied.Count == 0, $"{userName} sees links to pages they may not open: {string.Join(", ", denied)}");
    }

    [Fact]
    public async Task The_dashboard_lists_only_the_templates_and_data_sources_the_user_may_see()
    {
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            IServiceProvider sp = scope.ServiceProvider;
            MediatR.ISender sender = sp.GetRequiredService<MediatR.ISender>();
            await sender.Send(new Borc.DataMapper.Application.Templates.CreateTemplate.CreateTemplateCommand(null, "قالب داشبورد", null));
            await sender.Send(new Borc.DataMapper.Application.DataSources.CreateDataSource.CreateDataSourceCommand(
                null, "منبع داشبورد", Borc.DataMapper.Domain.DataSources.DataSourceType.StaticList, ItemsText: "A | الف"));

            var users = sp.GetRequiredService<Borc.Users.Services.IUserAdministration>();
            IRoleAdministration roles = sp.GetRequiredService<IRoleAdministration>();
            IAssignmentAdministration assignments = sp.GetRequiredService<IAssignmentAdministration>();
            var options = await assignments.GetResourceOptionsAsync();
            await users.CreateAsync(new Borc.Users.Services.UserInput("dash-plain", "کاربر ساده", null, false, "Secret123"));
            long designer = await users.CreateAsync(new Borc.Users.Services.UserInput("dash-designer", "طراح", null, false, "Secret123"));
            int role = await roles.CreateRoleAsync(new RoleInput { Name = "Dash-designer", IsActive = true });
            await roles.SetPermissionsAsync(role, new[] { "Mapper.Templates", "Mapper.DataSources" }
                .Select(key => new PermissionChange(options.Single(r => r.Key == key).Id, WellKnownActions.View, PermissionEffect.Allow)).ToList());
            await assignments.AddUserRoleAsync(designer.ToString(System.Globalization.CultureInfo.InvariantCulture), new UserRoleInput { RoleId = role });
        }

        // no permission on templates or data sources: neither is listed, nor in the activity
        string mine = WebUtility.HtmlDecode(await (await (await app.SignInAsync("dash-plain", "Secret123")).GetAsync("/")).Content.ReadAsStringAsync());
        Assert.DoesNotContain("قالب داشبورد", mine);
        Assert.DoesNotContain("منبع داشبورد", mine);
        Assert.DoesNotContain(">منابع داده</div>", mine); // the data sources card is not there at all

        string theirs = WebUtility.HtmlDecode(await (await (await app.SignInAsync("dash-designer", "Secret123")).GetAsync("/")).Content.ReadAsStringAsync());
        Assert.Contains("قالب داشبورد", theirs);
        Assert.Contains("منبع داشبورد", theirs);
    }

    [GeneratedRegex("href=\"(/[^\"#]*)\"")]
    private static partial Regex Href();
}
