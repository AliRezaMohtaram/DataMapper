using Borc.DataMapper.Web.Modules;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

/// <summary>«فقط داده‌های من» / «همهٔ داده‌هایی که می‌توانم ببینم» برای مدیران و نقش‌های با دید بیشتر (کوکی).</summary>
public sealed class ViewModeController : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Set(bool mine, string? returnUrl)
    {
        if (mine)
        {
            Response.Cookies.Append(DataVisibility.ModeCookie, DataVisibility.MineMode,
                new CookieOptions { HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365) });
        }
        else
        {
            Response.Cookies.Delete(DataVisibility.ModeCookie);
        }

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
