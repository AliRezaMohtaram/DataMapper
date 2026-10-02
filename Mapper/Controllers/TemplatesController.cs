using Borc.DataMapper.Application.Templates.CreateTemplate;
using Borc.DataMapper.Application.Templates.DeleteTemplate;
using Borc.DataMapper.Application.Templates.ListTemplates;
using Borc.DataMapper.Domain.Templates;
using Borc.DataMapper.Web.ViewModels.Templates;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

public sealed class TemplatesController : Controller
{
    private readonly ISender _sender;

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
        return View(new CreateTemplateCommand(string.Empty, string.Empty, null));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAsync(
        CreateTemplateCommand command,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(command);

        var result = await _sender.Send(
            command,
            cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Message!);

            return View(command);
        }

        return RedirectToAction(nameof(Index));
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
}