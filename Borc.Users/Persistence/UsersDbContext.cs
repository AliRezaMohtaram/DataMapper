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

    /// <summary>Length of the string parts of the login and token keys (2 × 128 × 2 bytes + 8 &lt; 900).</summary>
    private const int KeyPartLength = 128;

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
        // SQL Server: a clustered key may hold at most 900 bytes; Identity's default nvarchar(450) pairs need 1800.
        builder.Entity<IdentityUserLogin<long>>(login =>
        {
            login.ToTable("UserLogins");
            login.Property(l => l.LoginProvider).HasMaxLength(KeyPartLength);
            login.Property(l => l.ProviderKey).HasMaxLength(KeyPartLength);
        });
        builder.Entity<IdentityUserToken<long>>(token =>
        {
            token.ToTable("UserTokens");
            token.Property(t => t.LoginProvider).HasMaxLength(KeyPartLength);
            token.Property(t => t.Name).HasMaxLength(KeyPartLength);
        });
    }
}
