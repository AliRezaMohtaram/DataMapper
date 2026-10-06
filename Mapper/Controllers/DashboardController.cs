using Borc.DataMapper.Application.Dashboard.GetDashboard;
using Borc.DataMapper.Application.Imports.ListImportBatches;
using Borc.DataMapper.Web.ViewModels.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

public sealed class DashboardController : Controller
{
    private readonly ISender _sender;

    public DashboardController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDashboardQuery(), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "خطا در دریافت اطلاعات داشبورد.");

            return View(new DashboardViewModel(new DashboardDto(
                new DashboardKpis(0, 0, 0, 0, 0, 0, 0, 0, null),
                Array.Empty<ImportBatchListItemDto>(),
                Array.Empty<DashboardTemplateDto>(),
                Array.Empty<DashboardDataSourceDto>())));
        }

        return View(new DashboardViewModel(result.Data));
    }
}
