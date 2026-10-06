using System.Text.Json;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.DataRecords.Common;
using Borc.DataMapper.Application.DataSources.Common;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.GetRecordForm;

/// <summary>
/// تعریف فرم ورود داده: برای رکورد جدید با TemplateVersionId، برای ویرایش با RecordId.
/// </summary>
public sealed record GetRecordFormQuery(long? TemplateVersionId = null, long? RecordId = null)
    : IRequest<Result<RecordFormDto>>;

public sealed record RecordFormDto(
    long TemplateVersionId,
    long TemplateId,
    string TemplateName,
    int VersionNo,
    TemplateVersionStatus VersionStatus,
    long? RecordId,
    string? LayoutJson,
    IReadOnlyList<RecordFormFieldDto> Fields,
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyDictionary<string, string> Labels);

public sealed record RecordFormFieldDto(
    string Key,
    string Label,
    FieldDataType DataType,
    string DbType,
    bool IsRequired,
    int? Length,
    byte? Precision,
    byte? Scale,
    string? Regex,
    IReadOnlyList<DataSourceOption>? Options,
    long? DataSourceId);

public sealed class GetRecordFormHandler
    : IRequestHandler<GetRecordFormQuery, Result<RecordFormDto>>
{
    private readonly IAppDbContext _db;
    private readonly DataSourceOptionService _options;

    public GetRecordFormHandler(IAppDbContext db, DataSourceOptionService options)
    {
        _db = db;
        _options = options;
    }

    public async Task<Result<RecordFormDto>> Handle(
        GetRecordFormQuery request,
        CancellationToken cancellationToken)
    {
        var versionId = request.TemplateVersionId;
        string? dataJson = null;

        if (request.RecordId.HasValue)
        {
            var record = await _db.DataRecords
                .AsNoTracking()
                .Where(r => r.Id == request.RecordId.Value)
                .Select(r => new { r.TemplateVersionId, r.DataJson })
                .FirstOrDefaultAsync(cancellationToken);

            if (record is null)
                return Result<RecordFormDto>.Failure("رکورد موردنظر پیدا نشد.");

            versionId = record.TemplateVersionId;
            dataJson = record.DataJson;
        }

        if (!versionId.HasValue)
            return Result<RecordFormDto>.Failure("نسخهٔ قالب مشخص نشده است.");

        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId.Value, cancellationToken);

        if (version is null)
            return Result<RecordFormDto>.Failure("نسخهٔ موردنظر پیدا نشد.");

        if (version.Status != TemplateVersionStatus.Published)
            return Result<RecordFormDto>.Failure(
                request.RecordId.HasValue
                    ? "نسخهٔ این رکورد دیگر منتشرشده نیست؛ ویرایش داده ممکن نیست."
                    : "ورود داده فقط برای نسخهٔ منتشرشده ممکن است.");

        var templateName = await _db.Templates
            .AsNoTracking()
            .Where(t => t.Id == version.TemplateId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);

        var layoutJson = await _db.TemplateLayouts
            .AsNoTracking()
            .Where(l => l.TemplateVersionId == version.Id)
            .OrderByDescending(l => l.VersionNo)
            .Select(l => l.LayoutJson)
            .FirstOrDefaultAsync(cancellationToken);

        var fields = await RecordDataBuilder.LoadFieldsAsync(_db, version.Id, cancellationToken);

        var dbTypes = await _db.TemplateFields
            .AsNoTracking()
            .Where(f => f.TemplateVersionId == version.Id)
            .ToDictionaryAsync(f => f.Id, f => f.DbType, cancellationToken);

        var dtos = fields
            .Select(f => new RecordFormFieldDto(
                f.Target.Key, f.Target.Label, f.Target.DataType,
                dbTypes.TryGetValue(f.Target.Id, out var dbt) ? dbt : string.Empty,
                f.Target.IsRequired, f.Target.Length, f.Target.Precision, f.Target.Scale,
                f.Target.Regex, f.Inline, f.IsRemote ? f.DataSourceId : null))
            .ToList();

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (dataJson is not null)
        {
            try
            {
                foreach (var p in ImportJson.ParseRaw(dataJson))
                    values[p.Key] = p.Value;
            }
            catch (JsonException)
            {
                // دادهٔ خراب: فرم خالی باز می‌شود
            }
        }
        else
        {
            // رکورد جدید: مقدار پیش‌فرض فیلدها
            foreach (var f in fields.Where(f => !string.IsNullOrWhiteSpace(f.Target.DefaultValue)))
                values[f.Target.Key] = f.Target.DefaultValue;
        }

        // عنوان مقدار فعلیِ فیلدهایی که گزینه‌هایشان با جستجوی سمت سرور می‌آید (برای نمایش در Combobox)
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in fields.Where(f => f.IsRemote))
        {
            if (!values.TryGetValue(f.Target.Key, out var current) || string.IsNullOrWhiteSpace(current))
                continue;

            var found = await _options.FindAsync(f.DataSourceId!.Value, current, cancellationToken);

            if (found.Option is not null)
                labels[f.Target.Key] = found.Option.Label;
        }

        return Result<RecordFormDto>.Ok(new RecordFormDto(
            version.Id, version.TemplateId, templateName ?? string.Empty, version.VersionNo,
            version.Status, request.RecordId, layoutJson, dtos, values, labels));
    }
}
