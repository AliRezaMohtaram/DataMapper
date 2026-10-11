using Borc.DataMapper.Application.Templates.ChangeTemplateStatus;
using Acl.AspNetCore.Authorization;
using Acl.Core.Model;
using Borc.DataMapper.Web.Modules;
using Borc.DataMapper.Application.Templates.CreateTemplate;
using Borc.DataMapper.Application.Templates.DeleteTemplate;
using Borc.DataMapper.Application.Templates.GetTemplate;
using Borc.DataMapper.Application.Templates.ListTemplates;
using Borc.DataMapper.Application.Templates.UpdateTemplate;
using Borc.DataMapper.Domain.Templates;
using Borc.DataMapper.Web.Mvc;
using Borc.DataMapper.Web.ViewModels.Templates;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

[RequirePermission(MapperResources.Templates, WellKnownActions.View)]
public sealed class TemplatesController : Controller
{
    private readonly ISender _sender;
    private readonly OrgChart.Core.Chart.IOrgChartReader _chart;

    /// <summary>فرم مشترک ایجاد/ویرایش؛ هم در مودال و هم در صفحهٔ کامل استفاده می‌شود.</summary>
    private const string FormPartial = "_TemplateForm";

    public TemplatesController(ISender sender, OrgChart.Core.Chart.IOrgChartReader chart)
    {
        _sender = sender;
        _chart = chart;
    }

    /// <summary>واحدهای انتخاب‌شده باید در چارت باشند؛ کلید با نگارش چارت ذخیره می‌شود.</summary>
    private async Task<IReadOnlyList<string>?> ValidUnitsAsync(TemplateFormViewModel model, CancellationToken ct)
    {
        var chart = await _chart.GetSnapshotAsync(ct);
        var keys = new List<string>();
        foreach (var key in model.OrgUnitKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (chart.FindUnit(key) is not { } unit)
            {
                ModelState.AddModelError(string.Empty, "واحد انتخاب‌شده در چارت سازمانی پیدا نشد.");
                return null;
            }

            keys.Add(unit.Key);
        }

        return keys;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        TemplateStatus? status,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListTemplatesQuery(search, status, page, pageSize);

        var result = await _sender.Send(query, cancellationToken);

        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Message ?? "خطا در دریافت لیست قالب‌ها.");

            return View(new TemplateIndexViewModel(
                query,
                new(Array.Empty<TemplateListItemDto>(), 1, pageSize, 0)));
        }

        return View(new TemplateIndexViewModel(query, result.Data));
    }

    [HttpGet]
    [RequirePermission(MapperResources.Templates, WellKnownActions.Create)]
    public IActionResult Create()
    {
        return this.ModalOrView(FormPartial, new TemplateFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Templates, WellKnownActions.Create)]
    public async Task<IActionResult> CreateAsync(
        TemplateFormViewModel model,
        CancellationToken cancellationToken)
    {
        var units = await ValidUnitsAsync(model, cancellationToken);
        if (ModelState.IsValid && units is not null)
        {
            var result = await _sender.Send(
                new CreateTemplateCommand(null, model.Name!.Trim(), model.Description),
                cancellationToken);

            if (result.Success)
            {
                await _sender.Send(new Application.Templates.SetTemplateUnits.SetTemplateUnitsCommand(result.Data, units), cancellationToken);
                TempData["Success"] = result.Message ?? "قالب ایجاد شد. حالا نسخه و فیلدهای آن را تعریف کنید.";

                // قدم بعدی ساخت قالب، تعریف نسخه و فیلدهاست؛ به صفحهٔ جزئیات می‌رویم
                return this.ModalOrRedirect(Url.Action(nameof(Detail), new { id = result.Data })!);
            }

            ModelState.AddResultErrors(result, "ساخت قالب انجام نشد.", Request);
        }

        return this.ModalOrView(FormPartial, model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Templates, WellKnownActions.Delete)]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new DeleteTemplateCommand(id),
            cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "قالب حذف شد." : "حذف قالب انجام نشد.");

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Detail(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new Application.Templates.GetTemplate.GetTemplateQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "قالب موردنظر پیدا نشد.";
            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }

    [HttpGet]
    [RequirePermission(MapperResources.Templates, WellKnownActions.Edit)]
    public async Task<IActionResult> Edit(
        long id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new Application.Templates.GetTemplate.GetTemplateQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "قالب موردنظر پیدا نشد.";
            return RedirectToAction(nameof(Index));
        }

        return this.ModalOrView(FormPartial, new TemplateFormViewModel
        {
            Id = result.Data.Id,
            Code = result.Data.Code,
            Name = result.Data.Name,
            Description = result.Data.Description,
            OrgUnitKeys = result.Data.OrgUnitKeys.ToList(),
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Templates, WellKnownActions.Edit)]
    public async Task<IActionResult> EditAsync(
        TemplateFormViewModel model,
        CancellationToken cancellationToken)
    {
        var units = await ValidUnitsAsync(model, cancellationToken);
        if (ModelState.IsValid && units is not null)
        {
            var result = await _sender.Send(
                new UpdateTemplateCommand(model.Id, model.Name!.Trim(), model.Description), cancellationToken);

            if (result.Success)
            {
                await _sender.Send(new Application.Templates.SetTemplateUnits.SetTemplateUnitsCommand(model.Id, units), cancellationToken);
                TempData["Success"] = result.Message ?? "قالب ویرایش شد.";
                return this.ModalOrRedirect(Url.IsLocalUrl(model.ReturnUrl)
                    ? model.ReturnUrl!
                    : Url.Action(nameof(Detail), new { id = model.Id })!);
            }

            ModelState.AddResultErrors(result, "ویرایش قالب انجام نشد.", Request);
        }

        // نمایش مجدد فرم: کد قالب برای نمایش فقط‌خواندنی دوباره خوانده می‌شود
        var current = await _sender.Send(new Application.Templates.GetTemplate.GetTemplateQuery(model.Id), cancellationToken);
        model.Code = current.Data?.Code;

        return this.ModalOrView(FormPartial, model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Templates, WellKnownActions.Edit)]
    public async Task<IActionResult> ChangeStatus(
        long id,
        TemplateStatus status,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ChangeTemplateStatusCommand(id, status),
            cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "وضعیت قالب تغییر کرد." : "تغییر وضعیت انجام نشد.");

        return RedirectToAction(nameof(Detail), new { id });
    }
}