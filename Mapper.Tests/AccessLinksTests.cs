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

    [GeneratedRegex("href=\"(/[^\"#]*)\"")]
    private static partial Regex Href();
}
