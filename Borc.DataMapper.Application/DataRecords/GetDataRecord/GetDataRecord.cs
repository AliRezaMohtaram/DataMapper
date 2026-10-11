using System.Text.Json;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Records;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.GetDataRecord;

public sealed record GetDataRecordQuery(long Id) : IRequest<Result<DataRecordDetailDto>>;

public sealed record DataRecordDetailDto(
    long Id,
    long TemplateId,
    string TemplateName,
    long TemplateVersionId,
    int VersionNo,
    TemplateVersionStatus VersionStatus,
    RecordSource Source,
    long? ImportBatchId,
    string? ImportFileName,
    long? ImportRowNumber,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<DataRecordValueDto> Values,
    RecordNeighborsDto Neighbors)
{
    public long? CreatedBy { get; init; }
}

/// <summary>
/// جای رکورد در فهرست رکوردهای همان نسخه (همان ترتیب فهرست: جدیدترین اول) برای «قبلی / بعدی».
/// PreviousId = ردیف بالاتر (جدیدتر)، NextId = ردیف پایین‌تر (قدیمی‌تر).
/// </summary>
public sealed record RecordNeighborsDto(long? PreviousId, long? NextId, int Position, int Total);

public sealed record DataRecordValueDto(
    string Key,
    string Label,
    FieldDataType DataType,
    string? Value,
    bool IsExtra);

public sealed class GetDataRecordHandler
    : IRequestHandler<GetDataRecordQuery, Result<DataRecordDetailDto>>
{
    private readonly IAppDbContext _db;

    public GetDataRecordHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<DataRecordDetailDto>> Handle(
        GetDataRecordQuery request,
        CancellationToken cancellationToken)
    {
        var head = await (
            from r in _db.DataRecords.AsNoTracking()
            join v in _db.TemplateVersions on r.TemplateVersionId equals v.Id
            join t in _db.Templates on r.TemplateId equals t.Id
            where r.Id == request.Id
            select new { r, v, t }).FirstOrDefaultAsync(cancellationToken);

        if (head is null)
            return Result<DataRecordDetailDto>.Failure("رکورد موردنظر پیدا نشد.");

        string? fileName = null;
        long? rowNumber = null;

        if (head.r.ImportBatchId.HasValue)
            fileName = await _db.ImportBatches
                .AsNoTracking()
                .Where(b => b.Id == head.r.ImportBatchId.Value)
                .Select(b => b.FileName)
                .FirstOrDefaultAsync(cancellationToken);

        if (head.r.ImportRowId.HasValue)
            rowNumber = await _db.ImportRows
                .AsNoTracking()
                .Where(x => x.Id == head.r.ImportRowId.Value)
                .Select(x => (long?)x.RowNumber)
                .FirstOrDefaultAsync(cancellationToken);

        var fields = await _db.TemplateFields
            .AsNoTracking()
            .Where(f => f.TemplateVersionId == head.v.Id)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .Select(f => new { f.FieldKey, f.Label, f.DataType, f.DataSourceId })
            .ToListAsync(cancellationToken);

        Dictionary<string, string?> stored;

        try
        {
            stored = ImportJson.ParseRaw(head.r.DataJson)
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            stored = new Dictionary<string, string?>();
        }

        // مقدار فیلدهای منبع‌دار معمولاً کد است؛ عنوان گزینه (فهرست ثابت/فایل) نمایش داده می‌شود، مثل فهرست رکوردها
        foreach (var f in fields.Where(f => f.DataSourceId.HasValue))
        {
            if (!stored.TryGetValue(f.FieldKey, out var raw) || string.IsNullOrWhiteSpace(raw))
                continue;

            var title = await _db.DataSourceItems.AsNoTracking()
                .Where(i => i.DataSourceId == f.DataSourceId!.Value && i.Value == raw)
                .Select(i => i.Label)
                .FirstOrDefaultAsync(cancellationToken);

            if (title is not null)
                stored[f.FieldKey] = title;
        }

        var values = fields
            .Select(f => new DataRecordValueDto(
                f.FieldKey, f.Label, f.DataType,
                stored.TryGetValue(f.FieldKey, out var v) ? v : null,
                false))
            .ToList();

        // کلیدهایی که دیگر در قالب نیستند (برای اینکه داده‌ای پنهان نماند)
        var known = fields.Select(f => f.FieldKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        values.AddRange(stored
            .Where(p => !known.Contains(p.Key))
            .Select(p => new DataRecordValueDto(p.Key, p.Key, FieldDataType.Text, p.Value, true)));

        var created = head.r.CreatedAt;
        var id = head.r.Id;
        var sameVersion = _db.DataRecords.AsNoTracking().Where(r => r.TemplateVersionId == head.v.Id);
        var newer = sameVersion.Where(r => r.CreatedAt > created || (r.CreatedAt == created && r.Id > id));
        var older = sameVersion.Where(r => r.CreatedAt < created || (r.CreatedAt == created && r.Id < id));

        var neighbors = new RecordNeighborsDto(
            await newer.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).Select(r => (long?)r.Id).FirstOrDefaultAsync(cancellationToken),
            await older.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Select(r => (long?)r.Id).FirstOrDefaultAsync(cancellationToken),
            await newer.CountAsync(cancellationToken) + 1,
            await sameVersion.CountAsync(cancellationToken));

        return Result<DataRecordDetailDto>.Ok(new DataRecordDetailDto(
            head.r.Id, head.t.Id, head.t.Name, head.v.Id, head.v.VersionNo, head.v.Status,
            head.r.SourceType, head.r.ImportBatchId, fileName, rowNumber,
            head.r.CreatedAt, head.r.UpdatedAt, values, neighbors) { CreatedBy = head.r.CreatedBy });
    }
}
