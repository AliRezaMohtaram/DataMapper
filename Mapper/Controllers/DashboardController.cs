using Borc.DataMapper.Application.Dashboard.GetDashboard;
using Borc.DataMapper.Application.Imports.ListImportBatches;
using Borc.DataMapper.Web.ViewModels.Dashboard;
using Borc.DataMapper.Web.Mvc;
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
            ModelState.AddResultErrors(result, "خطا در دریافت اطلاعات داشبورد.", Request);

            return View(new DashboardViewModel(new DashboardDto(
                new DashboardKpis(0, 0, 0, 0, 0, 0, 0, 0, null),
                Array.Empty<ImportBatchListItemDto>(),
                Array.Empty<DashboardTemplateDto>(),
                Array.Empty<DashboardDataSourceDto>())));
        }

        return View(new DashboardViewModel(result.Data));
    }
}
