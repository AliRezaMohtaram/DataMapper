using Acl.AspNetCore.Authorization;
using Acl.Core.Model;
using Borc.DataMapper.Application.OrgUnits;
using Borc.DataMapper.Web.Modules;
using Borc.DataMapper.Web.Mvc;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

/// <summary>
/// تغییر واحد سازمانی یک قالب/منبع داده/پروفایل/ایمپورت/رکورد (مودال). Needs Edit on the row's resource; the row must be
/// visible to the user and the new unit among their choices (public only for definitions).
/// </summary>
public sealed class OrgUnitsController(ISender sender, OrgUnitChoices choices, IAuthorizationService authorization) : Controller
{
    private const string FormPartial = "_ChangeOrgUnit";

    [HttpGet]
    public async Task<IActionResult> Change(OrgUnitOwnedKind kind, long id, string? returnUrl, CancellationToken cancellationToken)
    {
        if (await LoadAsync(kind, id, returnUrl, cancellationToken) is { } fail)
            return fail;

        return this.ModalOrView(FormPartial, ViewData.Model!);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Change))]
    public async Task<IActionResult> ChangeAsync(OrgUnitOwnedKind kind, long id, string? returnUrl, string? orgUnitKey, CancellationToken cancellationToken)
    {
        if (await LoadAsync(kind, id, returnUrl, cancellationToken) is { } fail)
            return fail;

        var model = (ChangeOrgUnitViewModel)ViewData.Model!;
        if (!await choices.IsAllowedAsync(Resource(kind), orgUnitKey, AllowPublic(kind), cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "واحد سازمانی انتخاب‌شده مجاز نیست.");
            return this.ModalOrView(FormPartial, model with { Selected = orgUnitKey });
        }

        var result = await sender.Send(new ChangeOrgUnitCommand(kind, id, orgUnitKey), cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "تغییر واحد انجام نشد.");
            return this.ModalOrView(FormPartial, model);
        }

        TempData["Success"] = result.Message;
        return this.ModalOrRedirect(model.BackUrl);
    }

    public static string Resource(OrgUnitOwnedKind kind) => kind switch
    {
        OrgUnitOwnedKind.Template => MapperResources.Templates,
        OrgUnitOwnedKind.DataSource => MapperResources.DataSources,
        OrgUnitOwnedKind.MappingProfile => MapperResources.MappingProfiles,
        OrgUnitOwnedKind.Import => MapperResources.Imports,
        _ => MapperResources.DataRecords,
    };

    /// <summary>Definitions (templates, data sources, mapping profiles) may be public; imports and records may not.</summary>
    public static bool AllowPublic(OrgUnitOwnedKind kind) =>
        kind is OrgUnitOwnedKind.Template or OrgUnitOwnedKind.DataSource or OrgUnitOwnedKind.MappingProfile;

    /// <summary>Checks Edit on the resource and loads the row (visible to the user) into ViewData.Model.</summary>
    private async Task<IActionResult?> LoadAsync(OrgUnitOwnedKind kind, long id, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(kind))
            return NotFound();

        if (!(await authorization.AuthorizeAsync(User, null, [new PermissionRequirement(Resource(kind), WellKnownActions.Edit)])).Succeeded)
            return Forbid();

        if (await sender.Send(new GetOrgUnitOwnerQuery(kind, id), cancellationToken) is not { } row)
            return NotFound();

        var back = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
        ViewData.Model = new ChangeOrgUnitViewModel(
            row, back, await choices.OptionsAsync(Resource(kind), AllowPublic(kind), cancellationToken), row.OrgUnitKey,
            await choices.TitleAsync(row.OrgUnitKey, cancellationToken));
        return null;
    }
}

public sealed record ChangeOrgUnitViewModel(
    OrgUnitOwnerDto Row,
    string BackUrl,
    IReadOnlyList<OrgUnitOption> Options,
    string? Selected,
    string CurrentTitle);
