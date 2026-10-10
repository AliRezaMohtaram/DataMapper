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

        // After the existence check above (same database).
        await ApplyMapperPatchesAsync(scope.ServiceProvider.GetRequiredService<Borc.DataMapper.Infrastructure.Persistence.BorcDataMapperDbContext>(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// اسکریپت‌های تغییر schema خود Mapper که به ماژول‌ها مربوط‌اند (Scripts/009_OrgUnits.sql)؛ idempotent هستند.
    /// Mapper's own schema patches needed by the module integration (idempotent scripts embedded from Scripts/).
    /// </summary>
    private async Task ApplyMapperPatchesAsync(DbContext db, CancellationToken cancellationToken)
    {
        foreach (string name in new[] { "Scripts.009_OrgUnits.sql" })
        {
            await using Stream stream = typeof(ModuleDatabaseMigrator).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded script {name} is missing.");
            string script = await new StreamReader(stream).ReadToEndAsync(cancellationToken);
            foreach (string batch in System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline))
            {
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    await db.Database.ExecuteSqlRawAsync(batch, cancellationToken);
                }
            }

            logger.LogDebug("Applied {Script}", name);
        }
    }
}
