using System.Net;
using System.Text.Json;

using Borc.Users.Services;

using Microsoft.Extensions.DependencyInjection;

namespace Borc.Users.Tests;

public sealed class UsersTests : IAsyncLifetime
{
    private UsersHost _host = null!;

    public async Task InitializeAsync() => _host = await UsersHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<T> AdminAsync<T>(Func<IUserAdministration, Task<T>> action)
    {
        await using AsyncServiceScope scope = _host.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IUserAdministration>());
    }

    private Task<long> CreateUserAsync(string userName, string? email = null, bool admin = false) =>
        AdminAsync(a => a.CreateAsync(new UserInput(userName, "کاربر " + userName, email, admin, "Secret123")));

    [Fact]
    public async Task Anonymous_requests_go_to_the_sign_in_page_and_modal_requests_get_401()
    {
        HttpResponseMessage page = await _host.Client().GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.StartsWith("/Account/Login", page.Headers.Location!.PathAndQuery);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _host.Client(modal: true).GetAsync("/Users/Edit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _host.Client().GetAsync("/Account/Login")).StatusCode);
    }

    [Fact]
    public async Task Bootstrap_admin_signs_in_and_reaches_protected_pages()
    {
        HttpClient client = await _host.SignedInAsync();

        Assert.Equal("home", await client.GetStringAsync("/"));
        string users = await UsersHost.TextAsync(await client.GetAsync("/Users"));
        Assert.Contains("مدیر سامانه", users);
    }

    [Fact]
    public async Task Sign_in_accepts_the_email_and_rejects_a_wrong_password()
    {
        await CreateUserAsync("sara", "sara@example.com");

        Assert.Equal(HttpStatusCode.Redirect, (await UsersHost.SignInAsync(_host.Client(), "SARA@example.com", "Secret123")).StatusCode);

        HttpResponseMessage wrong = await UsersHost.SignInAsync(_host.Client(), "sara", "nope12345");
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        Assert.Contains("نام کاربری یا رمز عبور درست نیست", await UsersHost.TextAsync(wrong));
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account_until_an_administrator_resets_the_password()
    {
        long id = await CreateUserAsync("reza");
        for (int i = 0; i < 5; i++)
        {
            await UsersHost.SignInAsync(_host.Client(), "reza", "wrong123");
        }

        HttpResponseMessage locked = await UsersHost.SignInAsync(_host.Client(), "reza", "Secret123");
        Assert.Contains("قفل", await UsersHost.TextAsync(locked));
        Assert.True((await AdminAsync(a => a.GetAsync(id)))!.IsLockedOut);

        await AdminAsync(async a => { await a.ResetPasswordAsync(id, "NewSecret1"); return 0; });

        Assert.Equal(HttpStatusCode.Redirect, (await UsersHost.SignInAsync(_host.Client(), "reza", "NewSecret1")).StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_cannot_sign_in_and_loses_open_sessions()
    {
        long id = await CreateUserAsync("ali");
        HttpClient session = await _host.SignedInAsync("ali", "Secret123");
        Assert.Equal("home", await session.GetStringAsync("/"));

        HttpClient admin = await _host.SignedInAsync(modal: true);
        HttpResponseMessage response = await UsersHost.SubmitAsync(admin, $"/Users/Status?id={id}&active=false", []);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("redirect", await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Redirect, (await session.GetAsync("/")).StatusCode);
        HttpResponseMessage again = await UsersHost.SignInAsync(_host.Client(), "ali", "Secret123");
        Assert.Contains("غیرفعال", await UsersHost.TextAsync(again));
    }

    [Fact]
    public async Task Non_administrators_cannot_open_user_management()
    {
        await CreateUserAsync("user1");
        HttpClient client = await _host.SignedInAsync("user1", "Secret123");

        HttpResponseMessage response = await client.GetAsync("/Users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Administrator_creates_a_user_in_a_modal_and_errors_stay_in_the_form()
    {
        HttpClient admin = await _host.SignedInAsync(modal: true);

        HttpResponseMessage created = await UsersHost.SubmitAsync(admin, "/Users/Edit",
        [
            new("DisplayName", "مریم رضایی"), new("UserName", "maryam"), new("Email", "m@example.com"),
            new("Password", "Secret123"), new("Confirm", "Secret123"),
        ]);
        using (JsonDocument json = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            Assert.Equal("/Users", json.RootElement.GetProperty("redirect").GetString());
        }

        UserSummary maryam = Assert.Single((await AdminAsync(a => a.ListAsync(new UserQuery("maryam")))).Items);
        Assert.Equal(("مریم رضایی", "m@example.com", false), (maryam.DisplayName, maryam.Email, maryam.IsAdministrator));

        HttpResponseMessage duplicate = await UsersHost.SubmitAsync(admin, "/Users/Edit",
            [new("DisplayName", "x"), new("UserName", "MARYAM"), new("Password", "Secret123"), new("Confirm", "Secret123")]);
        string html = await UsersHost.TextAsync(duplicate);
        Assert.StartsWith("<form", html.TrimStart());
        Assert.Contains("قبلاً ثبت شده است", html);

        HttpResponseMessage weak = await UsersHost.SubmitAsync(admin, "/Users/Edit",
            [new("DisplayName", "y"), new("UserName", "yy"), new("Password", "short"), new("Confirm", "short")]);
        Assert.Contains("دست‌کم 8 حرف", await UsersHost.TextAsync(weak));
    }

    [Fact]
    public async Task The_last_active_administrator_and_ones_own_account_are_protected()
    {
        long adminId = (await AdminAsync(a => a.ListAsync(new UserQuery(UsersHost.AdminUserName)))).Items[0].Id;
        HttpClient admin = await _host.SignedInAsync(modal: true);

        HttpResponseMessage self = await UsersHost.SubmitAsync(admin, $"/Users/Status?id={adminId}&active=false", []);
        Assert.Contains("نمی‌توانید حساب خودتان را غیرفعال کنید", await UsersHost.TextAsync(self));

        UsersException ex = await Assert.ThrowsAsync<UsersException>(() => AdminAsync(async a =>
        {
            await a.UpdateAsync(adminId, new UserInput(UsersHost.AdminUserName, "مدیر", null, false));
            return 0;
        }));
        Assert.Equal(UsersErrors.LastAdministrator, ex.Code);
    }

    [Fact]
    public async Task User_changes_own_password()
    {
        await CreateUserAsync("neda");
        HttpClient client = await _host.SignedInAsync("neda", "Secret123");

        HttpResponseMessage wrong = await UsersHost.SubmitAsync(client, "/Account/Password",
            [new("Current", "bad12345"), new("NewPassword", "Changed123"), new("Confirm", "Changed123")]);
        Assert.Contains("رمز عبور فعلی درست نیست", await UsersHost.TextAsync(wrong));

        HttpResponseMessage ok = await UsersHost.SubmitAsync(client, "/Account/Password",
            [new("Current", "Secret123"), new("NewPassword", "Changed123"), new("Confirm", "Changed123")]);
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await UsersHost.SignInAsync(_host.Client(), "neda", "Changed123")).StatusCode);
    }

    [Fact]
    public async Task Lookup_searches_active_users_only()
    {
        long a = await CreateUserAsync("kian", "kian@example.com");
        long b = await CreateUserAsync("kiana");
        await AdminAsync(async x => { await x.SetActiveAsync(b, false); return 0; });

        await using AsyncServiceScope scope = _host.Services.CreateAsyncScope();
        IUserLookup lookup = scope.ServiceProvider.GetRequiredService<IUserLookup>();
        Assert.Equal([a], (await lookup.SearchAsync("kia", 10)).Select(u => u.Id));
        Assert.Equal(2, (await lookup.GetAsync([a, b, 999])).Count);
    }
}
