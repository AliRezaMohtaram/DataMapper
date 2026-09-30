using Borc.DataMapper.Application.Templates.CreateTemplate;
using MediatR;
using Microsoft.AspNetCore.DataProtection.KeyManagement.Internal;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

public sealed class TemplatesController : Controller
{
    private readonly ISender _sender;

    public TemplatesController(ISender sender)
    {
        _sender = sender;
    }
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
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

        return RedirectToAction(nameof(Create));
    }
}