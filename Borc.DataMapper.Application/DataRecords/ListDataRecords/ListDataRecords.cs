using System.Text.Json;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Records;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.ListDataRecords;

/// <summary>
/// رکوردهای یک نسخهٔ قالب به‌صورت جدول (ستون‌ها = فیلدهای قالب). بدون نسخه: نسخهٔ ایمپورت فیلترشده،
/// وگرنه نسخهٔ آخرین رکورد ثبت‌شده.
/// RecordIds: exactly these records in this order (one page already chosen by the advanced filter); the other filters
/// and paging are then not applied and the caller sets the total.
/// TemplateId without TemplateVersionId: the records of every version of the template (user request: see the rows of
/// all versions); columns = the fields of all its versions (keys are kept across versions), newest version's order first.
/// </summary>
public sealed record ListDataRecordsQuery(
    string? Search = null,
    long? TemplateVersionId = null,
    RecordSource? Source = null,
    long? ImportBatchId = null,
    int Page = 1,
    int PageSize = 20,
    IReadOnlyList<long>? RecordIds = null,
    long? TemplateId = null
) : IRequest<Result<DataRecordListDto>>
{
    public const int MaxPageSize = 100;
}

/// <param name="TemplateVersionId">نسخه‌ای که نمایش داده می‌شود؛ null وقتی هنوز رکوردی نیست یا همهٔ نسخه‌ها.</param>
/// <param name="TemplateId">The template of the version shown (or of all versions).</param>
/// <param name="AllVersions">The records of every version of <paramref name="TemplateId"/>.</param>
public sealed record DataRecordListDto(
    long? TemplateVersionId,
    IReadOnlyList<RecordColumnDto> Columns,
    PagedResult<DataRecordListItemDto> Page,
    long? TemplateId = null,
    bool AllVersions = false);

public sealed record RecordColumnDto(string Key, string Label, FieldDataType DataType);

/// <param name="Values">مقدار هر فیلد به کلید فیلد؛ برای فیلدهای منبع‌دار (فهرست ثابت/فایل) عنوان گزینه.</param>
public sealed record DataRecordListItemDto(
    long Id,
    string TemplateName,
    int VersionNo,
    long TemplateVersionId,
    RecordSource Source,
    long? ImportBatchId,
    IReadOnlyDictionary<string, string?> Values,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    long? CreatedBy = null,
    long? UpdatedBy = null);

public sealed class ListDataRecordsHandler
    : IRequestHandler<ListDataRecordsQuery, Result<DataRecordListDto>>
{
    private readonly IAppDbContext _db;

    public ListDataRecordsHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<DataRecordListDto>> Handle(
        ListDataRecordsQuery request,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, ListDataRecordsQuery.MaxPageSize);

        var versionId = request.TemplateVersionId;
        var allOf = versionId is null ? request.TemplateId : null;

        if (allOf is null && versionId is null && request.ImportBatchId.HasValue)
            versionId = await _db.ImportBatches.AsNoTracking()
                .Where(b => b.Id == request.ImportBatchId.Value)
                .Select(b => (long?)b.TemplateVersionId)
                .FirstOrDefaultAsync(cancellationToken);

        if (allOf is null)
            versionId ??= await _db.DataRecords.AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Select(r => (long?)r.TemplateVersionId)
                .FirstOrDefaultAsync(cancellationToken);

        if (versionId is null && allOf is null)
            return Result<DataRecordListDto>.Ok(new DataRecordListDto(
                null, Array.Empty<RecordColumnDto>(),
                new PagedResult<DataRecordListItemDto>(Array.Empty<DataRecordListItemDto>(), 1, pageSize, 0)));

        // one version's fields; for all versions every field key once, from the newest version that has it
        var fields = (await (
                from f in _db.TemplateFields.AsNoTracking()
                join fv in _db.TemplateVersions on f.TemplateVersionId equals fv.Id
                where allOf != null ? fv.TemplateId == allOf.Value : f.TemplateVersionId == versionId!.Value
                select new { f.FieldKey, f.Label, f.DataType, f.DataSourceId, f.SortOrder, f.Id, fv.VersionNo })
            .ToListAsync(cancellationToken))
            .GroupBy(f => f.FieldKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(f => f.VersionNo).First())
            .OrderByDescending(f => f.VersionNo)
            .ThenBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .ToList();

        var query =
            from r in _db.DataRecords.AsNoTracking()
            join v in _db.TemplateVersions on r.TemplateVersionId equals v.Id
            join t in _db.Templates on r.TemplateId equals t.Id
            select new { r, v, t };
        query = allOf is { } templateId
            ? query.Where(x => x.r.TemplateId == templateId)
            : query.Where(x => x.r.TemplateVersionId == versionId!.Value);

        var ids = request.RecordIds;
        if (ids is not null)
            query = query.Where(x => ids.Contains(x.r.Id));

        if (ids is null && request.Source.HasValue)
            query = query.Where(x => x.r.SourceType == request.Source.Value);

        if (ids is null && request.ImportBatchId.HasValue)
            query = query.Where(x => x.r.ImportBatchId == request.ImportBatchId.Value);

        if (ids is null && search is not null)
        {
            var pattern = $"%{search}%";
            query = query.Where(x => EF.Functions.Like(x.r.DataJson, pattern));
        }

        var total = ids is null ? await query.CountAsync(cancellationToken) : ids.Count;

        var rows = await query
            .OrderByDescending(x => x.r.CreatedAt)
            .ThenByDescending(x => x.r.Id)
            .Skip(ids is null ? (page - 1) * pageSize : 0)
            .Take(ids is null ? pageSize : ids.Count)
            .Select(x => new
            {
                x.r.Id,
                TemplateName = x.t.Name,
                x.v.VersionNo,
                x.r.TemplateVersionId,
                x.r.SourceType,
                x.r.ImportBatchId,
                x.r.DataJson,
                x.r.CreatedAt,
                x.r.UpdatedAt,
                x.r.CreatedBy,
                x.r.UpdatedBy
            })
            .ToListAsync(cancellationToken);

        if (ids is not null)
        {
            var order = ids.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i);
            rows = rows.OrderBy(x => order.GetValueOrDefault(x.Id, int.MaxValue)).ToList();
        }

        var values = rows.Select(x => Parse(x.DataJson)).ToList();

        // مقدار ذخیره‌شدهٔ فیلدهای منبع‌دار معمولاً کد است؛ عنوان گزینه را از فهرست ثابت/فایل نشان می‌دهیم.
        // (منبع قالب و API گزینهٔ ذخیره‌شده ندارند؛ مقدار خام می‌ماند.)
        foreach (var field in fields.Where(f => f.DataSourceId.HasValue))
        {
            var used = values
                .Select(v => v.TryGetValue(field.FieldKey, out var value) ? value : null)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Distinct()
                .ToList();

            if (used.Count == 0)
                continue;

            var labels = await _db.DataSourceItems.AsNoTracking()
                .Where(i => i.DataSourceId == field.DataSourceId!.Value && used.Contains(i.Value))
                .Select(i => new { i.Value, i.Label })
                .ToListAsync(cancellationToken);

            var byValue = labels
                .GroupBy(l => l.Value, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Label, StringComparer.OrdinalIgnoreCase);

            foreach (var row in values)
            {
                if (row.TryGetValue(field.FieldKey, out var value) && value is not null && byValue.TryGetValue(value, out var label))
                    row[field.FieldKey] = label;
            }
        }

        var items = rows
            .Select((x, i) => new DataRecordListItemDto(
                x.Id, x.TemplateName, x.VersionNo, x.TemplateVersionId, x.SourceType,
                x.ImportBatchId, values[i], x.CreatedAt, x.UpdatedAt, x.CreatedBy, x.UpdatedBy))
            .ToList();

        var columns = fields
            .Select(f => new RecordColumnDto(f.FieldKey, f.Label, f.DataType))
            .ToList();

        return Result<DataRecordListDto>.Ok(new DataRecordListDto(
            versionId, columns, new PagedResult<DataRecordListItemDto>(items, page, pageSize, total),
            allOf ?? await _db.TemplateVersions.AsNoTracking().Where(v => v.Id == versionId).Select(v => (long?)v.TemplateId).FirstOrDefaultAsync(cancellationToken),
            allOf is not null));
    }

    private static Dictionary<string, string?> Parse(string json)
    {
        try
        {
            return ImportJson.ParseRaw(json)
                .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
