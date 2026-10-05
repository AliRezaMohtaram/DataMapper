using System.Text.Json;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Imports.ApplyImportMapping;

/// <summary>
/// اعمال نگاشت ستون‌ها روی همهٔ سطرها و اعتبارسنجی آن‌ها.
/// SaveProfileName پر باشد، نگاشت به‌عنوان MappingProfile جدید ذخیره می‌شود.
/// </summary>
public sealed record ApplyImportMappingCommand(
    long BatchId,
    IReadOnlyList<ColumnMappingInput> Mappings,
    string? SaveProfileName
) : IRequest<Result>;

public sealed class ApplyImportMappingHandler
    : IRequestHandler<ApplyImportMappingCommand, Result>
{
    private readonly IAppDbContext _db;

    public ApplyImportMappingHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        ApplyImportMappingCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches
            .FirstOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken);

        if (batch is null)
            return Result.Failure("ایمپورت موردنظر پیدا نشد.");

        if (batch.Status != ImportBatchStatus.Uploaded)
            return Result.Failure("تطبیق فقط برای ایمپورت آپلودشده ممکن است.");

        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == batch.TemplateVersionId, cancellationToken);

        if (version is null || version.Status != TemplateVersionStatus.Published)
            return Result.Failure("نسخهٔ این ایمپورت دیگر منتشرشده نیست.");

        var fields = (await _db.TemplateFields
                .AsNoTracking()
                .Where(f => f.TemplateVersionId == version.Id)
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Id)
                .ToListAsync(cancellationToken))
            .Select(f => new TargetField(
                f.Id, f.FieldKey, f.Label, f.DataType, f.IsRequired,
                f.Length, f.Precision, f.Scale, f.Regex, f.DefaultValue))
            .ToList();

        var byKey = fields.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);

        // ---- بررسی نگاشت ----
        var headerToField = new Dictionary<string, TargetField>(StringComparer.Ordinal);
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in request.Mappings)
        {
            if (string.IsNullOrWhiteSpace(m.TargetFieldKey))
                continue;

            if (!byKey.TryGetValue(m.TargetFieldKey.Trim(), out var target))
                return Result.Failure($"فیلد «{m.TargetFieldKey}» در این نسخه وجود ندارد.");

            if (!usedKeys.Add(target.Key))
                return Result.Failure($"فیلد «{target.Label}» به بیش از یک ستون نگاشت شده است.");

            headerToField[m.SourceColumn] = target;
        }

        if (headerToField.Count == 0)
            return Result.Failure("حداقل یک ستون باید به یک فیلد نگاشت شود.");

        var missing = fields
            .Where(f => f.IsRequired && string.IsNullOrWhiteSpace(f.DefaultValue) && !usedKeys.Contains(f.Key))
            .Select(f => f.Label)
            .ToList();

        if (missing.Count > 0)
            return Result.Failure("برای فیلدهای الزامی زیر ستونی نگاشت نشده است: " + string.Join("، ", missing));

        var profileName = string.IsNullOrWhiteSpace(request.SaveProfileName)
            ? null
            : request.SaveProfileName.Trim();

        if (profileName is not null)
        {
            var nameTaken = await _db.MappingProfiles.AnyAsync(
                p => p.TemplateVersionId == version.Id && p.Name == profileName,
                cancellationToken);

            if (nameTaken)
                return Result.Failure("پروفایلی با این نام برای این نسخه وجود دارد.");
        }

        var batchId = batch.Id;

        var headerOfField = headerToField.ToDictionary(kv => kv.Value.Key, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

        try
        {
            return await _db.InTransactionAsync(async () =>
            {
                var b = await _db.ImportBatches.FirstAsync(x => x.Id == batchId, cancellationToken);

                if (profileName is not null)
                    await SaveProfileAsync(b, version.Id, profileName, headerToField, cancellationToken);

                b.StartValidation();
                await _db.SaveChangesAsync(cancellationToken);

                var valid = 0;
                var invalid = 0;
                long lastId = 0;

                while (true)
                {
                    var currentLastId = lastId;

                    var chunk = await _db.ImportRows
                        .Where(r => r.ImportBatchId == b.Id && r.Id > currentLastId)
                        .OrderBy(r => r.Id)
                        .Take(ImportLimits.ChunkSize)
                        .ToListAsync(cancellationToken);

                    if (chunk.Count == 0)
                        break;

                    foreach (var row in chunk)
                    {
                        var raw = ImportJson.ParseRaw(row.RawDataJson)
                            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

                        var values = new List<KeyValuePair<string, object?>>();
                        var errors = new List<string>();

                        foreach (var field in fields)
                        {
                            string? rawValue = null;

                            if (headerOfField.TryGetValue(field.Key, out var header))
                                raw.TryGetValue(header, out rawValue);

                            var result = ImportValueValidator.Validate(field, rawValue);

                            values.Add(new KeyValuePair<string, object?>(field.Key, result.Value));

                            if (result.Error is not null)
                                errors.Add(result.Error);
                        }

                        row.SetMapped(ImportJson.BuildMapped(values));

                        if (errors.Count == 0)
                        {
                            row.MarkValid();
                            valid++;
                        }
                        else
                        {
                            row.MarkInvalid(errors.Count, ImportJson.BuildErrors(errors));
                            invalid++;
                        }
                    }

                    await _db.SaveChangesAsync(cancellationToken);
                    lastId = chunk[^1].Id;
                }

                b.CompleteValidation(valid + invalid, valid, invalid);
                await _db.SaveChangesAsync(cancellationToken);

                return Result.Ok($"اعتبارسنجی انجام شد: {valid} سطر معتبر و {invalid} سطر نامعتبر.");
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure("ذخیرهٔ نتیجهٔ اعتبارسنجی انجام نشد؛ دوباره تلاش کنید.");
        }
        catch (JsonException)
        {
            return Result.Failure("دادهٔ یکی از سطرها خراب است و اعتبارسنجی انجام نشد.");
        }
    }

    private async Task SaveProfileAsync(
        ImportBatch batch,
        long versionId,
        string name,
        Dictionary<string, TargetField> headerToField,
        CancellationToken ct)
    {
        var profile = MappingProfile.Create(name, versionId, ImportSourceType.Excel);
        _db.MappingProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);

        var order = 0;

        foreach (var (header, field) in headerToField)
        {
            _db.MappingRules.Add(MappingRule.Create(
                profile.Id,
                header,
                field.Id,
                MappingMethod.Manual,
                sortOrder: order++));
        }

        batch.AssignProfile(profile.Id);
        await _db.SaveChangesAsync(ct);
    }
}