using Borc.Users.Services;

using Microsoft.AspNetCore.Mvc;

namespace Borc.Users.Web.Pages.Account;

/// <summary>The signed-in user changes their own password.</summary>
public sealed class PasswordModel(UserSignIn signIn) : UsersPageModel
{
    public const string FormPath = "/Pages/Account/_PasswordForm.cshtml";

    [BindProperty] public string? Current { get; set; }
    [BindProperty] public string? NewPassword { get; set; }
    [BindProperty] public string? Confirm { get; set; }

    public IActionResult OnGet() => Form(FormPath);

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(Current) || string.IsNullOrEmpty(NewPassword))
        {
            ModelState.AddModelError(string.Empty, "رمز فعلی و رمز جدید را وارد کنید.");
            return Form(FormPath);
        }

        if (NewPassword != Confirm)
        {
            ModelState.AddModelError(string.Empty, "تکرار رمز جدید با آن یکسان نیست.");
            return Form(FormPath);
        }

        try
        {
            await signIn.ChangePasswordAsync(User, Current, NewPassword);
            return Done(Url.Content("~/"), "رمز عبور شما تغییر کرد.");
        }
        catch (UsersException ex)
        {
            AddErrors(ex);
            return Form(FormPath);
        }
    }
}
