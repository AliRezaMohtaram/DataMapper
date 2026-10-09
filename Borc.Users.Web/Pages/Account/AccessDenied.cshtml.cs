namespace Borc.Users.Web.Pages.Account;

[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public sealed class AccessDeniedModel : UsersPageModel
{
    public void OnGet()
    {
    }
}
