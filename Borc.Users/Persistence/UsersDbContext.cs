using Borc.Users.Model;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Borc.Users.Persistence;

/// <summary>Identity tables in schema "usr" (no roles: authorization belongs to the host or the Acl module).</summary>
public sealed class UsersDbContext(DbContextOptions<UsersDbContext> options) : IdentityUserContext<AppUser, long>(options)
{
    public const string Schema = "usr";

    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<AppUser>(user =>
        {
            user.ToTable("Users");
            user.Property(u => u.DisplayName).HasMaxLength(AppUser.DisplayNameMaxLength).IsRequired();
            user.Property(u => u.IsActive).HasDefaultValue(true);
            user.Property(u => u.CreatedAt).HasPrecision(3);
            user.Property(u => u.UpdatedAt).HasPrecision(3);
            user.Property(u => u.LastSignInAt).HasPrecision(3);
            user.Property(u => u.LockoutEnd).HasPrecision(3);

            // Sign-in accepts the e-mail too, so it must identify one account.
            user.HasIndex(u => u.NormalizedEmail).HasDatabaseName("UX_Users_NormalizedEmail").IsUnique()
                .HasFilter("[NormalizedEmail] IS NOT NULL");
        });

        builder.Entity<IdentityUserClaim<long>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<long>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<long>>().ToTable("UserTokens");
    }
}
