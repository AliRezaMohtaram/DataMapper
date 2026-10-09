using Borc.Users.Model;

using Microsoft.AspNetCore.Identity;

namespace Borc.Users.Services;

/// <summary>Signs in with the user name or the e-mail address.</summary>
public sealed class UserSignIn(SignInManager<AppUser> signIn, UserManager<AppUser> users, TimeProvider time)
{
    public async Task<SignInOutcome> SignInAsync(string login, string password, bool remember)
    {
        string text = login.Trim();
        AppUser? user = await users.FindByNameAsync(text);
        if (user is null && text.Contains('@'))
        {
            user = await users.FindByEmailAsync(text);
        }

        if (user is null)
        {
            return SignInOutcome.InvalidCredentials;
        }

        SignInResult result = await signIn.PasswordSignInAsync(user, password, remember, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            user.LastSignInAt = time.GetUtcNow().UtcDateTime;
            await users.UpdateAsync(user);
            return SignInOutcome.Succeeded;
        }

        // NotAllowed comes from ActiveUserConfirmation; only say so after a correct password.
        return result.IsLockedOut ? SignInOutcome.LockedOut
            : result.IsNotAllowed ? SignInOutcome.Inactive
            : SignInOutcome.InvalidCredentials;
    }

    public Task SignOutAsync() => signIn.SignOutAsync();

    /// <summary>Changes the signed-in user's own password and refreshes their cookie.</summary>
    public async Task ChangePasswordAsync(System.Security.Claims.ClaimsPrincipal principal, string current, string next)
    {
        AppUser user = await users.GetUserAsync(principal) ?? throw new UsersException(UsersErrors.NotFound, "کاربر پیدا نشد.");
        IdentityResult result = await users.ChangePasswordAsync(user, current, next);
        if (!result.Succeeded)
        {
            throw new UsersException(UsersErrors.Identity, result.Errors.Select(e => e.Description).ToList());
        }

        await signIn.RefreshSignInAsync(user);
    }
}

/// <summary>Inactive accounts cannot sign in (SignInManager reports NotAllowed).</summary>
internal sealed class ActiveUserConfirmation : IUserConfirmation<AppUser>
{
    public Task<bool> IsConfirmedAsync(UserManager<AppUser> manager, AppUser user) => Task.FromResult(user.IsActive);
}

/// <summary>Adds the display name and the administrator flag to the cookie.</summary>
internal sealed class AppUserClaimsFactory(UserManager<AppUser> users, Microsoft.Extensions.Options.IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser>(users, options)
{
    protected override async Task<System.Security.Claims.ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        System.Security.Claims.ClaimsIdentity identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new(UserClaimTypes.DisplayName, user.DisplayName));
        if (user.IsAdministrator)
        {
            identity.AddClaim(new(UserClaimTypes.Administrator, "true"));
        }

        return identity;
    }
}
