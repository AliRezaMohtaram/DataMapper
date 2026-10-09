using Borc.Users;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Borc.Users.Web;

public static class UsersPolicies
{
    /// <summary>May manage user accounts (administrator flag on the account).</summary>
    public const string Administrator = "Users.Administrator";
}

public static class UsersUiExtensions
{
    /// <summary>
    /// Cookie sign-in for the whole host (Identity application cookie), the pages under /Account and /Users and the
    /// <see cref="UsersPolicies.Administrator"/> policy. With <paramref name="requireSignIn"/> (default) every endpoint
    /// without [AllowAnonymous] needs a signed-in user. The host calls AddRazorPages() and MapRazorPages().
    /// </summary>
    public static IServiceCollection AddBorcUsersUi(this IServiceCollection services, bool requireSignIn = true)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(cookie =>
        {
            cookie.LoginPath = "/Account/Login";
            cookie.LogoutPath = "/Account/Logout";
            cookie.AccessDeniedPath = "/Account/AccessDenied";
            cookie.Cookie.Name = "Borc.Auth";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            cookie.SlidingExpiration = true;
            cookie.ExpireTimeSpan = TimeSpan.FromHours(8);
            cookie.Events = new CookieAuthenticationEvents
            {
                // Modal (fetch) requests get a status code instead of the sign-in page's HTML.
                OnRedirectToLogin = context => RedirectOrStatus(context, StatusCodes401),
                OnRedirectToAccessDenied = context => RedirectOrStatus(context, StatusCodes403),
                OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync,
            };
        });

        // Deactivation and password resets reach other sessions within a minute.
        services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

        services.AddAuthorizationBuilder()
            .AddPolicy(UsersPolicies.Administrator, p => p.RequireAuthenticatedUser().RequireClaim(UserClaimTypes.Administrator, "true"));
        if (requireSignIn)
        {
            services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        }

        // Add the pages even when the host's entry assembly does not list this library (tests, plugins).
        services.AddRazorPages().ConfigureApplicationPartManager(manager =>
        {
            System.Reflection.Assembly assembly = typeof(UsersPageModel).Assembly;
            if (manager.ApplicationParts.Any(p => p is Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart a && a.Assembly == assembly))
            {
                return;
            }

            foreach (var part in Microsoft.AspNetCore.Mvc.ApplicationParts.ApplicationPartFactory.GetApplicationPartFactory(assembly).GetApplicationParts(assembly))
            {
                manager.ApplicationParts.Add(part);
            }
        });
        return services;
    }

    private const int StatusCodes401 = 401;
    private const int StatusCodes403 = 403;

    private static Task RedirectOrStatus(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context, int status)
    {
        if (context.Request.Headers["X-MX-Modal"] == "1" || context.Request.Headers.Accept.ToString().Contains("application/json"))
        {
            context.Response.StatusCode = status;
        }
        else
        {
            context.Response.Redirect(context.RedirectUri);
        }

        return Task.CompletedTask;
    }
}
