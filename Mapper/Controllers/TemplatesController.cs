using Borc.DataMapper.Application.Templates.ChangeTemplateStatus;
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

public sealed class TemplatesController : Controller
{
    private readonly ISender _sender;

    /// <summary>فرم مشترک ایجاد/ویرایش؛ هم در مودال و هم در صفحهٔ کامل استفاده می‌شود.</summary>
    private const string FormPartial = "_TemplateForm";

    public TemplatesController(ISender sender)
    {
        _sender = sender;
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
    public IActionResult Create()
    {
        return this.ModalOrView(FormPartial, new TemplateFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAsync(
        TemplateFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(
                new CreateTemplateCommand(model.Code!.Trim(), model.Name!.Trim(), model.Description),
                cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "قالب ایجاد شد. حالا نسخه و فیلدهای آن را تعریف کنید.";

                // قدم بعدی ساخت قالب، تعریف نسخه و فیلدهاست؛ به صفحهٔ جزئیات می‌رویم
                return this.ModalOrRedirect(Url.Action(nameof(Detail), new { id = result.Data })!);
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "ساخت قالب انجام نشد.");
        }

        return this.ModalOrView(FormPartial, model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAsync(
        TemplateFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(
                new UpdateTemplateCommand(model.Id, model.Name!.Trim(), model.Description), cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "قالب ویرایش شد.";
                return this.ModalOrRedirect(Url.IsLocalUrl(model.ReturnUrl)
                    ? model.ReturnUrl!
                    : Url.Action(nameof(Detail), new { id = model.Id })!);
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "ویرایش قالب انجام نشد.");
        }

        // نمایش مجدد فرم: کد قالب برای نمایش فقط‌خواندنی دوباره خوانده می‌شود
        var current = await _sender.Send(new Application.Templates.GetTemplate.GetTemplateQuery(model.Id), cancellationToken);
        model.Code = current.Data?.Code;

        return this.ModalOrView(FormPartial, model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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