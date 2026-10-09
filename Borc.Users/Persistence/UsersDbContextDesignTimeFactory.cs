using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Borc.Users.Persistence;

/// <summary>Only for <c>dotnet ef</c> (migrations); never used at runtime.</summary>
internal sealed class UsersDbContextDesignTimeFactory : IDesignTimeDbContextFactory<UsersDbContext>
{
    public UsersDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<UsersDbContext>()
        .UseSqlServer("Server=.;Database=BorcDataMapper;Trusted_Connection=True;TrustServerCertificate=True",
            sql => sql.MigrationsHistoryTable(UsersDbContext.MigrationsHistoryTable, UsersDbContext.Schema))
        .Options);
}
