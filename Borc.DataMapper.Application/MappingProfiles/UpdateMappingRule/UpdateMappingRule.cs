using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Mappings;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.MappingProfiles.UpdateMappingRule;

/// <summary>عنوان ستون ثابت می‌ماند؛ برای تغییر آن قاعده را حذف و دوباره اضافه کنید.</summary>
public sealed record UpdateMappingRuleCommand(
    long Id,
    long TargetFieldId,
    int SortOrder
) : IRequest<Result<long>>;

public sealed class UpdateMappingRuleValidator
    : AbstractValidator<UpdateMappingRuleCommand>
{
    public UpdateMappingRuleValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.TargetFieldId).GreaterThan(0).WithMessage("فیلد مقصد را انتخاب کنید.");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 100000);
    }
}

public sealed class UpdateMappingRuleHandler
    : IRequestHandler<UpdateMappingRuleCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public UpdateMappingRuleHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        UpdateMappingRuleCommand request,
        CancellationToken cancellationToken)
    {
        var rule = await _db.MappingRules
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (rule is null)
            return Result<long>.Failure("قاعدهٔ موردنظر پیدا نشد.");

        var profile = await _db.MappingProfiles
            .AsNoTracking()
            .FirstAsync(p => p.Id == rule.MappingProfileId, cancellationToken);

        var fieldOk = await _db.TemplateFields.AnyAsync(
            f => f.Id == request.TargetFieldId && f.TemplateVersionId == profile.TemplateVersionId,
            cancellationToken);

        if (!fieldOk)
            return Result<long>.Failure("فیلد انتخاب‌شده به نسخهٔ این پروفایل تعلق ندارد.");

        var duplicate = await _db.MappingRules.AnyAsync(
            r => r.MappingProfileId == rule.MappingProfileId
                 && r.TargetFieldId == request.TargetFieldId
                 && r.Id != rule.Id,
            cancellationToken);

        if (duplicate)
            return Result<long>.Failure("این فیلد قبلاً به ستون دیگری نگاشت شده است.");

        // تغییر دستی، روش را Manual و اطمینان را پاک می‌کند.
        rule.Update(request.TargetFieldId, MappingMethod.Manual, null, rule.TransformJson, rule.ValidationJson, request.SortOrder);

        await _db.SaveChangesAsync(cancellationToken);

        return Result<long>.Ok(rule.MappingProfileId, "قاعده ویرایش شد.");
    }
}
