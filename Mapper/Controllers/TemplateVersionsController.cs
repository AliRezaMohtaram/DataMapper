using Borc.DataMapper.Application.TemplateVersions.ArchiveTemplateVersion;
using Acl.AspNetCore.Authorization;
using Acl.Core.Model;
using Borc.DataMapper.Web.Modules;
using Borc.DataMapper.Application.TemplateVersions.CreateTemplateVersion;
using Borc.DataMapper.Application.TemplateVersions.DeleteTemplateVersion;
using Borc.DataMapper.Application.TemplateVersions.GetTemplateVersion;
using Borc.DataMapper.Application.TemplateVersions.PublishTemplateVersion;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

[RequirePermission(MapperResources.Templates, WellKnownActions.View)]
public sealed class TemplateVersionsController : Controller
{
    private readonly ISender _sender;

    public TemplateVersionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> Detail(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTemplateVersionQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "نسخه موردنظر پیدا نشد.";
            return RedirectToAction("Index", "Templates");
        }

        return View(result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Templates, WellKnownActions.Edit)]
    public async Task<IActionResult> Create(
        long templateId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateTemplateVersionCommand(templateId),
            cancellationToken);

        if (!result.Success)
        {
            TempData["Error"] = result.Message ?? "ساخت نسخه انجام نشد.";
            return RedirectToAction("Detail", "Templates", new { id = templateId });
        }

        TempData["Success"] = result.Message ?? "نسخه ایجاد شد.";
        return RedirectToAction(nameof(Detail), new { id = result.Data });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Templates, WellKnownActions.Approve)]
    public async Task<IActionResult> Publish(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new PublishTemplateVersionCommand(id), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "نسخه منتشر شد." : "انتشار انجام نشد.");

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Templates, WellKnownActions.Approve)]
    public async Task<IActionResult> Archive(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ArchiveTemplateVersionCommand(id), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "نسخه بایگانی شد." : "بایگانی انجام نشد.");

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Templates, WellKnownActions.Delete)]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteTemplateVersionCommand(id), cancellationToken);

        if (!result.Success)
        {
            TempData["Error"] = result.Message ?? "حذف نسخه انجام نشد.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        TempData["Success"] = result.Message ?? "نسخه حذف شد.";
        return RedirectToAction("Detail", "Templates", new { id = result.Data });
    }
}