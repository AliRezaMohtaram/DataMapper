using System.Globalization;

using Borc.Users.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Borc.Users.Web;

/// <summary>
/// Modal-first forms like the host (mx.js): a request with <c>X-MX-Modal: 1</c> gets only the form partial and, after
/// saving, JSON <c>{redirect}</c>; without JavaScript the same URLs show a full page and redirect.
/// </summary>
public abstract class UsersPageModel : PageModel
{
    public const string ModalHeader = "X-MX-Modal";

    public bool IsModal => Request.Headers[ModalHeader] == "1";

    /// <summary>The form partial for a modal request, else the full page (which renders the same partial).</summary>
    protected IActionResult Form(string partialPath) => IsModal ? Partial(partialPath, this) : Page();

    /// <summary>Success: toast message for the next page (host layout shows TempData["Success"]) and navigation.</summary>
    protected IActionResult Done(string url, string message)
    {
        TempData["Success"] = message;
        return IsModal ? new JsonResult(new { redirect = url }) : Redirect(url);
    }

    protected void AddErrors(UsersException ex)
    {
        foreach (string message in ex.Messages)
        {
            ModelState.AddModelError(string.Empty, message);
        }
    }

    public IReadOnlyList<string> FormErrors => ModelState
        .Where(kv => string.IsNullOrEmpty(kv.Key))
        .SelectMany(kv => kv.Value!.Errors)
        .Select(e => e.ErrorMessage)
        .Where(m => !string.IsNullOrWhiteSpace(m))
        .ToList();
}

/// <summary>Persian (Jalali) dates and digits in the server's time zone.</summary>
public static class UsersFormat
{
    private static readonly PersianCalendar s_calendar = new();

    public static string Date(DateTime? utc, bool withTime = false)
    {
        if (utc is null)
        {
            return "—";
        }

        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), TimeZoneInfo.Local);
        string text = $"{s_calendar.GetYear(local):0000}/{s_calendar.GetMonth(local):00}/{s_calendar.GetDayOfMonth(local):00}";
        return Digits(withTime ? $"{text} {local:HH:mm}" : text);
    }

    public static string Number(int value) => Digits(value.ToString(CultureInfo.InvariantCulture));

    public static string Digits(string text) =>
        string.Concat(text.Select(c => c is >= '0' and <= '9' ? (char)('۰' + (c - '0')) : c));
}
