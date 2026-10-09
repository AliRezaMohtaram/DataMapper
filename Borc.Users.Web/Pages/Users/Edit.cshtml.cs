using Borc.Users.Services;

using Microsoft.AspNetCore.Mvc;

namespace Borc.Users.Web.Pages.Users;

/// <summary>Create (no id) or edit an account.</summary>
[Microsoft.AspNetCore.Authorization.Authorize(Policy = UsersPolicies.Administrator)]
public sealed class EditModel(IUserAdministration admin) : UsersPageModel
{
    public const string FormPath = "/Pages/Users/_EditForm.cshtml";

    [BindProperty(SupportsGet = true)] public long? Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty] public string? UserName { get; set; }
    [BindProperty] public string? DisplayName { get; set; }
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public bool IsAdministrator { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? Confirm { get; set; }

    public bool IsEdit => Id is not null;
    public string BackUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Page("/Users/Index")!;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (Id is { } id)
        {
            if (await admin.GetAsync(id, cancellationToken) is not { } user)
            {
                return NotFound();
            }

            (UserName, DisplayName, Email, IsAdministrator) = (user.UserName, user.DisplayName, user.Email, user.IsAdministrator);
        }

        return Form(FormPath);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!IsEdit && Password != Confirm)
        {
            ModelState.AddModelError(string.Empty, "تکرار رمز عبور با آن یکسان نیست.");
            return Form(FormPath);
        }

        UserInput input = new(UserName ?? "", DisplayName ?? "", Email, IsAdministrator, Password);
        try
        {
            if (Id is { } id)
            {
                await admin.UpdateAsync(id, input, cancellationToken);
                return Done(BackUrl, $"حساب «{input.DisplayName.Trim()}» ذخیره شد.");
            }

            await admin.CreateAsync(input, cancellationToken);
            return Done(BackUrl, $"حساب «{input.DisplayName.Trim()}» ساخته شد.");
        }
        catch (UsersException ex)
        {
            AddErrors(ex);
            Password = Confirm = null;
            return Form(FormPath);
        }
    }
}
