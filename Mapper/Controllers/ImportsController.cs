using Acl.AspNetCore.Authorization;
using Acl.Core.Model;
using Borc.DataMapper.Web.Modules;
using Borc.DataMapper.Application.Imports.ApplyImportMapping;
using Borc.DataMapper.Application.Imports.CommitImportBatch;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Application.Imports.DeleteImportBatch;
using Borc.DataMapper.Application.Imports.GetImportBatch;
using Borc.DataMapper.Application.Imports.GetImportFile;
using Borc.DataMapper.Application.Imports.GetImportLookups;
using Borc.DataMapper.Application.Imports.ListImportBatches;
using Borc.DataMapper.Application.Imports.ReopenImportMapping;
using Borc.DataMapper.Application.Imports.UploadImportBatch;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Web.Mvc;
using Borc.DataMapper.Web.ViewModels;
using Borc.DataMapper.Web.ViewModels.Imports;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

[RequirePermission(MapperResources.Imports, WellKnownActions.View)]
public sealed class ImportsController : Controller
{
    private readonly ISender _sender;

    public ImportsController(ISender sender)
    {
        _sender = sender;
    }

    // ---------- فهرست ----------

    [HttpGet]
    public async Task<IActionResult> Index(
        ImportBatchStatus? status,
        string? search,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListImportBatchesQuery(status, search, page, pageSize);

        var result = await _sender.Send(query, cancellationToken);

        if (!result.Success || result.Data is null)
        {
            ModelState.AddResultErrors(result, "خطا در دریافت فهرست ایمپورت‌ها.", Request);

            return View(new ImportIndexViewModel(
                query,
                new(Array.Empty<ImportBatchListItemDto>(), 1, pageSize, 0)));
        }

        return View(new ImportIndexViewModel(query, result.Data));
    }

    // ---------- آپلود ----------

    /// <summary>فرم مشترک آپلود؛ هم در مودال (از هر صفحه) و هم در صفحهٔ کامل.</summary>
    private const string UploadPartial = "_UploadForm";

    [HttpGet]
    [RequirePermission(MapperResources.Imports, WellKnownActions.Create)]
    [OrgUnitField(MapperResources.Imports, allowPublic: false)]
    public async Task<IActionResult> Upload(long? versionId, long? profileId, CancellationToken cancellationToken)
        => await UploadFormAsync(versionId, profileId, cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ImportLimits.MaxFileBytes + 1_048_576)]
    [RequirePermission(MapperResources.Imports, WellKnownActions.Create)]
    [OrgUnitField(MapperResources.Imports, allowPublic: false)]
    public async Task<IActionResult> UploadAsync(
        IFormFile? file,
        long templateVersionId,
        long? mappingProfileId,
        CancellationToken cancellationToken)
    {
        string? error = null;

        if (ModelState[OrgUnitFieldAttribute.FieldName]?.Errors.Count > 0)
            error = "واحد سازمانی انتخاب‌شده مجاز نیست.";
        else if (templateVersionId <= 0)
            error = "قالب و نسخهٔ مقصد را انتخاب کنید.";
        else if (file is null || file.Length == 0)
            error = "فایلی انتخاب نشده است.";
        else if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            error = "فقط فایل xlsx پشتیبانی می‌شود.";
        else if (file.Length > ImportLimits.MaxFileBytes)
            error = "حجم فایل بیش از حد مجاز (10 مگابایت) است.";

        if (error is null)
        {
            byte[] content;

            await using (var ms = new MemoryStream())
            {
                await file!.CopyToAsync(ms, cancellationToken);
                content = ms.ToArray();
            }

            var result = await _sender.Send(
                new UploadImportBatchCommand(templateVersionId, file.FileName, content, mappingProfileId),
                cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "فایل بارگذاری شد؛ ستون‌ها را تطبیق دهید.";
                return this.ModalOrRedirect(Url.Action(nameof(Detail), new { id = result.Data })!);
            }

            error = result.Message ?? "بارگذاری فایل انجام نشد.";
        }

        ModelState.AddModelError(string.Empty, error);
        return await UploadFormAsync(templateVersionId, mappingProfileId, cancellationToken);
    }

    private async Task<IActionResult> UploadFormAsync(long? versionId, long? profileId, CancellationToken ct)
    {
        var lookups = await _sender.Send(new GetImportLookupsQuery(), ct);

        var vm = new ImportUploadViewModel(
            lookups.Data ?? new ImportLookupsDto(Array.Empty<ImportVersionOption>(), Array.Empty<ImportProfileOption>()),
            versionId > 0 ? versionId : null,
            profileId);

        return this.ModalOrView(UploadPartial, vm, "Upload");
    }

    // ---------- فایل اصلی ----------

    /// <summary>دانلود فایلی که برای این ایمپورت آپلود شده بود.</summary>
    [HttpGet]
    public async Task<IActionResult> Download(long id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetImportFileQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "فایل پیدا نشد.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    // ---------- جزئیات / تطبیق ----------

    [HttpGet]
    public async Task<IActionResult> Detail(
        long id,
        ImportRowStatus? rowStatus,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _sender.Send(
            new GetImportBatchQuery(id, rowStatus, page, pageSize),
            cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "ایمپورت موردنظر پیدا نشد.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["RowStatus"] = rowStatus;

        return View(result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Imports, WellKnownActions.Edit)]
    public async Task<IActionResult> Map(
        long id,
        List<ColumnMappingInput>? mappings,
        string? saveProfileName,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ApplyImportMappingCommand(id, mappings ?? new List<ColumnMappingInput>(), saveProfileName),
            cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "اعتبارسنجی انجام شد." : "اعمال نگاشت انجام نشد.");

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Imports, WellKnownActions.Edit)]
    public async Task<IActionResult> Reopen(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ReopenImportMappingCommand(id), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "انجام شد." : "انجام نشد.");

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Imports, WellKnownActions.Approve)]
    public async Task<IActionResult> Commit(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CommitImportBatchCommand(id), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "ثبت نهایی انجام شد." : "ثبت نهایی انجام نشد.");

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission(MapperResources.Imports, WellKnownActions.Delete)]
    public async Task<IActionResult> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteImportBatchCommand(id), cancellationToken);

        if (!result.Success)
        {
            TempData["Error"] = result.Message ?? "حذف انجام نشد.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        TempData["Success"] = result.Message ?? "ایمپورت حذف شد.";
        return RedirectToAction(nameof(Index));
    }
}
