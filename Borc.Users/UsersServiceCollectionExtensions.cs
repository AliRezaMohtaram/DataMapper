using Borc.Users.Model;
using Borc.Users.Persistence;
using Borc.Users.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Borc.Users;

public static class UsersServiceCollectionExtensions
{
    /// <summary>Users on SQL Server (schema "usr", own migrations history table).</summary>
    public static IdentityBuilder AddBorcUsers(this IServiceCollection services, string connectionString, Action<UsersOptions>? configure = null) =>
        services.AddBorcUsers(
            db => db.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(UsersDbContext.MigrationsHistoryTable, UsersDbContext.Schema)),
            configure);

    /// <summary>
    /// Identity accounts with long ids, sign-in by user name or e-mail, Persian messages, lockout, inactive accounts
    /// rejected, and the services <see cref="IUserAdministration"/>, <see cref="IUserLookup"/> and <see cref="UserSignIn"/>.
    /// Cookie authentication and the pages come from Borc.Users.Web (<c>AddBorcUsersUi</c>).
    /// </summary>
    public static IdentityBuilder AddBorcUsers(this IServiceCollection services, Action<DbContextOptionsBuilder> database, Action<UsersOptions>? configure = null)
    {
        OptionsBuilder<UsersOptions> options = services.AddOptions<UsersOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.AddDbContext<UsersDbContext>(database);
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);

        IdentityBuilder identity = services.AddIdentityCore<AppUser>()
            .AddEntityFrameworkStores<UsersDbContext>()
            .AddSignInManager()
            .AddErrorDescriber<PersianIdentityErrorDescriber>()
            .AddClaimsPrincipalFactory<AppUserClaimsFactory>();
        services.AddOptions<IdentityOptions>().Configure<IOptions<UsersOptions>>((o, users) =>
        {
            o.User.RequireUniqueEmail = false; // e-mail is optional; uniqueness when set comes from a filtered index
            o.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@";
            o.Password.RequiredLength = users.Value.PasswordMinLength;
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = false;
            o.Password.RequireUppercase = false;
            o.Password.RequireNonAlphanumeric = false;
            o.Lockout.MaxFailedAccessAttempts = users.Value.MaxFailedSignIns;
            o.Lockout.DefaultLockoutTimeSpan = users.Value.LockoutDuration;
            o.SignIn.RequireConfirmedAccount = true; // "confirmed" = active, see ActiveUserConfirmation
        });
        services.AddScoped<IUserConfirmation<AppUser>, ActiveUserConfirmation>();

        services.AddScoped<UserAdministration>();
        services.AddScoped<IUserAdministration>(sp => sp.GetRequiredService<UserAdministration>());
        services.AddScoped<IUserLookup>(sp => sp.GetRequiredService<UserAdministration>());
        services.AddScoped<UserSignIn>();
        services.AddHostedService<UsersBootstrapper>();
        return identity;
    }

    /// <summary>Adds a listener told when an account is (de)activated. Several may be added.</summary>
    public static IServiceCollection AddUserStatusListener<TListener>(this IServiceCollection services)
        where TListener : class, IUserStatusListener
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IUserStatusListener, TListener>());
        return services;
    }
}
