using System.Text.Json;
using Borc.DataMapper.Application.DataRecords.CreateDataRecord;
using Borc.DataMapper.Application.DataRecords.DeleteDataRecord;
using Borc.DataMapper.Application.DataRecords.GetDataRecord;
using Borc.DataMapper.Application.DataRecords.GetRecordForm;
using Borc.DataMapper.Application.DataRecords.ListDataRecords;
using Borc.DataMapper.Application.DataRecords.UpdateDataRecord;
using Borc.DataMapper.Application.Imports.GetImportLookups;
using Borc.DataMapper.Domain.Records;
using Borc.DataMapper.Web.ViewModels.DataRecords;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

public sealed class DataRecordsController : Controller
{
    private readonly ISender _sender;

    public DataRecordsController(ISender sender)
    {
        _sender = sender;
    }

    // ---------- فهرست ----------

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        long? templateVersionId,
        RecordSource? source,
        long? importBatchId,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListDataRecordsQuery(search, templateVersionId, source, importBatchId, page, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        var versions = await LoadVersionOptionsAsync(cancellationToken);

        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "خطا در دریافت رکوردها.");

            return View(new DataRecordIndexViewModel(
                query, new(Array.Empty<DataRecordListItemDto>(), 1, pageSize, 0), versions));
        }

        return View(new DataRecordIndexViewModel(query, result.Data, versions));
    }

    // ---------- ورود دستی ----------

    /// <summary>بدون versionId: انتخاب قالب؛ با versionId: فرم ورود داده.</summary>
    [HttpGet]
    public async Task<IActionResult> Create(long? versionId, CancellationToken cancellationToken)
    {
        if (!versionId.HasValue)
        {
            var lookups = await _sender.Send(new GetImportLookupsQuery(), cancellationToken);
            return View("SelectVersion", lookups.Data?.Versions ?? Array.Empty<ImportVersionOption>());
        }

        var form = await _sender.Send(new GetRecordFormQuery(TemplateVersionId: versionId), cancellationToken);

        if (!form.Success || form.Data is null)
        {
            TempData["Error"] = form.Message ?? "فرم ورود داده ساخته نشد.";
            return RedirectToAction(nameof(Create));
        }

        return View("Form", BuildFormPage(form.Data));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        var form = await _sender.Send(new GetRecordFormQuery(RecordId: id), cancellationToken);

        if (!form.Success || form.Data is null)
        {
            TempData["Error"] = form.Message ?? "فرم ویرایش ساخته نشد.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        return View("Form", BuildFormPage(form.Data));
    }

    /// <summary>ذخیرهٔ رکورد جدید یا ویرایش‌شده؛ فرم با fetch و JSON ارسال می‌شود.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        [FromBody] SaveRecordRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.Values is null)
            return Json(new { success = false, message = "درخواست نامعتبر است." });

        var result = request.RecordId.HasValue
            ? await _sender.Send(new UpdateDataRecordCommand(request.RecordId.Value, request.Values), cancellationToken)
            : await _sender.Send(new CreateDataRecordCommand(request.VersionId, request.Values), cancellationToken);

        if (!result.Success || result.Data is null)
            return Json(new { success = false, message = result.Message });

        if (!result.Data.Saved)
            return Json(new { success = false, message = result.Message, fieldErrors = result.Data.FieldErrors });

        TempData["Success"] = result.Message;

        return Json(new
        {
            success = true,
            message = result.Message,
            redirect = Url.Action(nameof(Detail), new { id = result.Data.Id })
        });
    }

    // ---------- جزئیات و حذف ----------

    [HttpGet]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDataRecordQuery(id), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "رکورد موردنظر پیدا نشد.";
            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeleteDataRecordCommand(id), cancellationToken);

        TempData[result.Success ? "Success" : "Error"] =
            result.Message ?? (result.Success ? "رکورد حذف شد." : "حذف رکورد انجام نشد.");

        return RedirectToAction(nameof(Index));
    }

    // ---------- کمکی ----------

    private async Task<IReadOnlyList<VersionFilterOption>> LoadVersionOptionsAsync(CancellationToken ct)
    {
        var lookups = await _sender.Send(new GetImportLookupsQuery(), ct);

        return lookups.Data?.Versions.Select(v => new VersionFilterOption(v.Id, v.Label)).ToList()
               ?? new List<VersionFilterOption>();
    }

    private RecordFormPageViewModel BuildFormPage(RecordFormDto d)
    {
        object? layout = null;

        if (!string.IsNullOrWhiteSpace(d.LayoutJson))
        {
            try
            {
                layout = JsonDocument.Parse(d.LayoutJson).RootElement.Clone();
            }
            catch (JsonException)
            {
                layout = null;
            }
        }

        var data = new
        {
            versionId = d.TemplateVersionId,
            versionNo = d.VersionNo,
            templateName = d.TemplateName,
            recordId = d.RecordId,
            layout,
            fields = d.Fields.Select(f => new
            {
                key = f.Key,
                label = f.Label,
                //type = f.DataType.ToString().ToLowerInvariant(),
                required = f.IsRequired,
                dbType = f.DbType,
                length = f.Length,
                precision = f.Precision,
                scale = f.Scale,
                regex = f.Regex,
                options = f.Options.Select(o => new { value = o.Value, label = o.Label })
            }),
            values = d.Values,
            urls = new
            {
                save = Url.Action(nameof(Save), "DataRecords"),
                index = Url.Action(nameof(Index), "DataRecords")
            }
        };

        return new RecordFormPageViewModel(
            d.RecordId, d.TemplateName, d.VersionNo, d.TemplateVersionId, JsonSerializer.Serialize(data));
    }
}
