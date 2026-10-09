using System.Net;
using System.Text.RegularExpressions;

using Borc.Users.Persistence;
using Borc.Users.Web;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Borc.Users.Tests;

/// <summary>A host like DataMapper (Users module + one protected endpoint) on TestServer and SQLite in memory.</summary>
public sealed partial class UsersHost : IAsyncDisposable
{
    public const string AdminUserName = "admin";
    public const string AdminPassword = "Admin12345";

    private readonly WebApplication _app;
    private readonly SqliteConnection _connection;

    private UsersHost(WebApplication app, SqliteConnection connection) => (_app, _connection) = (app, connection);

    public IServiceProvider Services => _app.Services;

    public static async Task<UsersHost> StartAsync()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();
        await using (UsersDbContext db = new(new DbContextOptionsBuilder<UsersDbContext>().UseSqlite(connection).Options))
        {
            await db.Database.EnsureCreatedAsync();
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddBorcUsers(db => db.UseSqlite(connection), o =>
        {
            o.MigrateOnStartup = false;
            o.Bootstrap.UserName = AdminUserName;
            o.Bootstrap.Password = AdminPassword;
        });
        builder.Services.AddBorcUsersUi();
        builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
        builder.Services.AddMvc().ConfigureApplicationPartManager(m => m.ApplicationParts.Add(new CompiledRazorAssemblyPart(typeof(UsersHost).Assembly)));

        WebApplication app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/", () => "home");
        app.MapRazorPages();
        await app.StartAsync();
        return new UsersHost(app, connection);
    }

    /// <summary>A browser-like client: keeps cookies, does not follow redirects.</summary>
    public HttpClient Client(bool modal = false)
    {
        HttpClient client = new(new CookieHandler(_app.GetTestServer().CreateHandler())) { BaseAddress = new Uri("http://localhost") };
        if (modal)
        {
            client.DefaultRequestHeaders.Add(UsersPageModel.ModalHeader, "1");
        }

        return client;
    }

    public async Task<HttpClient> SignedInAsync(string login = AdminUserName, string password = AdminPassword, bool modal = false)
    {
        HttpClient client = Client(modal);
        HttpResponseMessage response = await SignInAsync(client, login, password);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    public static Task<HttpResponseMessage> SignInAsync(HttpClient client, string login, string password) =>
        SubmitAsync(client, "/Account/Login", [new("Login", login), new("Password", password)]);

    /// <summary>GETs the form and POSTs it back with its antiforgery token plus <paramref name="fields"/>.</summary>
    public static async Task<HttpResponseMessage> SubmitAsync(HttpClient client, string url, IEnumerable<KeyValuePair<string, string>> fields)
    {
        HttpResponseMessage get = await client.GetAsync(url);
        get.EnsureSuccessStatusCode();
        string html = await get.Content.ReadAsStringAsync();
        string token = TokenInput().Match(html).Groups[1].Value;
        Match form = FormAction().Match(html);
        string action = WebUtility.HtmlDecode(form.Groups[1].Success ? form.Groups[1].Value : form.Groups[2].Value);
        return await client.PostAsync(action, new FormUrlEncodedContent(fields.Append(new("__RequestVerificationToken", token))));
    }

    public static async Task<string> TextAsync(HttpResponseMessage response) => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _connection.Dispose();
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenInput();

    [GeneratedRegex("<form[^>]*method=\"post\"[^>]*action=\"([^\"]*)\"|<form[^>]*action=\"([^\"]*)\"[^>]*method=\"post\"")]
    private static partial Regex FormAction();

    private sealed class CookieHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private readonly CookieContainer _cookies = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string header = _cookies.GetCookieHeader(request.RequestUri!);
            if (header.Length > 0)
            {
                request.Headers.Add("Cookie", header);
            }

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values))
            {
                foreach (string value in values)
                {
                    _cookies.SetCookies(request.RequestUri!, value);
                }
            }

            return response;
        }
    }
}
