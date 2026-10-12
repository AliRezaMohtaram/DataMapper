using System.Text.Json;
using Acl.AspNetCore.Authorization;
using Acl.Core.Model;
using Borc.DataMapper.Web.Modules;
using Borc.DataMapper.Application.DataRecords.CreateDataRecord;
using Borc.DataMapper.Application.DataRecords.DeleteDataRecord;
using Borc.DataMapper.Application.DataRecords.GetDataRecord;
using Borc.DataMapper.Application.DataRecords.GetRecordForm;
using Borc.DataMapper.Application.DataRecords.ListDataRecords;
using Borc.DataMapper.Application.DataRecords.UpdateDataRecord;
using Borc.DataMapper.Application.Imports.GetImportLookups;
using Borc.DataMapper.Domain.Records;
using Borc.DataMapper.Domain.Templates;
using Borc.DataMapper.Web.ViewModels.DataRecords;
using Borc.DataMapper.Web.Mvc;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Borc.DataMapper.Web.Controllers;

[RequirePermission(MapperResources.DataRecords, WellKnownActions.View)]
public sealed class DataRecordsController : Controller
{
    private readonly ISender _sender;

    public DataRecordsController(ISender sender)
    {
        _sender = sender;
    }

    // ---------- فهرست ----------

    /// <summary>
    /// Master list: one row per template version with records (count, units, last change). Row click → the records of
    /// that version (Version, the detail).
    /// آدرس‌های قدیمی با نسخه یا ایمپورت به Version هدایت می‌شوند.
    /// </summary>
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
        if (importBatchId.HasValue)
            return RedirectToAction(nameof(Version), new { search, templateVersionId, source, importBatchId, page, pageSize });

        var query = new ListRecordsOverviewQuery(search, templateVersionId, source, page, pageSize);
        var result = await _sender.Send(query, cancellationToken);
        var versions = await LoadVersionOptionsAsync(cancellationToken);

        if (!result.Success || result.Data is null)
        {
            ModelState.AddResultErrors(result, "خطا در دریافت رکوردها.", Request);
            return View(new RecordOverviewViewModel(query, new(Array.Empty<RecordVersionSummaryDto>(), 1, pageSize, 0), versions));
        }

        return View(new RecordOverviewViewModel(query, result.Data, versions));
    }

    /// <summary>رکوردهای یک نسخهٔ قالب به‌صورت جدول (ستون‌ها = فیلدها)؛ ثبت، ویرایش، رونوشت و حذف.</summary>
    [HttpGet]
    public async Task<IActionResult> Version(
        string? search,
        long? templateVersionId,
        RecordSource? source,
        long? importBatchId,
        string? q,
        [FromServices] Modules.Reports.ListFilters filters,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListDataRecordsQuery(search, templateVersionId, source, importBatchId, page, pageSize);

        // which version: the asked one, else the default (filtered import's or latest record's)
        var versionId = templateVersionId;
        if (versionId is null)
            versionId = (await _sender.Send(query with { Page = 1, PageSize = 1 }, cancellationToken)).Data?.TemplateVersionId;

        // the advanced filter (and the old search text) go through MReporter with the user's visibility
        Modules.Reports.FilteredPage? filtered = null;
        string? sourceKey = null;
        if (versionId is { } vid)
        {
            (filtered, sourceKey) = await filters.RecordsAsync(vid, q, search, source, importBatchId, Math.Max(1, page), Math.Clamp(pageSize, 1, ListDataRecordsQuery.MaxPageSize), cancellationToken);
            if (filtered.Error is not null)
                ModelState.AddModelError(string.Empty, filtered.Error);
            else if (filtered.Ids is not null)
                query = query with { TemplateVersionId = vid, RecordIds = filtered.Ids };
            query = query with { Search = null };
        }

        var result = await _sender.Send(query, cancellationToken);
        var versions = await LoadVersionOptionsAsync(cancellationToken);
        if (result.Success && result.Data is not null && filtered?.Ids is not null)
            result = Application.Common.Results.Result<DataRecordListDto>.Ok(result.Data with
            {
                Page = result.Data.Page with { Page = Math.Max(1, page), PageSize = Math.Clamp(pageSize, 1, ListDataRecordsQuery.MaxPageSize), TotalCount = filtered.Total },
            });

        ViewData["FilterSource"] = sourceKey;
        ViewData["FilterQuery"] = filtered?.QueryJson;

        if (!result.Success || result.Data is null)
        {
            ModelState.AddResultErrors(result, "خطا در دریافت رکوردها.", Request);

            return View("Version", new DataRecordIndexViewModel(
                query, new(Array.Empty<DataRecordListItemDto>(), 1, pageSize, 0), Array.Empty<RecordColumnDto>(), versions));
        }

        // بدون انتخاب، نسخهٔ پیش‌فرض (ایمپورت فیلترشده یا آخرین رکورد) نمایش داده می‌شود
        return View("Version", new DataRecordIndexViewModel(
            query with { TemplateVersionId = result.Data.TemplateVersionId }, result.Data.Page, result.Data.Columns, versions));
    }

    // ---------- ورود دستی ----------

    /// <summary>
    /// فرم رکورد جدید (در مودال یا صفحهٔ کامل). بدون versionId: انتخاب قالب (فقط قالب‌هایی که واحد کاربر مجاز است).
    /// copyFrom: رکورد جدید با مقدارهای یک رکورد موجود (رونوشت).
    /// </summary>
    [HttpGet]
    [RequirePermission(MapperResources.DataRecords, WellKnownActions.Create)]
    public async Task<IActionResult> Create(long? versionId, long? copyFrom, CancellationToken cancellationToken)
    {
        Application.Common.Results.Result<RecordFormDto> form;

        if (copyFrom.HasValue)
        {
            form = await _sender.Send(new GetRecordFormQuery(RecordId: copyFrom), cancellationToken);

            if (form.Success && form.Data is not null && form.Data.VersionStatus != TemplateVersionStatus.Published)
            {
                TempData["Error"] = "نسخهٔ قالب این رکورد منتشرشده نیست؛ رونوشت از آن ممکن نیست.";
                return RedirectToAction(nameof(Detail), new { id = copyFrom });
            }
        }
        else
        {
            if (!versionId.HasValue)
            {
                var lookups = await _sender.Send(new GetImportLookupsQuery(), cancellationToken);
                var versions = lookups.Data?.Versions ?? Array.Empty<ImportVersionOption>();

                // انتخاب قالب در مودال باز می‌شود؛ بدون JavaScript صفحهٔ کامل SelectVersion
                return this.ModalOrView("_SelectVersion", versions, "SelectVersion");
            }

            form = await _sender.Send(new GetRecordFormQuery(TemplateVersionId: versionId), cancellationToken);
        }

        if (!form.Success || form.Data is null)
        {
            TempData["Error"] = form.Message ?? "فرم ورود داده ساخته نشد.";
            return RedirectToAction(nameof(Create));
        }

        var data = form.Data with { RecordId = null };

        var page = BuildFormPage(data) with { CopiedFrom = copyFrom };

        return Request.IsModalRequest()
            ? PartialView("_RecordForm", page)
            : View("Form", page with { Recent = await LoadRecentAsync(data.TemplateVersionId, cancellationToken) });
    }

    /// <summary>آخرین رکوردهای نسخه برای زیر فرم ورود داده (HTML)؛ پس از هر ذخیره دوباره گرفته می‌شود.</summary>
    [HttpGet]
    public async Task<IActionResult> Recent(long versionId, CancellationToken cancellationToken)
    {
        return PartialView("_RecentRecords", await LoadRecentAsync(versionId, cancellationToken));
    }

    /// <summary>فرم ویرایش (در مودال از فهرست/جزئیات، یا صفحهٔ کامل).</summary>
    [HttpGet]
    [RequirePermission(MapperResources.DataRecords, WellKnownActions.Edit)]
    public async Task<IActionResult> Edit(long id, CancellationToken cancellationToken)
    {
        var form = await _sender.Send(new GetRecordFormQuery(RecordId: id), cancellationToken);

        if (!form.Success || form.Data is null)
        {
            TempData["Error"] = form.Message ?? "فرم ویرایش ساخته نشد.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var page = BuildFormPage(form.Data);

        return Request.IsModalRequest()
            ? PartialView("_RecordForm", page)
            : View("Form", page with { Recent = await LoadRecentAsync(form.Data.TemplateVersionId, cancellationToken) });
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

        // ثبت جدید = Create، ویرایش = Edit (هر دو از همین اکشن می‌آیند).
        var action = request.RecordId.HasValue ? WellKnownActions.Edit : WellKnownActions.Create;
        var authorization = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        if (!(await authorization.AuthorizeAsync(User, null, [new PermissionRequirement(MapperResources.DataRecords, action)])).Succeeded)
            return Forbid();

        var result = request.RecordId.HasValue
            ? await _sender.Send(new UpdateDataRecordCommand(request.RecordId.Value, request.Values), cancellationToken)
            : await _sender.Send(new CreateDataRecordCommand(request.VersionId, request.Values), cancellationToken);

        if (!result.Success || result.Data is null)
            return Json(new { success = false, message = result.Message });

        if (!result.Data.Saved)
            return Json(new { success = false, message = result.Message, fieldErrors = result.Data.FieldErrors });

        // «ذخیره و رکورد جدید» در همان فرم می‌ماند و پیامش را خودش نشان می‌دهد
        if (!request.Stay)
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
    [RequirePermission(MapperResources.DataRecords, WellKnownActions.Delete)]
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
        var lookups = await _sender.Send(new GetImportLookupsQuery(ForEntry: false), ct);

        return lookups.Data?.Versions.Select(v => new VersionFilterOption(v.Id, v.Label)).ToList()
               ?? new List<VersionFilterOption>();
    }

    private const int RecentCount = 10;

    private async Task<RecentRecordsViewModel> LoadRecentAsync(long versionId, CancellationToken ct)
    {
        var result = await _sender.Send(new ListDataRecordsQuery(TemplateVersionId: versionId, PageSize: RecentCount), ct);
        var data = result.Data;

        return new RecentRecordsViewModel(
            versionId,
            data?.Page.TotalCount ?? 0,
            new RecordGridViewModel(
                data?.Columns ?? Array.Empty<RecordColumnDto>(),
                data?.Page.Items ?? Array.Empty<DataRecordListItemDto>(),
                Compact: true));
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
                // منبع کوچک: گزینه‌ها همراه فرم؛ منبع بزرگ/API/قالب: dataSourceId و جستجوی سمت سرور
                options = f.Options?.Select(o => new { value = o.Value, label = o.Label }),
                dataSourceId = f.DataSourceId
            }),
            values = d.Values,
            labels = d.Labels,
            urls = new
            {
                save = Url.Action(nameof(Save), "DataRecords"),
                index = Url.Action(nameof(Index), "DataRecords"),
                recent = Url.Action(nameof(Recent), "DataRecords", new { versionId = d.TemplateVersionId }),
                options = Url.Action("Options", "DataSources")
            }
        };

        return new RecordFormPageViewModel(
            d.RecordId, d.TemplateName, d.VersionNo, d.TemplateVersionId, JsonSerializer.Serialize(data));
    }
}
