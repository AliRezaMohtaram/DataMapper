using Acl.AspNetCore.Authorization;
using Acl.Core.Model;
using Borc.DataMapper.Web.Modules;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.TemplateLayouts.DeleteTemplateLayout;
using Borc.DataMapper.Application.TemplateLayouts.GetTemplateLayout;
using Borc.DataMapper.Application.TemplateLayouts.SaveTemplateLayout;
using Borc.DataMapper.Application.TemplateLayouts.UpdateTemplateLayout;
using Borc.DataMapper.Application.Templates.UpdateTemplate;
using Borc.DataMapper.Domain.Templates;
using Borc.DataMapper.Web.ViewModels.TemplateLayouts;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Threading;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Borc.DataMapper.Web.Controllers;

[RequirePermission(MapperResources.Templates, WellKnownActions.Edit)]
public sealed class TemplateLayoutsController : Controller
{
    private readonly ISender _sender;

    public TemplateLayoutsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> Designer(
        long versionId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTemplateLayoutQuery(versionId), cancellationToken);

        if (!result.Success || result.Data is null)
        {
            TempData["Error"] = result.Message ?? "نسخه موردنظر پیدا نشد.";
            return RedirectToAction("Index", "Templates");
        }

        var d = result.Data;

        return View(new LayoutDesignerViewModel(
            d.TemplateVersionId,
            d.TemplateId,
            d.TemplateName,
            d.VersionNo,
            d.VersionStatus,
            HasBlocks(d.LayoutJson),
            d.IsPublished,
            BuildDataJson(d)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        [FromBody] SaveLayoutRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return Json(new { success = false, message = "درخواست نامعتبر است." });

        var result = await _sender.Send(
            new SaveTemplateLayoutCommand(request.VersionId, request.LayoutJson ?? string.Empty),
            cancellationToken);

        return Json(new { success = result.Success, message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteLayoutRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return Json(new { success = false, message = "درخواست نامعتبر است." });

        var result = await _sender.Send(
            new DeleteTemplateLayoutCommand(request.VersionId),
            cancellationToken);

        return Json(new { success = result.Success, message = result.Message });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateTemplateLayoutCommand command,
        CancellationToken cancellationToken)
    {
       
            var result = await _sender.Send(command, cancellationToken);

            if (result.Success)
            {
                TempData["Success"] = result.Message ?? "قالب ویرایش شد.";
                //return RedirectToAction(nameof(Detail), new { id = command.Id });
            
        }

       
        return Json(new { success = result.Success, message = result.Message });
    }

    // ---------- کمکی‌ها ----------

    /// <summary>داده‌ای که طراح JavaScript از صفحه می‌خواند (script#ldData).</summary>
    private string BuildDataJson(TemplateLayoutDto d)
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
            readOnly = d.VersionStatus != TemplateVersionStatus.Draft,
            isPublished = d.IsPublished,
            layout,
            fields = d.Fields.Select(f => new
            {
                key = f.Key,
                label = f.Label,
                type = f.DataType.ToString().ToLowerInvariant(),
                required = f.IsRequired,
                dbType = f.DbType,
                length = f.Length,
                precision = f.Precision,
                scale = f.Scale,
                regex = f.Regex
            }),
            urls = new
            {
                save = Url.Action(nameof(Save), "TemplateLayouts"),
                remove = Url.Action(nameof(Delete), "TemplateLayouts")
            }
        };

        // رمزگذاری پیش‌فرض System.Text.Json علامت‌های < > & را escape می‌کند؛ برای script امن است.
        return JsonSerializer.Serialize(data);
    }

    private static bool HasBlocks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.TryGetProperty("blocks", out var blocks)
                   && blocks.ValueKind == JsonValueKind.Array
                   && blocks.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
