using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Mappings;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.MappingProfiles.AddMappingRule;

/// <summary>قاعدهٔ دستی: ستونی با این عنوان در فایل، به فیلد مقصد نگاشت می‌شود.</summary>
public sealed record AddMappingRuleCommand(
    long MappingProfileId,
    string SourceColumn,
    long TargetFieldId,
    int SortOrder = 0
) : IRequest<Result<long>>;

public sealed class AddMappingRuleValidator
    : AbstractValidator<AddMappingRuleCommand>
{
    public AddMappingRuleValidator()
    {
        RuleFor(x => x.MappingProfileId).GreaterThan(0);
        RuleFor(x => x.SourceColumn).NotEmpty().MaximumLength(250);
        RuleFor(x => x.TargetFieldId).GreaterThan(0).WithMessage("فیلد مقصد را انتخاب کنید.");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 100000);
    }
}

public sealed class AddMappingRuleHandler
    : IRequestHandler<AddMappingRuleCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public AddMappingRuleHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        AddMappingRuleCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await _db.MappingProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.MappingProfileId, cancellationToken);

        if (profile is null)
            return Result<long>.Failure("پروفایل نگاشت موردنظر پیدا نشد.");

        var fieldOk = await _db.TemplateFields.AnyAsync(
            f => f.Id == request.TargetFieldId && f.TemplateVersionId == profile.TemplateVersionId,
            cancellationToken);

        if (!fieldOk)
            return Result<long>.Failure("فیلد انتخاب‌شده به نسخهٔ این پروفایل تعلق ندارد.");

        var source = request.SourceColumn.Trim();
        var normalized = TextNormalizer.NormalizeHeader(source);

        if (normalized.Length == 0)
            return Result<long>.Failure("عنوان ستون معتبر نیست.");

        var existing = await _db.MappingRules
            .AsNoTracking()
            .Where(r => r.MappingProfileId == profile.Id)
            .Select(r => new { r.NormalizedSourceColumn, r.TargetFieldId })
            .ToListAsync(cancellationToken);

        if (existing.Any(r => r.NormalizedSourceColumn == normalized))
            return Result<long>.Failure("برای این عنوان ستون قبلاً قاعده ثبت شده است.");

        if (existing.Any(r => r.TargetFieldId == request.TargetFieldId))
            return Result<long>.Failure("این فیلد قبلاً به ستون دیگری نگاشت شده است؛ هر فیلد فقط یک قاعده دارد.");

        var rule = MappingRule.Create(
            profile.Id, source, request.TargetFieldId, MappingMethod.Manual,
            sortOrder: request.SortOrder);

        _db.MappingRules.Add(rule);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<long>.Failure("ذخیرهٔ قاعده انجام نشد؛ احتمالاً عنوان ستون تکراری است.");
        }

        return Result<long>.Ok(rule.Id, "قاعده اضافه شد.");
    }
}
