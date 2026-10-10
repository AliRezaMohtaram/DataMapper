using Acl.AspNetCore.Authorization;
using Acl.Core.Model;
using Borc.DataMapper.Web.Modules;
using Borc.DataMapper.Application.MappingProfiles.AddMappingRule;
using Borc.DataMapper.Application.MappingProfiles.CreateMappingProfile;
using Borc.DataMapper.Application.MappingProfiles.DeleteMappingProfile;
using Borc.DataMapper.Application.MappingProfiles.DeleteMappingRule;
using Borc.DataMapper.Application.MappingProfiles.GetMappingProfile;
using Borc.DataMapper.Application.MappingProfiles.GetMappingProfileLookups;
using Borc.DataMapper.Application.MappingProfiles.ListMappingProfiles;
using Borc.DataMapper.Application.MappingProfiles.SetMappingProfileStatus;
using Borc.DataMapper.Application.MappingProfiles.UpdateMappingProfile;
using Borc.DataMapper.Application.MappingProfiles.UpdateMappingRule;
using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Web.Mvc;
using Borc.DataMapper.Web.ViewModels.MappingProfiles;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

[RequirePermission(MapperResources.MappingProfiles, WellKnownActions.View)]
public sealed class MappingProfilesController : Controller
{
    private readonly ISender _sender;

    /// <summary>فرم‌های مشترک (مودال و صفحهٔ کامل).</summary>
    private const string ProfilePartial = "_ProfileForm";
    private const string RulePartial = "_RuleForm";

    public MappingProfilesController(ISender sender)
    {
        _sender = sender;
    }

    // ---------- فهرست ----------

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        long? templateVersionId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListMappingProfilesQuery(search, templateVersionId, page, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.Success || result.Data is null)
        {
            ModelState.AddResultErrors(result, "خطا در دریافت پروفایل‌های نگاشت.", Request);

            return View(new MappingProfileIndexViewModel(
                query,
                new(Array.Empty<MappingProfileListItemDto>(), 1, pageSize, 0)));
        }

        return View(new MappingProfileIndexViewModel(query, result.Data));
    }

    // ---------- ایجاد ----------

    [HttpGet]
    [RequirePermission(MapperResources.MappingProfiles, WellKnownActions.Create)]
    [OrgUnitField(MapperResources.MappingProfiles, allowPublic: true)]
    public async Task<IActionResult> Create(long? templateVersionId, CancellationToken cancellationToken)
    {
        await LoadVersionsAsync(cancellationToken);

        return this.ModalOrView(ProfilePartial, new CreateMappingProfileCommand(string.Empty, templateVersionId ?? 0, ImportSourceType.Excel));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.MappingProfiles, WellKnownActions.Create)]
    [OrgUnitField(MapperResources.MappingProfiles, allowPublic: true)]
    public async Task<IActionResult> CreateAsync(
        CreateMappingProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(command, cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "پروفایل نگاشت ایجاد شد؛ حالا قاعده‌ها را اضافه کنید.";
                return this.ModalOrRedirect(Url.Action(nameof(Detail), new { id = result.Data })!);
            }

            ModelState.AddResultErrors(result, "ساخت پروفایل انجام نشد.", Request);
        }

        await LoadVersionsAsync(cancellationToken);
        return this.ModalOrView(ProfilePartial, command);
    }

    // ---------- جزئیات ----------

    [HttpGet]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMappingProfileQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "پروفایل نگاشت موردنظر پیدا نشد.";
            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }

    // ---------- ویرایش ----------

    [HttpGet]
    [RequirePermission(MapperResources.MappingProfiles, WellKnownActions.Edit)]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMappingProfileQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "پروفایل نگاشت موردنظر پیدا نشد.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Target"] = $"{result.Data.TemplateName} — نسخه {result.Data.VersionNo}";

        return this.ModalOrView(ProfilePartial, new UpdateMappingProfileCommand(result.Data.Id, result.Data.Name, result.Data.SourceType));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.MappingProfiles, WellKnownActions.Edit)]
    public async Task<IActionResult> EditAsync(
        UpdateMappingProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(command, cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "پروفایل ویرایش شد.";
                return this.ModalOrRedirect(Url.Action(nameof(Detail), new { id = command.Id })!);
            }

            ModelState.AddResultErrors(result, "ویرایش پروفایل انجام نشد.", Request);
        }

        var current = await _sender.Send(new GetMappingProfileQuery(command.Id), cancellationToken);
        ViewData["Target"] = current.Data is null
            ? string.Empty
            : $"{current.Data.TemplateName} — نسخه {current.Data.VersionNo}";

        return this.ModalOrView(ProfilePartial, command);
    }

    // ---------- وضعیت و حذف ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.MappingProfiles, WellKnownActions.Edit)]
    public async Task<IActionResult> SetStatus(long id, bool isActive, string? returnTo, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetMappingProfileStatusCommand(id, isActive), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "وضعیت پروفایل تغییر کرد." : "تغییر وضعیت انجام نشد.");

        return returnTo == "detail"
            ? RedirectToAction(nameof(Detail), new { id })
            : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.MappingProfiles, WellKnownActions.Delete)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteMappingProfileCommand(id), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "پروفایل حذف شد." : "حذف پروفایل انجام نشد.");

        return RedirectToAction(nameof(Index));
    }

    // ---------- قاعده‌ها ----------

    [HttpGet]
    [RequirePermission(MapperResources.MappingProfiles, WellKnownActions.Edit)]
    public async Task<IActionResult> AddRule(long profileId, CancellationToken cancellationToken)
    {
        if (!await LoadRuleFormAsync(profileId, null, cancellationToken))
            return RedirectToAction(nameof(Index));

        return this.ModalOrView(RulePartial, new AddMappingRuleCommand(profileId, string.Empty, 0, NextSortOrder()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.MappingProfiles, WellKnownActions.Edit)]
    public async Task<IActionResult> AddRuleAsync(
        AddMappingRuleCommand command,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(command, cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "قاعده اضافه شد.";
                return this.ModalOrRedirect(Url.Action(nameof(Detail), new { id = command.MappingProfileId })!);
            }

            ModelState.AddResultErrors(result, "افزودن قاعده انجام نشد.", Request);
        }

        if (!await LoadRuleFormAsync(command.MappingProfileId, null, cancellationToken))
            return RedirectToAction(nameof(Index));

        return this.ModalOrView(RulePartial, command);
    }

    [HttpGet]
    [RequirePermission(MapperResources.MappingProfiles, WellKnownActions.Edit)]
    public async Task<IActionResult> EditRule(long profileId, long id, CancellationToken cancellationToken)
    {
        if (!await LoadRuleFormAsync(profileId, id, cancellationToken))
            return RedirectToAction(nameof(Index));

        var rule = ((MappingProfileDetailDto)ViewData["Profile"]!).Rules.FirstOrDefault(r => r.Id == id);

        if (rule is null)
        {
            TempData["Error"] = "قاعدهٔ موردنظر پیدا نشد.";
            return RedirectToAction(nameof(Detail), new { id = profileId });
        }

        ViewData["ProfileId"] = profileId;
        ViewData["SourceColumn"] = rule.SourceColumn;

        return this.ModalOrView(RulePartial, new UpdateMappingRuleCommand(rule.Id, rule.TargetFieldId, rule.SortOrder));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.MappingProfiles, WellKnownActions.Edit)]
    public async Task<IActionResult> EditRuleAsync(
        long profileId,
        UpdateMappingRuleCommand command,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(command, cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "قاعده ویرایش شد.";
                return this.ModalOrRedirect(Url.Action(nameof(Detail), new { id = profileId })!);
            }

            ModelState.AddResultErrors(result, "ویرایش قاعده انجام نشد.", Request);
        }

        if (!await LoadRuleFormAsync(profileId, command.Id, cancellationToken))
            return RedirectToAction(nameof(Index));

        var rule = ((MappingProfileDetailDto)ViewData["Profile"]!).Rules.FirstOrDefault(r => r.Id == command.Id);
        ViewData["ProfileId"] = profileId;
        ViewData["SourceColumn"] = rule?.SourceColumn ?? string.Empty;

        return this.ModalOrView(RulePartial, command);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.MappingProfiles, WellKnownActions.Edit)]
    public async Task<IActionResult> DeleteRule(long id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteMappingRuleCommand(id), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "قاعده حذف شد." : "حذف قاعده انجام نشد.");

        return result.Success
            ? RedirectToAction(nameof(Detail), new { id = result.Data })
            : RedirectToAction(nameof(Index));
    }

    // ---------- کمکی ----------

    private async Task LoadVersionsAsync(CancellationToken ct)
    {
        var lookups = await _sender.Send(new GetMappingProfileLookupsQuery(), ct);

        ViewData["Versions"] = lookups.Data?.Versions
            ?? (IReadOnlyList<ProfileVersionOption>)Array.Empty<ProfileVersionOption>();
    }

    /// <summary>پروفایل، فیلدهای نسخه و فیلدهای قبلاً نگاشت‌شده را برای فرم قاعده در ViewData می‌گذارد.</summary>
    private async Task<bool> LoadRuleFormAsync(long profileId, long? editingRuleId, CancellationToken ct)
    {
        var profile = await _sender.Send(new GetMappingProfileQuery(profileId), ct);

        if (!profile.Success || profile.Data is null)
        {
            TempData["Error"] = profile.Message ?? "پروفایل نگاشت موردنظر پیدا نشد.";
            return false;
        }

        var lookups = await _sender.Send(new GetMappingProfileLookupsQuery(profile.Data.TemplateVersionId), ct);

        ViewData["Profile"] = profile.Data;
        ViewData["Fields"] = lookups.Data?.Fields
            ?? (IReadOnlyList<ProfileFieldOption>)Array.Empty<ProfileFieldOption>();

        // فیلدهایی که قاعده دارند (به‌جز قاعدهٔ در حال ویرایش) غیرفعال نمایش داده می‌شوند.
        ViewData["MappedFieldIds"] = profile.Data.Rules
            .Where(r => r.Id != editingRuleId)
            .Select(r => r.TargetFieldId)
            .ToHashSet();

        ViewData["NextSort"] = profile.Data.Rules.Count == 0 ? 0 : profile.Data.Rules.Max(r => r.SortOrder) + 1;

        return true;
    }

    private int NextSortOrder() => ViewData["NextSort"] is int n ? n : 0;
}
