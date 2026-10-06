using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Mvc;

/// <summary>
/// فرم‌های «مودال‌محور»: mx.js فرم را با هدر X-MX-Modal در مودال باز می‌کند.
/// برای چنین درخواستی فقط partial فرم (یا JSON مقصد) برمی‌گردد؛ بدون JavaScript
/// همان آدرس‌ها صفحهٔ کامل را نشان می‌دهند.
/// </summary>
public static class ModalResults
{
    public const string Header = "X-MX-Modal";

    public static bool IsModalRequest(this HttpRequest request)
        => request.Headers[Header] == "1";

    /// <summary>درخواست مودال: partial فرم؛ در غیر این صورت View کامل (پیش‌فرض: هم‌نام اکشن).</summary>
    public static IActionResult ModalOrView(this Controller controller, string partialName, object model, string? viewName = null)
        => controller.Request.IsModalRequest()
            ? controller.PartialView(partialName, model)
            : viewName is null ? controller.View(model) : controller.View(viewName, model);

    /// <summary>پس از ذخیرهٔ موفق: مودال → JSON مقصد (mx.js خودش هدایت می‌کند)؛ صفحه → Redirect.</summary>
    public static IActionResult ModalOrRedirect(this Controller controller, string url)
        => controller.Request.IsModalRequest()
            ? controller.Json(new { redirect = url })
            : controller.Redirect(url);
}
