using System.Text.Json;
using Borc.DataMapper.Application.Abstractions.Imports;
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
/// Checks the column mapping, stores it on the batch and queues the validation of every row (RunImportJob, in the
/// background). SaveProfileName set → the mapping is also saved as a new MappingProfile.
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
    private readonly IImportJobQueue _queue;

    public ApplyImportMappingHandler(IAppDbContext db, IImportJobQueue queue)
    {
        _db = db;
        _queue = queue;
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

        // header → field key; the background validation applies it to every row
        var mappingJson = JsonSerializer.Serialize(headerToField.ToDictionary(kv => kv.Key, kv => kv.Value.Key));

        try
        {
            await _db.InTransactionAsync(async () =>
            {
                var b = await _db.ImportBatches.FirstAsync(x => x.Id == batchId, cancellationToken);

                if (profileName is not null)
                    await SaveProfileAsync(b, version.Id, profileName, headerToField, cancellationToken);

                b.StartValidation(mappingJson);
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure("شروع اعتبارسنجی انجام نشد؛ دوباره تلاش کنید.");
        }

        _queue.Enqueue(batchId);

        return Result.Ok("اعتبارسنجی شروع شد؛ پیشرفت آن در همین صفحه نمایش داده می‌شود.");
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