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
    IReadOnlyList<DataRecordValueDto> Values);

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
            .Select(f => new { f.FieldKey, f.Label, f.DataType })
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

        return Result<DataRecordDetailDto>.Ok(new DataRecordDetailDto(
            head.r.Id, head.t.Id, head.t.Name, head.v.Id, head.v.VersionNo, head.v.Status,
            head.r.SourceType, head.r.ImportBatchId, fileName, rowNumber,
            head.r.CreatedAt, head.r.UpdatedAt, values));
    }
}
