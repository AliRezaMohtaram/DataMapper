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
        if (string.IsNullOrWhiteSpace(admin.UserName) || await db.Users.AnyAsync(cancellationToken))
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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
