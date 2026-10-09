using Borc.Users.Services;

using Microsoft.AspNetCore.Mvc;

namespace Borc.Users.Web.Pages.Users;

/// <summary>Confirms and applies (de)activation of an account.</summary>
[Microsoft.AspNetCore.Authorization.Authorize(Policy = UsersPolicies.Administrator)]
public sealed class StatusModel(IUserAdministration admin) : UsersPageModel
{
    public const string FormPath = "/Pages/Users/_StatusForm.cshtml";

    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty(SupportsGet = true)] public bool Active { get; set; }
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
            await admin.SetActiveAsync(Id, Active, cancellationToken);
            return Done(BackUrl, Active ? $"حساب «{Subject.DisplayName}» فعال شد." : $"حساب «{Subject.DisplayName}» غیرفعال شد.");
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
