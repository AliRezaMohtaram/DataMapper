using System.Net;
using System.Text.RegularExpressions;
using Acl.EFCore;
using Borc.DataMapper.Infrastructure.Persistence;
using Borc.Users.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrgChart.EFCore;

namespace Mapper.Tests;

/// <summary>
/// The real Mapper app (Program.cs) with each module's DbContext on its own SQLite in-memory database
/// (created up front; startup migrations off), so the wiring — sign-in, layout, module pages — runs end to end.
/// </summary>
public sealed partial class MapperApp : WebApplicationFactory<Program>
{
    public const string AdminPassword = "Admin12345";

    private readonly SqliteConnection _mapper = Open();
    private readonly SqliteConnection _users = Open();
    private readonly SqliteConnection _chart = Open();
    private readonly SqliteConnection _acl = Open();

    public MapperApp()
    {
        new BorcDataMapperDbContext(Options<BorcDataMapperDbContext>(_mapper)).Database.EnsureCreated();
        new UsersDbContext(Options<UsersDbContext>(_users)).Database.EnsureCreated();
        new OrgChartDbContext(Options<OrgChartDbContext>(_chart)).Database.EnsureCreated();
        new AclDbContext(Options<AclDbContext>(_acl)).Database.EnsureCreated();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Users:MigrateOnStartup", "false");
        builder.UseSetting("Users:Bootstrap:Password", AdminPassword);
        builder.UseSetting("Modules:MigrateOnStartup", "false");
        builder.ConfigureTestServices(services =>
        {
            UseSqlite<BorcDataMapperDbContext>(services, _mapper);
            services.AddDbContext<BorcDataMapperDbContext>((sp, o) => o.AddInterceptors(sp.GetRequiredService<AuditStampInterceptor>()));
            UseSqlite<UsersDbContext>(services, _users);
            UseSqlite<OrgChartDbContext>(services, _chart);
            UseSqlite<AclDbContext>(services, _acl);

            // The entry assembly is the test host here; scan Mapper for its [Resource] declarations as the real app does.
            services.Configure<Acl.Core.AclOptions>(o => o.ResourceAssemblies.Add(typeof(Program).Assembly));
        });
    }

    /// <summary>A browser-like client signed in as the bootstrap administrator (id 1, Acl super admin).</summary>
    public Task<HttpClient> AdminAsync() => SignInAsync("admin", AdminPassword);

    /// <summary>A browser-like client (cookies kept, redirects not followed) signed in as the given user.</summary>
    public async Task<HttpClient> SignInAsync(string userName, string password)
    {
        HttpClient client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        HttpResponseMessage login = await client.GetAsync("/Account/Login");
        string html = await login.Content.ReadAsStringAsync();
        string token = Token().Match(html).Groups[1].Value;
        HttpResponseMessage response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(
        [
            new("Login", userName), new("Password", password), new("__RequestVerificationToken", token),
        ]));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _mapper.Dispose();
        _users.Dispose();
        _chart.Dispose();
        _acl.Dispose();
    }

    private static SqliteConnection Open()
    {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    private static DbContextOptions<T> Options<T>(SqliteConnection connection) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseSqlite(connection).Options;

    /// <summary>Drops the app's SQL Server configuration of <typeparamref name="T"/> and uses SQLite instead.</summary>
    private static void UseSqlite<T>(IServiceCollection services, SqliteConnection connection) where T : DbContext
    {
        services.RemoveAll<DbContextOptions<T>>();
        services.RemoveAll<IDbContextOptionsConfiguration<T>>();
        services.AddDbContext<T>(o => o.UseSqlite(connection));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex Token();
}
