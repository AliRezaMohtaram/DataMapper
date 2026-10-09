using Borc.Users.Services;

using Microsoft.AspNetCore.Mvc;

namespace Borc.Users.Web.Pages.Account;

[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public sealed class LoginModel(UserSignIn signIn) : UsersPageModel
{
    [BindProperty] public string? Login { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public bool Remember { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? LocalRedirect(SafeReturnUrl) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrEmpty(Password))
        {
            ModelState.AddModelError(string.Empty, "نام کاربری (یا ایمیل) و رمز عبور را وارد کنید.");
            return Page();
        }

        SignInOutcome outcome = await signIn.SignInAsync(Login, Password, Remember);
        if (outcome == SignInOutcome.Succeeded)
        {
            return LocalRedirect(SafeReturnUrl);
        }

        ModelState.AddModelError(string.Empty, outcome switch
        {
            SignInOutcome.LockedOut => "به‌دلیل تلاش‌های ناموفق، حساب موقتاً قفل شده است. چند دقیقه بعد دوباره تلاش کنید.",
            SignInOutcome.Inactive => "این حساب غیرفعال است. با مدیر سامانه تماس بگیرید.",
            _ => "نام کاربری یا رمز عبور درست نیست.",
        });
        Password = null;
        return Page();
    }

    private string SafeReturnUrl => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : Url.Content("~/");
}
