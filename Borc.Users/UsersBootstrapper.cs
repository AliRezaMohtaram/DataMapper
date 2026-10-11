using Borc.Users.Model;
using Borc.Users.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Borc.Users;

/// <summary>At startup: applies the "usr" migrations (if enabled) and creates the first administrator.</summary>
internal sealed class UsersBootstrapper(IServiceProvider services, IOptions<UsersOptions> options, ILogger<UsersBootstrapper> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        UsersDbContext db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        if (options.Value.MigrateOnStartup)
        {
            // The module only adds its own tables to the host's database; it never creates the database itself
            // (a wrong connection string would otherwise leave the host on a new, empty database).
            if (db.Database.IsRelational()
                && db.GetService<IRelationalDatabaseCreator>() is { } creator
                && !await creator.ExistsAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Users: database '{db.Database.GetDbConnection().Database}' does not exist. Check the connection string; "
                    + "the Users module creates only its own tables (schema usr) in an existing database.");
            }

            await db.Database.MigrateAsync(cancellationToken);
        }

        UsersOptions.BootstrapAdmin admin = options.Value.Bootstrap;
        if (string.IsNullOrWhiteSpace(admin.UserName))
        {
            return;
        }

        if (admin.ResetPassword)
        {
            await ResetAdministratorAsync(scope.ServiceProvider, admin);
            return;
        }

        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        if (string.IsNullOrEmpty(admin.Password))
        {
            logger.LogWarning("Users: no account exists and Users:Bootstrap:Password is empty, so no administrator was created.");
            return;
        }

        UserManager<AppUser> users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        AppUser user = new()
        {
            UserName = admin.UserName.Trim(),
            DisplayName = admin.DisplayName,
            IsAdministrator = true,
            CreatedAt = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
        };
        IdentityResult result = await users.CreateAsync(user, admin.Password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Users: the bootstrap administrator could not be created: "
                + string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        logger.LogInformation("Users: created the first administrator '{UserName}'. Change its password after signing in.", user.UserName);
    }

    /// <summary>Users:Bootstrap:ResetPassword — gives the bootstrap account its configured password back.</summary>
    private async Task ResetAdministratorAsync(IServiceProvider provider, UsersOptions.BootstrapAdmin admin)
    {
        if (string.IsNullOrEmpty(admin.Password))
        {
            logger.LogWarning("Users: ResetPassword is on but Users:Bootstrap:Password is empty; nothing was changed.");
            return;
        }

        UserManager<AppUser> users = provider.GetRequiredService<UserManager<AppUser>>();
        string userName = admin.UserName!.Trim();
        AppUser? user = await users.FindByNameAsync(userName);
        IdentityResult result;
        if (user is null)
        {
            user = new AppUser
            {
                UserName = userName,
                DisplayName = admin.DisplayName,
                IsAdministrator = true,
                CreatedAt = provider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime,
            };
            result = await users.CreateAsync(user, admin.Password);
        }
        else
        {
            user.IsActive = true;
            await users.UpdateAsync(user);
            await users.SetLockoutEndDateAsync(user, null);
            await users.ResetAccessFailedCountAsync(user);
            result = await users.RemovePasswordAsync(user);
            if (result.Succeeded)
            {
                result = await users.AddPasswordAsync(user, admin.Password);
            }
        }

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Users: the administrator password could not be reset: "
                + string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        logger.LogWarning("Users: the password of '{UserName}' was reset from configuration. Turn Users:Bootstrap:ResetPassword off now.", userName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
