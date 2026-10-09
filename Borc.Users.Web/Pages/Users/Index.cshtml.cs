using Borc.Users.Services;

using Microsoft.AspNetCore.Mvc;

namespace Borc.Users.Web.Pages.Users;

[Microsoft.AspNetCore.Authorization.Authorize(Policy = UsersPolicies.Administrator)]
public sealed class IndexModel(IUserAdministration admin) : UsersPageModel
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Search { get; set; }

    /// <summary>"active", "inactive" or empty (all).</summary>
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public UserPage Result { get; private set; } = new([], 0, 1, 20);

    public long? CurrentUserId => long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out long id) ? id : null;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        bool? active = Status switch { "active" => true, "inactive" => false, _ => null };
        Result = await admin.ListAsync(new UserQuery(Search, active, PageNumber), cancellationToken);
    }

    public string ReturnUrl => Request.Path + Request.QueryString;
}
