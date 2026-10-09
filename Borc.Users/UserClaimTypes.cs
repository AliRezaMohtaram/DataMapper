namespace Borc.Users;

/// <summary>Claims added to the signed-in user, besides NameIdentifier (the id) and Name (the user name).</summary>
public static class UserClaimTypes
{
    public const string DisplayName = "usr:display_name";

    /// <summary>Present ("true") for administrators.</summary>
    public const string Administrator = "usr:admin";
}
