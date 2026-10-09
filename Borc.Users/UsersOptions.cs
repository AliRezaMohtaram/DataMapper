namespace Borc.Users;

public sealed class UsersOptions
{
    /// <summary>Applies the module's migrations (schema "usr" only) at startup. Default true.</summary>
    public bool MigrateOnStartup { get; set; } = true;

    /// <summary>Created at startup as an administrator when there is no user yet (empty user name = none).</summary>
    public BootstrapAdmin Bootstrap { get; set; } = new();

    /// <summary>Minimum password length (a digit is also required). Default 8.</summary>
    public int PasswordMinLength { get; set; } = 8;

    /// <summary>Failed sign-ins before a temporary lockout. Default 5.</summary>
    public int MaxFailedSignIns { get; set; } = 5;

    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(10);

    public sealed class BootstrapAdmin
    {
        public string? UserName { get; set; }

        public string? Password { get; set; }

        public string DisplayName { get; set; } = "مدیر سامانه";
    }
}
