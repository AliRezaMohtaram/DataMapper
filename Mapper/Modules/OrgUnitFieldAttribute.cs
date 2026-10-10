using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Borc.DataMapper.Web.Modules;

/// <summary>What <c>_OrgUnitField</c> renders: the choices and the selected key (null = public).</summary>
public sealed record OrgUnitFieldModel(IReadOnlyList<OrgUnitOption> Options, string? Selected);

/// <summary>
/// فیلد «واحد سازمانی» در فرم‌های ایجاد. On a create action: fills <c>ViewData["OrgUnitField"]</c> for the form and, on
/// POST, validates the posted <c>OrgUnitKey</c> against the user's choices and hands it to <see cref="OrgUnitChoices"/>
/// (the save then stamps it on the new row). An unknown unit is a model error.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OrgUnitFieldAttribute(string resourceKey, bool allowPublic) : Attribute, IAsyncActionFilter
{
    public const string ViewDataKey = "OrgUnitField";
    public const string FieldName = "OrgUnitKey";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var choices = context.HttpContext.RequestServices.GetRequiredService<OrgUnitChoices>();
        var ct = context.HttpContext.RequestAborted;
        var options = await choices.OptionsAsync(resourceKey, allowPublic, ct);
        string? selected = await choices.GetDefaultAsync(ct);
        if (!options.Any(o => string.Equals(o.Key, selected, StringComparison.OrdinalIgnoreCase)))
        {
            selected = options.FirstOrDefault()?.Key;
        }

        var request = context.HttpContext.Request;
        if (HttpMethods.IsPost(request.Method) && request.HasFormContentType && request.Form.ContainsKey(FieldName))
        {
            string? posted = request.Form[FieldName].ToString();
            posted = string.IsNullOrWhiteSpace(posted) ? null : posted.Trim();
            if (options.Any(o => string.Equals(o.Key, posted, StringComparison.OrdinalIgnoreCase)))
            {
                choices.Choose(posted);
                selected = posted;
            }
            else
            {
                context.ModelState.AddModelError(FieldName, "واحد سازمانی انتخاب‌شده مجاز نیست.");
            }
        }

        if (context.Controller is Controller controller && options.Count > 0)
        {
            controller.ViewData[ViewDataKey] = new OrgUnitFieldModel(options, selected);
        }

        await next();
    }
}
