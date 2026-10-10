using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Borc.DataMapper.Application.Imports.GetImportBatch;

public sealed record GetImportBatchQuery(
    long Id,
    ImportRowStatus? RowStatus = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<Result<ImportBatchDetailDto>>;

public sealed record ImportBatchDetailDto(
    long Id,
    long TemplateVersionId,
    long TemplateId,
    string TemplateName,
    int VersionNo,
    long? MappingProfileId,
    string? MappingProfileName,
    string FileName,
    long? FileSize,
    /// <summary>فایل اصلی نگه‌داری شده و قابل دانلود است (ایمپورت‌های قدیمی فقط نام فایل دارند).</summary>
    bool HasFile,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    ImportBatchStatus Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    IReadOnlyList<ImportTargetFieldDto> Fields,
    /// <summary>فقط در وضعیت Uploaded پر می‌شود (پیشنهاد نگاشت ستون‌ها).</summary>
    IReadOnlyList<ImportColumnDto> Columns,
    /// <summary>برای وضعیت‌های بعد از آپلود؛ در Uploaded برابر null است.</summary>
    PagedResult<ImportRowItemDto>? Rows);

public sealed record ImportTargetFieldDto(string Key, string Label, FieldDataType DataType, bool IsRequired);

public sealed record ImportColumnDto(
    string Header,
    IReadOnlyList<string> Samples,
    string? FieldKey,
    MappingMethod Method,
    decimal Confidence);

public sealed record ImportRowItemDto(
    long Id,
    int RowNumber,
    ImportRowStatus Status,
    int ErrorCount,
    IReadOnlyList<string> Errors,
    string Preview);

public sealed class GetImportBatchHandler
    : IRequestHandler<GetImportBatchQuery, Result<ImportBatchDetailDto>>
{
    private readonly IAppDbContext _db;

    public GetImportBatchHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ImportBatchDetailDto>> Handle(
        GetImportBatchQuery request,
        CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (batch is null)
            return Result<ImportBatchDetailDto>.Failure("ایمپورت موردنظر پیدا نشد.");

        var info = await (
            from v in _db.TemplateVersions.AsNoTracking()
            join t in _db.Templates on v.TemplateId equals t.Id
            where v.Id == batch.TemplateVersionId
            select new { v.TemplateId, TemplateName = t.Name, v.VersionNo })
            .FirstOrDefaultAsync(cancellationToken);

        if (info is null)
            return Result<ImportBatchDetailDto>.Failure("نسخهٔ این ایمپورت پیدا نشد.");

        var fieldEntities = await _db.TemplateFields
            .AsNoTracking()
            .Where(f => f.TemplateVersionId == batch.TemplateVersionId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .ToListAsync(cancellationToken);

        var fields = fieldEntities
            .Select(f => new ImportTargetFieldDto(f.FieldKey, f.Label, f.DataType, f.IsRequired))
            .ToList();

        string? profileName = null;

        if (batch.MappingProfileId.HasValue)
        {
            profileName = await _db.MappingProfiles
                .AsNoTracking()
                .Where(p => p.Id == batch.MappingProfileId.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        IReadOnlyList<ImportColumnDto> columns = Array.Empty<ImportColumnDto>();
        PagedResult<ImportRowItemDto>? rows = null;

        if (batch.Status == ImportBatchStatus.Uploaded)
            columns = await BuildColumnsAsync(batch, fieldEntities, cancellationToken);
        else
            rows = await BuildRowsAsync(batch.Id, request, cancellationToken);

        return Result<ImportBatchDetailDto>.Ok(new ImportBatchDetailDto(
            batch.Id,
            batch.TemplateVersionId,
            info.TemplateId,
            info.TemplateName,
            info.VersionNo,
            batch.MappingProfileId,
            profileName,
            batch.FileName,
            batch.FileSize,
            await _db.ImportBatchFiles.AnyAsync(f => f.ImportBatchId == batch.Id, cancellationToken),
            batch.TotalRows,
            batch.ValidRows,
            batch.InvalidRows,
            batch.Status,
            batch.CreatedAt,
            batch.StartedAt,
            batch.CompletedAt,
            fields,
            columns,
            rows));
    }

    private async Task<IReadOnlyList<ImportColumnDto>> BuildColumnsAsync(
        ImportBatch batch,
        List<TemplateField> fieldEntities,
        CancellationToken ct)
    {
        var sampleJson = await _db.ImportRows
            .AsNoTracking()
            .Where(r => r.ImportBatchId == batch.Id)
            .OrderBy(r => r.RowNumber)
            .Take(3)
            .Select(r => r.RawDataJson)
            .ToListAsync(ct);

        if (sampleJson.Count == 0)
            return Array.Empty<ImportColumnDto>();

        var sampleRows = sampleJson.Select(ImportJson.ParseRaw).ToList();
        var headers = sampleRows[0].Select(p => p.Key).ToList();

        var aliasRows = await (
            from a in _db.TemplateFieldAliases.AsNoTracking()
            join f in _db.TemplateFields on a.TemplateFieldId equals f.Id
            where f.TemplateVersionId == batch.TemplateVersionId
            select new { f.Id, a.NormalizedAlias }).ToListAsync(ct);

        var aliasesByField = aliasRows
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.NormalizedAlias).ToList());

        var matchFields = fieldEntities
            .Select(f => new MatchField(
                f.Id,
                f.FieldKey,
                f.Label,
                aliasesByField.TryGetValue(f.Id, out var list) ? list : Array.Empty<string>()))
            .ToList();

        var hints = new List<ProfileRuleHint>();

        if (batch.MappingProfileId.HasValue)
        {
            var keyById = fieldEntities.ToDictionary(f => f.Id, f => f.FieldKey);

            var rules = await _db.MappingRules
                .AsNoTracking()
                .Where(r => r.MappingProfileId == batch.MappingProfileId.Value)
                .ToListAsync(ct);

            foreach (var r in rules)
            {
                if (keyById.TryGetValue(r.TargetFieldId, out var key))
                    hints.Add(new ProfileRuleHint(r.NormalizedSourceColumn, key, r.MappingMethod, r.Confidence));
            }
        }

        var suggestions = ColumnMatcher.Suggest(headers, matchFields, hints);

        return suggestions
            .Select(s => new ImportColumnDto(
                s.Header,
                sampleRows
                    .Select(row => row.FirstOrDefault(p => p.Key == s.Header).Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v!)
                    .ToList(),
                s.FieldKey,
                s.Method,
                s.Confidence))
            .ToList();
    }

    private async Task<PagedResult<ImportRowItemDto>> BuildRowsAsync(
        long batchId,
        GetImportBatchQuery request,
        CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 10, 200);

        var query = _db.ImportRows
            .AsNoTracking()
            .Where(r => r.ImportBatchId == batchId);

        if (request.RowStatus.HasValue)
        {
            var status = request.RowStatus.Value;
            query = query.Where(r => r.Status == status);
        }

        var total = await query.CountAsync(ct);

        var entities = await query
            .OrderBy(r => r.RowNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new { r.Id, r.RowNumber, r.Status, r.ErrorCount, r.ErrorJson, r.MappedDataJson })
            .ToListAsync(ct);

        var items = entities
            .Select(r => new ImportRowItemDto(
                r.Id,
                r.RowNumber,
                r.Status,
                r.ErrorCount,
                ImportJson.ReadErrors(r.ErrorJson),
                ImportJson.Preview(r.MappedDataJson)))
            .ToList();

        return new PagedResult<ImportRowItemDto>(items, page, pageSize, total);
    }
}