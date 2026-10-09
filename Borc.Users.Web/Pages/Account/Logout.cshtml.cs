using Borc.Users.Services;

using Microsoft.AspNetCore.Mvc;

namespace Borc.Users.Web.Pages.Account;

/// <summary>POST only (antiforgery-protected); a GET just goes home.</summary>
public sealed class LogoutModel(UserSignIn signIn) : UsersPageModel
{
    public IActionResult OnGet() => LocalRedirect("~/");

    public async Task<IActionResult> OnPostAsync()
    {
        await signIn.SignOutAsync();
        return RedirectToPage("/Account/Login");
    }
}
