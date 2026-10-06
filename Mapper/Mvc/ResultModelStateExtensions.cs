using Borc.DataMapper.Application.Common.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Borc.DataMapper.Web.Mvc;

public static class ResultModelStateExtensions
{
    /// <summary>
    /// خطای یک Result ناموفق را به ModelState منتقل می‌کند:
    /// خطاهای اعتبارسنجی هر ویژگی زیر همان فیلد فرم (اگر آن فیلد در فرم ارسال شده باشد)
    /// و بقیه — یا پیام کلی Result — به‌عنوان خطای کل فرم.
    /// </summary>
    public static void AddResultErrors(this ModelStateDictionary modelState, Result result, string fallback, HttpRequest? request = null)
    {
        if (result.ValidationErrors is not { Count: > 0 } errors)
        {
            modelState.AddModelError(string.Empty, result.Message ?? fallback);
            return;
        }

        var posted = request?.HasFormContentType == true
            ? request.Form.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var unplaced = new List<string>();

        foreach (var (key, messages) in errors)
        {
            foreach (var message in messages)
            {
                if (posted is null || posted.Contains(key))
                    modelState.AddModelError(key, message);
                else
                    unplaced.Add(message);
            }
        }

        if (unplaced.Count > 0)
            modelState.AddModelError(string.Empty, string.Join(" — ", unplaced.Distinct()));
    }
}
