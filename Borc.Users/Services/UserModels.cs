namespace Borc.Users.Services;

public sealed record UserSummary(
    long Id,
    string UserName,
    string DisplayName,
    string? Email,
    bool IsActive,
    bool IsAdministrator,
    bool IsLockedOut,
    DateTime CreatedAt,
    DateTime? LastSignInAt);

/// <param name="Active">Only active (true), only inactive (false) or all (null).</param>
public sealed record UserQuery(string? Search = null, bool? Active = null, int Page = 1, int PageSize = 20);

public sealed record UserPage(IReadOnlyList<UserSummary> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}

/// <param name="Password">Required when creating; ignored when updating (see ResetPasswordAsync).</param>
public sealed record UserInput(string UserName, string DisplayName, string? Email, bool IsAdministrator, string? Password = null);

public enum SignInOutcome
{
    Succeeded = 1,
    InvalidCredentials = 2,
    LockedOut = 3,
    Inactive = 4,
}

/// <summary>A rejected user operation. <see cref="Messages"/> are ready to show (Persian).</summary>
public sealed class UsersException(string code, IReadOnlyList<string> messages) : Exception(string.Join(" ", messages))
{
    public UsersException(string code, string message) : this(code, [message])
    {
    }

    public string Code { get; } = code;

    public IReadOnlyList<string> Messages { get; } = messages;
}

public static class UsersErrors
{
    public const string NotFound = "NotFound";
    public const string Invalid = "Invalid";
    public const string Identity = "Identity";
    public const string SelfChange = "SelfChange";
    public const string LastAdministrator = "LastAdministrator";
}
