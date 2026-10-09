using Acl.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using OrgChart.EFCore;

namespace Borc.DataMapper.Web.Modules;

/// <summary>
/// پیش از بقیهٔ سرویس‌ها جدول‌های چارت (schema org) و Acl (schema acl) را در همان پایگاه داده به‌روز می‌کند.
/// Applies the OrgChart (schema org) and Acl (schema acl) migrations to the existing Mapper database at startup
/// (Modules:MigrateOnStartup, default true). Registered before AddAccessControl so Acl's startup work sees its tables.
/// </summary>
public sealed class ModuleDatabaseMigrator(IServiceProvider services, IConfiguration configuration, ILogger<ModuleDatabaseMigrator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Modules:MigrateOnStartup", true))
        {
            return;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        foreach (DbContext db in new DbContext[]
                 {
                     scope.ServiceProvider.GetRequiredService<OrgChartDbContext>(),
                     scope.ServiceProvider.GetRequiredService<AclDbContext>(),
                 })
        {
            if (!await db.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Database '{db.Database.GetDbConnection().Database}' does not exist. Check ConnectionStrings:BorcDataMapper.");
            }

            IEnumerable<string> pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pending.Any())
            {
                logger.LogInformation("Applying {Context} migrations: {Migrations}", db.GetType().Name, string.Join(", ", pending));
                await db.Database.MigrateAsync(cancellationToken);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
