namespace Borc.Users.Services;

/// <summary>Account management for administrators. The acting user comes from the current HTTP request.</summary>
public interface IUserAdministration
{
    Task<UserPage> ListAsync(UserQuery query, CancellationToken cancellationToken = default);

    Task<UserSummary?> GetAsync(long id, CancellationToken cancellationToken = default);

    Task<long> CreateAsync(UserInput input, CancellationToken cancellationToken = default);

    Task UpdateAsync(long id, UserInput input, CancellationToken cancellationToken = default);

    /// <summary>Deactivating signs the user out everywhere (security stamp) and notifies <see cref="IUserStatusListener"/>s.</summary>
    Task SetActiveAsync(long id, bool active, CancellationToken cancellationToken = default);

    /// <summary>Sets a new password and ends a lockout; the user's other sessions are signed out.</summary>
    Task ResetPasswordAsync(long id, string newPassword, CancellationToken cancellationToken = default);
}

/// <summary>Names and search for pickers and for other modules (org chart, access control).</summary>
public interface IUserLookup
{
    /// <summary>Active users whose user name, display name or e-mail contains <paramref name="text"/>.</summary>
    Task<IReadOnlyList<UserSummary>> SearchAsync(string text, int maxResults, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, UserSummary>> GetAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken = default);
}

/// <summary>Told when an account is deactivated or reactivated (e.g. to end the user's org-chart assignments).</summary>
public interface IUserStatusListener
{
    Task OnStatusChangedAsync(long userId, bool isActive, CancellationToken cancellationToken = default);
}
