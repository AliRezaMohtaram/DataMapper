using Borc.Users.Services;

using Microsoft.AspNetCore.Mvc;

namespace Borc.Users.Web.Pages.Users;

/// <summary>Ends a temporary lockout without changing the password.</summary>
[Microsoft.AspNetCore.Authorization.Authorize(Policy = UsersPolicies.Manage)]
public sealed class UnlockModel(IUserAdministration admin) : UsersPageModel
{
    public const string FormPath = "/Pages/Users/_UnlockForm.cshtml";

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

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

        try
        {
            await admin.UnlockAsync(Id, cancellationToken);
            return Done(BackUrl, $"قفل حساب «{Subject.DisplayName}» برداشته شد.");
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
