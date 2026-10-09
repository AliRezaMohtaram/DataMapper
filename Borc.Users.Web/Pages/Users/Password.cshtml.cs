using Borc.Users.Services;

using Microsoft.AspNetCore.Mvc;

namespace Borc.Users.Web.Pages.Users;

/// <summary>An administrator sets a new password (also ends a lockout).</summary>
[Microsoft.AspNetCore.Authorization.Authorize(Policy = UsersPolicies.Manage)]
public sealed class PasswordModel(IUserAdministration admin) : UsersPageModel
{
    public const string FormPath = "/Pages/Users/_PasswordForm.cshtml";

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty] public string? NewPassword { get; set; }
    [BindProperty] public string? Confirm { get; set; }

    public UserSummary Subject { get; private set; } = null!;
    public string BackUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Page("/Users/Index")!;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken) ? Form(FormPath) : NotFound();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            return NotFound();
        }

        if (NewPassword != Confirm)
        {
            ModelState.AddModelError(string.Empty, "تکرار رمز عبور با آن یکسان نیست.");
            return Form(FormPath);
        }

        try
        {
            await admin.ResetPasswordAsync(Id, NewPassword ?? "", cancellationToken);
            return Done(BackUrl, $"رمز عبور «{Subject.DisplayName}» تغییر کرد.");
        }
        catch (UsersException ex)
        {
            AddErrors(ex);
            return Form(FormPath);
        }
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Subject = (await admin.GetAsync(Id, cancellationToken))!;
        return Subject is not null;
    }
}
