using Borc.DataMapper.Application.DataSources.CreateDataSource;
using Borc.DataMapper.Application.DataSources.DeleteDataSource;
using Borc.DataMapper.Application.DataSources.GetDataSource;
using Borc.DataMapper.Application.DataSources.ListDataSources;
using Borc.DataMapper.Application.DataSources.SetDataSourceActive;
using Borc.DataMapper.Application.DataSources.UpdateDataSource;
using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Web.ViewModels.DataSources;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

public sealed class DataSourcesController : Controller
{
    private readonly ISender _sender;

    public DataSourcesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListDataSourcesQuery(search, page, pageSize);

        var result = await _sender.Send(query, cancellationToken);

        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "خطا در دریافت منابع داده.");

            return View(new DataSourceIndexViewModel(
                query,
                new(Array.Empty<DataSourceListItemDto>(), 1, pageSize, 0)));
        }

        return View(new DataSourceIndexViewModel(query, result.Data));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateDataSourceCommand(string.Empty, string.Empty, DataSourceType.StaticList, null));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAsync(
        CreateDataSourceCommand command,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(command, cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "منبع داده ایجاد شد.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "ساخت منبع داده انجام نشد.");
        }

        return View(command);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDataSourceQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "منبع داده موردنظر پیدا نشد.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Code"] = result.Data.Code;
        ViewData["UsedBy"] = result.Data.UsedByFieldCount;

        return View(new UpdateDataSourceCommand(
            result.Data.Id,
            result.Data.Name,
            result.Data.SourceType,
            result.Data.ConfigJson));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAsync(
        UpdateDataSourceCommand command,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(command, cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "منبع داده ویرایش شد.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "ویرایش منبع داده انجام نشد.");
        }

        var current = await _sender.Send(new GetDataSourceQuery(command.Id), cancellationToken);
        ViewData["Code"] = current.Data?.Code ?? string.Empty;
        ViewData["UsedBy"] = current.Data?.UsedByFieldCount ?? 0;

        return View(command);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(
        long id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetDataSourceActiveCommand(id, isActive), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "وضعیت منبع داده تغییر کرد." : "تغییر وضعیت انجام نشد.");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteDataSourceCommand(id), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "منبع داده حذف شد." : "حذف منبع داده انجام نشد.");

        return RedirectToAction(nameof(Index));
    }
}