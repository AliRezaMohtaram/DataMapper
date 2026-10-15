using Borc.DataMapper.Application.Dashboard.GetDashboard;
using Borc.DataMapper.Application.Imports.ListImportBatches;
using Borc.DataMapper.Web.ViewModels.Dashboard;
using Borc.DataMapper.Web.Mvc;
using Acl.Core.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

// Landing page after sign-in: open to every signed-in user; its shortcuts are filtered by permission.
public sealed class DashboardController : Controller
{
    private readonly ISender _sender;
    private readonly IAccessService _access;

    public DashboardController(ISender sender, IAccessService access)
    {
        _sender = sender;
        _access = access;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // the cards follow the user's permissions (user: not every template and data source for everyone)
        var access = await _access.GetCurrentUserAccessAsync(cancellationToken);
        var rights = new DashboardRights(
            access.IsAllowed("Mapper.Templates", "View"),
            access.IsAllowed("Mapper.DataSources", "View"),
            access.IsAllowed("Mapper.Imports", "View"));
        var result = await _sender.Send(new GetDashboardQuery(new DashboardAccess(rights.Templates, rights.DataSources)), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            ModelState.AddResultErrors(result, "خطا در دریافت اطلاعات داشبورد.", Request);

            return View(new DashboardViewModel(new DashboardDto(
                new DashboardKpis(0, 0, 0, 0, 0, 0, 0, 0, null),
                Array.Empty<ImportBatchListItemDto>(),
                Array.Empty<DashboardTemplateDto>(),
                Array.Empty<DashboardDataSourceDto>())) { Rights = rights });
        }

        return View(new DashboardViewModel(result.Data) { Rights = rights });
    }
}
