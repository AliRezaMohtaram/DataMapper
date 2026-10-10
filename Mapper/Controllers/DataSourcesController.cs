using Acl.AspNetCore.Authorization;
using Acl.Core.Model;
using Borc.DataMapper.Web.Modules;
using Borc.DataMapper.Application.DataSources.CreateDataSource;
using Borc.DataMapper.Application.DataSources.DeleteDataSource;
using Borc.DataMapper.Application.DataSources.GetDataSource;
using Borc.DataMapper.Application.DataSources.GetDataSourceLookups;
using Borc.DataMapper.Application.DataSources.ImportDataSourceFile;
using Borc.DataMapper.Application.DataSources.ListDataSources;
using Borc.DataMapper.Application.DataSources.PreviewDataSourceFile;
using Borc.DataMapper.Application.DataSources.SearchDataSourceOptions;
using Borc.DataMapper.Application.DataSources.SetDataSourceActive;
using Borc.DataMapper.Application.DataSources.UpdateDataSource;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.DataSources;
using Borc.DataMapper.Web.Mvc;
using Borc.DataMapper.Web.ViewModels.DataSources;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

public sealed class DataSourcesController : Controller
{
    private readonly ISender _sender;

    /// <summary>فرم مشترک ایجاد/ویرایش؛ هم در مودال و هم در صفحهٔ کامل استفاده می‌شود.</summary>
    private const string FormPartial = "_DataSourceForm";

    public DataSourcesController(ISender sender)
    {
        _sender = sender;
    }

    // ---------- فهرست ----------

    [HttpGet]
    [RequirePermission(MapperResources.DataSources, WellKnownActions.View)]
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
            ModelState.AddResultErrors(result, "خطا در دریافت منابع داده.", Request);

            return View(new DataSourceIndexViewModel(
                query,
                new(Array.Empty<DataSourceListItemDto>(), 1, pageSize, 0)));
        }

        return View(new DataSourceIndexViewModel(query, result.Data));
    }

    // ---------- ایجاد ----------

    [HttpGet]
    [RequirePermission(MapperResources.DataSources, WellKnownActions.Create)]
    [OrgUnitField(MapperResources.DataSources, allowPublic: true)]
    public async Task<IActionResult> Create(DataSourceType? type, CancellationToken cancellationToken)
    {
        var vm = new DataSourceFormViewModel { SourceType = type ?? DataSourceType.StaticList };
        await FillLookupsAsync(vm, cancellationToken);

        return this.ModalOrView(FormPartial, vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.DataSources, WellKnownActions.Create)]
    [OrgUnitField(MapperResources.DataSources, allowPublic: true)]
    public async Task<IActionResult> CreateAsync(
        DataSourceFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(new CreateDataSourceCommand(
                model.Code ?? string.Empty, model.Name, model.SourceType, model.ItemsText,
                model.TemplateId, model.ValueKey, model.DisplayKey,
                model.ApiUrl, model.ApiItemsPath, model.ApiValueKey, model.ApiDisplayKey), cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "منبع داده ایجاد شد.";

                // منبع فایل بدون فایل خالی است؛ مستقیم به صفحهٔ بارگذاری می‌رویم.
                return this.ModalOrRedirect(model.SourceType == DataSourceType.File
                    ? Url.Action(nameof(UploadFile), new { id = result.Data })!
                    : Url.Action(nameof(Detail), new { id = result.Data })!);
            }

            ModelState.AddResultErrors(result, "ساخت منبع داده انجام نشد.", Request);
        }

        await FillLookupsAsync(model, cancellationToken);
        return this.ModalOrView(FormPartial, model);
    }

    // ---------- جزئیات ----------

    [HttpGet]
    [RequirePermission(MapperResources.DataSources, WellKnownActions.View)]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDataSourceQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "منبع داده موردنظر پیدا نشد.";
            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }

    // ---------- ویرایش ----------

    [HttpGet]
    [RequirePermission(MapperResources.DataSources, WellKnownActions.Edit)]
    public async Task<IActionResult> Edit(long id, string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDataSourceQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "منبع داده موردنظر پیدا نشد.";
            return RedirectToAction(nameof(Index));
        }

        var d = result.Data;

        var vm = new DataSourceFormViewModel
        {
            Id = d.Id,
            Code = d.Code,
            Name = d.Name,
            SourceType = d.SourceType,
            ItemsText = d.ItemsText,
            TemplateId = d.TemplateId,
            ValueKey = d.ValueKey,
            DisplayKey = d.DisplayKey,
            ApiUrl = d.ApiUrl,
            ApiItemsPath = d.ApiItemsPath,
            ApiValueKey = d.ApiValueKey,
            ApiDisplayKey = d.ApiDisplayKey,
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
        };

        ViewData["UsedBy"] = d.UsedByFieldCount;
        await FillLookupsAsync(vm, cancellationToken);

        return this.ModalOrView(FormPartial, vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.DataSources, WellKnownActions.Edit)]
    public async Task<IActionResult> EditAsync(
        DataSourceFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            var result = await _sender.Send(new UpdateDataSourceCommand(
                model.Id, model.Name, model.ItemsText,
                model.TemplateId, model.ValueKey, model.DisplayKey,
                model.ApiUrl, model.ApiItemsPath, model.ApiValueKey, model.ApiDisplayKey), cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "منبع داده ویرایش شد.";
                return this.ModalOrRedirect(Url.IsLocalUrl(model.ReturnUrl)
                    ? model.ReturnUrl!
                    : Url.Action(nameof(Detail), new { id = model.Id })!);
            }

            ModelState.AddResultErrors(result, "ویرایش منبع داده انجام نشد.", Request);
        }

        // کد و نوع در فرم ارسال نمی‌شوند؛ از منبع داده دوباره خوانده می‌شوند.
        var current = await _sender.Send(new GetDataSourceQuery(model.Id), cancellationToken);

        if (current.Data is not null)
        {
            model.Code = current.Data.Code;
            model.SourceType = current.Data.SourceType;
            ViewData["UsedBy"] = current.Data.UsedByFieldCount;
        }

        await FillLookupsAsync(model, cancellationToken);
        return this.ModalOrView(FormPartial, model);
    }

    // ---------- وضعیت و حذف ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.DataSources, WellKnownActions.Edit)]
    public async Task<IActionResult> SetActive(
        long id,
        bool isActive,
        string? returnTo,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetDataSourceActiveCommand(id, isActive), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "وضعیت منبع داده تغییر کرد." : "تغییر وضعیت انجام نشد.");

        return returnTo == "detail"
            ? RedirectToAction(nameof(Detail), new { id })
            : RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.DataSources, WellKnownActions.Delete)]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteDataSourceCommand(id), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "منبع داده حذف شد." : "حذف منبع داده انجام نشد.");

        return RedirectToAction(nameof(Index));
    }

    // ---------- بارگذاری فایل (Excel / CSV) ----------

    [HttpGet]
    [RequirePermission(MapperResources.DataSources, WellKnownActions.Edit)]
    public async Task<IActionResult> UploadFile(long id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDataSourceQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "منبع داده موردنظر پیدا نشد.";
            return RedirectToAction(nameof(Index));
        }

        if (result.Data.SourceType != DataSourceType.File)
        {
            TempData["Error"] = "فقط منبع داده‌ای از نوع «فایل» فایل می‌پذیرد.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        return View(result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ImportLimits.MaxFileBytes + 1024 * 64)]
    [RequirePermission(MapperResources.DataSources, WellKnownActions.Edit)]
    public async Task<IActionResult> UploadFileAsync(
        long id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "فایلی انتخاب نشده است.";
            return RedirectToAction(nameof(UploadFile), new { id });
        }

        if (file.Length > ImportLimits.MaxFileBytes)
        {
            TempData["Error"] = "حجم فایل بیش از 10 مگابایت است.";
            return RedirectToAction(nameof(UploadFile), new { id });
        }

        byte[] content;

        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, cancellationToken);
            content = ms.ToArray();
        }

        var result = await _sender.Send(
            new PreviewDataSourceFileCommand(id, file.FileName, content), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "فایل خوانده نشد.";
            return RedirectToAction(nameof(UploadFile), new { id });
        }

        ViewData["DataSourceId"] = id;
        return View("MapFile", result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.DataSources, WellKnownActions.Edit)]
    public async Task<IActionResult> ImportFile(
        long id,
        string token,
        int valueColumnIndex,
        int? displayColumnIndex,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ImportDataSourceFileCommand(id, token, valueColumnIndex, displayColumnIndex), cancellationToken);

        if (!result.Success)
        {
            TempData["Error"] = result.Message ?? "ثبت گزینه‌ها انجام نشد.";
            return RedirectToAction(nameof(UploadFile), new { id });
        }

        TempData["Success"] = result.Message ?? "گزینه‌ها ثبت شد.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ---------- گزینه‌ها (JSON) ----------

    /// <summary>
    /// جستجوی گزینه‌ها برای Combobox فرم ورود داده و «نمایش نمونه» صفحهٔ منبع.
    /// با value فقط همان مقدار دقیق برمی‌گردد.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Options(
        long id,
        string? q,
        string? value,
        int take = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new SearchDataSourceOptionsQuery(id, q, take, value), cancellationToken);

        if (!result.Success || result.Data is null)
            return Json(new { items = Array.Empty<object>(), total = 0, error = result.Message });

        return Json(new
        {
            items = result.Data.Items.Select(o => new { value = o.Value, label = o.Label }),
            total = result.Data.Total,
            error = result.Data.Error
        });
    }

    // ---------- کمکی ----------

    private async Task FillLookupsAsync(DataSourceFormViewModel vm, CancellationToken ct)
    {
        var lookups = await _sender.Send(new GetDataSourceLookupsQuery(), ct);

        if (lookups.Data is not null)
            vm.LookupsJson = DataSourceFormViewModel.BuildLookupsJson(lookups.Data);
    }
}
