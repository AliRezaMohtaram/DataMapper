using Borc.DataMapper.Application.Imports.ApplyImportMapping;
using Borc.DataMapper.Application.Imports.CommitImportBatch;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Application.Imports.DeleteImportBatch;
using Borc.DataMapper.Application.Imports.GetImportBatch;
using Borc.DataMapper.Application.Imports.GetImportLookups;
using Borc.DataMapper.Application.Imports.ListImportBatches;
using Borc.DataMapper.Application.Imports.ReopenImportMapping;
using Borc.DataMapper.Application.Imports.UploadImportBatch;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Web.ViewModels;
using Borc.DataMapper.Web.ViewModels.Imports;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

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
            ModelState.AddModelError(string.Empty, result.Message ?? "خطا در دریافت فهرست ایمپورت‌ها.");

            return View(new ImportIndexViewModel(
                query,
                new(Array.Empty<ImportBatchListItemDto>(), 1, pageSize, 0)));
        }

        return View(new ImportIndexViewModel(query, result.Data));
    }

    // ---------- آپلود ----------

    [HttpGet]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        var lookups = await _sender.Send(new GetImportLookupsQuery(), cancellationToken);

        ViewData["Lookups"] = lookups.Data
            ?? new ImportLookupsDto(Array.Empty<ImportVersionOption>(), Array.Empty<ImportProfileOption>());

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(ImportLimits.MaxFileBytes + 1_048_576)]
    public async Task<IActionResult> UploadAsync(
        IFormFile? file,
        long templateVersionId,
        long? mappingProfileId,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "فایلی انتخاب نشده است.";
            return RedirectToAction(nameof(Upload));
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "فقط فایل xlsx پشتیبانی می‌شود.";
            return RedirectToAction(nameof(Upload));
        }

        if (file.Length > ImportLimits.MaxFileBytes)
        {
            TempData["Error"] = "حجم فایل بیش از حد مجاز (10 مگابایت) است.";
            return RedirectToAction(nameof(Upload));
        }

        byte[] content;

        await using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms, cancellationToken);
            content = ms.ToArray();
        }

        var result = await _sender.Send(
            new UploadImportBatchCommand(templateVersionId, file.FileName, content, mappingProfileId),
            cancellationToken);

        if (!result.Success)
        {
            TempData["Error"] = result.Message ?? "بارگذاری فایل انجام نشد.";
            return RedirectToAction(nameof(Upload));
        }

        TempData["Success"] = result.Message ?? "فایل بارگذاری شد.";
        return RedirectToAction(nameof(Detail), new { id = result.Data });
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
