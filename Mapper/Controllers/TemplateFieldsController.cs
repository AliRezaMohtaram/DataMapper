using Borc.DataMapper.Application.TemplateFields.AddFieldAlias;
using Borc.DataMapper.Application.TemplateFields.CreateTemplateField;
using Borc.DataMapper.Application.TemplateFields.DeleteFieldAlias;
using Borc.DataMapper.Application.TemplateFields.DeleteTemplateField;
using Borc.DataMapper.Application.TemplateFields.GetFieldLookups;
using Borc.DataMapper.Application.TemplateFields.GetTemplateField;
using Borc.DataMapper.Application.TemplateFields.UpdateTemplateField;
using Borc.DataMapper.Application.TemplateVersions.GetTemplateVersion;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;
using Borc.DataMapper.Web.Mvc;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

public sealed class TemplateFieldsController : Controller
{
    private readonly ISender _sender;

    /// <summary>فرم مشترک ایجاد/ویرایش فیلد (همراه با نام‌های مستعار)؛ هم در مودال و هم در صفحهٔ کامل.</summary>
    private const string FormPartial = "_FieldForm";

    public TemplateFieldsController(ISender sender)
    {
        _sender = sender;
    }

    // ---------- ساخت فیلد ----------

    [HttpGet]
    public async Task<IActionResult> Create(
        long templateVersionId,
        CancellationToken cancellationToken)
    {
        if (!await LoadDraftVersionAsync(templateVersionId, cancellationToken))
            return RedirectToAction("Detail", "TemplateVersions", new { id = templateVersionId });

        await LoadLookupsAsync(cancellationToken);

        return this.ModalOrView(FormPartial, new CreateTemplateFieldCommand(
            templateVersionId,
            string.Empty,
            string.Empty,
            FieldDataType.Text,
            "VARCHAR2",
            false,
            null, null, null, null, null, null, null, null));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAsync(
        CreateTemplateFieldCommand command,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(command, cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "فیلد ایجاد شد.";
                return this.ModalOrRedirect(Url.Action("Detail", "TemplateVersions", new { id = command.TemplateVersionId })!);
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "ساخت فیلد انجام نشد.");
        }

        if (!await LoadDraftVersionAsync(command.TemplateVersionId, cancellationToken))
            return RedirectToAction("Detail", "TemplateVersions", new { id = command.TemplateVersionId });

        await LoadLookupsAsync(cancellationToken);

        return this.ModalOrView(FormPartial, command);
    }

    // ---------- ویرایش فیلد ----------

    [HttpGet]
    public async Task<IActionResult> Edit(
        long id,
        string? tab,
        CancellationToken cancellationToken)
    {
        ViewData["Tab"] = tab;
        return await EditFormAsync(id, cancellationToken);
    }

    /// <summary>فرم ویرایش با داده‌های تازه از دیتابیس (پس از ویرایش alias هم استفاده می‌شود).</summary>
    private async Task<IActionResult> EditFormAsync(long id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTemplateFieldQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "فیلد موردنظر پیدا نشد.";
            return RedirectToAction("Index", "Templates");
        }

        var f = result.Data;

        if (f.VersionStatus != TemplateVersionStatus.Draft)
        {
            TempData["Error"] = "فقط فیلدهای نسخه پیش‌نویس قابل ویرایش‌اند.";
            return RedirectToAction("Detail", "TemplateVersions", new { id = f.TemplateVersionId });
        }

        ViewData["Info"] = f;
        await LoadLookupsAsync(cancellationToken);

        return this.ModalOrView(FormPartial, new UpdateTemplateFieldCommand(
            f.Id, f.Label, f.DataType, f.DbType, f.IsRequired, f.SortOrder,
            f.Length, f.Precision, f.Scale, f.Regex, f.DefaultValue, f.DataSourceId, f.ConfigJson));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAsync(
        UpdateTemplateFieldCommand command,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(command, cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "فیلد ویرایش شد.";
                return this.ModalOrRedirect(Url.Action("Detail", "TemplateVersions", new { id = result.Data })!);
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "ویرایش فیلد انجام نشد.");
        }

        var info = await _sender.Send(new GetTemplateFieldQuery(command.Id), cancellationToken);

        if (!info.Success || info.Data is null)
        {
            TempData["Error"] = info.Message ?? "فیلد موردنظر پیدا نشد.";
            return RedirectToAction("Index", "Templates");
        }

        ViewData["Info"] = info.Data;
        await LoadLookupsAsync(cancellationToken);

        return this.ModalOrView(FormPartial, command);
    }

    // ---------- حذف فیلد ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteTemplateFieldCommand(id), cancellationToken);

        if (!result.Success)
        {
            TempData["Error"] = result.Message ?? "حذف فیلد انجام نشد.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["Success"] = result.Message ?? "فیلد حذف شد.";
        return RedirectToAction("Detail", "TemplateVersions", new { id = result.Data });
    }

    // ---------- aliasها ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAlias(
        long templateFieldId,
        string? alias,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AddFieldAliasCommand(templateFieldId, alias ?? string.Empty),
            cancellationToken);

        var message = result.Message ?? (result.Success ? "نام مستعار اضافه شد." : "افزودن نام مستعار انجام نشد.");

        // در مودال، همان فرم با تب «نام‌های مستعار» دوباره رندر می‌شود
        if (Request.IsModalRequest())
            return await AliasesTabAsync(templateFieldId, message, result.Success, cancellationToken);

        TempData[result.Success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Edit), new { id = templateFieldId, tab = "aliases" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAlias(
        long id,
        long fieldId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteFieldAliasCommand(id), cancellationToken);

        var message = result.Message ?? (result.Success ? "نام مستعار حذف شد." : "حذف نام مستعار انجام نشد.");

        if (Request.IsModalRequest())
            return await AliasesTabAsync(fieldId, message, result.Success, cancellationToken);

        TempData[result.Success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Edit), new { id = fieldId, tab = "aliases" });
    }

    // ---------- کمکی‌ها ----------

    private Task<IActionResult> AliasesTabAsync(long fieldId, string message, bool success, CancellationToken ct)
    {
        ViewData["Tab"] = "aliases";
        ViewData["Flash"] = message;
        ViewData["FlashTone"] = success ? "success" : "danger";
        return EditFormAsync(fieldId, ct);
    }

    private async Task<bool> LoadDraftVersionAsync(long versionId, CancellationToken ct)
    {
        var result = await _sender.Send(new GetTemplateVersionQuery(versionId), ct);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "نسخه موردنظر پیدا نشد.";
            return false;
        }

        if (result.Data.Status != TemplateVersionStatus.Draft)
        {
            TempData["Error"] = "فقط نسخه پیش‌نویس قابل ویرایش است.";
            return false;
        }

        ViewData["Version"] = result.Data;
        return true;
    }

    private async Task LoadLookupsAsync(CancellationToken ct)
    {
        var result = await _sender.Send(new GetFieldLookupsQuery(), ct);

        ViewData["Lookups"] = result.Data
            ?? new FieldLookupsDto(
                Array.Empty<DataSourceOption>(),
                Array.Empty<PredefinedRegexOption>());
    }
}