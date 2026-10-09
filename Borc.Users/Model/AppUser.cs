using Microsoft.AspNetCore.Identity;

namespace Borc.Users.Model;

/// <summary>
/// A user account. Accounts are never deleted (other data keeps referencing the id); <see cref="IsActive"/> = false
/// blocks sign-in instead.
/// </summary>
public sealed class AppUser : IdentityUser<long>
{
    public const int DisplayNameMaxLength = 128;

    public string DisplayName { get; set; } = "";

    public bool IsActive { get; set; } = true;

    /// <summary>May manage user accounts. Finer-grained access comes from the Acl module later.</summary>
    public bool IsAdministrator { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? LastSignInAt { get; set; }
}
