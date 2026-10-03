using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Common;
using Borc.DataMapper.Domain.Templates;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateFields.AddFieldAlias;

public sealed record AddFieldAliasCommand(long TemplateFieldId, string Alias) : IRequest<Result>;

public sealed class AddFieldAliasValidator : AbstractValidator<AddFieldAliasCommand>
{
    public AddFieldAliasValidator()
    {
        RuleFor(x => x.TemplateFieldId).GreaterThan(0);

        RuleFor(x => x.Alias)
            .NotEmpty()
            .MaximumLength(250);
    }
}

public sealed class AddFieldAliasHandler
    : IRequestHandler<AddFieldAliasCommand, Result>
{
    private readonly IAppDbContext _db;

    public AddFieldAliasHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        AddFieldAliasCommand request,
        CancellationToken cancellationToken)
    {
        var field = await _db.TemplateFields
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.TemplateFieldId, cancellationToken);

        if (field is null)
            return Result.Failure("فیلد موردنظر پیدا نشد.");

        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == field.TemplateVersionId, cancellationToken);

        if (version is null || !version.IsEditable)
            return Result.Failure("فقط aliasهای فیلدهای نسخه پیش‌نویس قابل تغییرند.");

        var normalized = TextNormalizer.NormalizeHeader(request.Alias);

        if (normalized.Length == 0)
            return Result.Failure("alias نمی‌تواند خالی باشد.");

        // یک alias در یک نسخه فقط به یک فیلد تعلق دارد؛ وگرنه تطبیق سرستون مبهم می‌شود.
        var duplicate = await (
            from a in _db.TemplateFieldAliases
            join f in _db.TemplateFields on a.TemplateFieldId equals f.Id
            where f.TemplateVersionId == version.Id && a.NormalizedAlias == normalized
            select a.Id).AnyAsync(cancellationToken);

        if (duplicate)
            return Result.Failure("این alias برای یکی از فیلدهای همین نسخه ثبت شده است.");

        _db.TemplateFieldAliases.Add(TemplateFieldAlias.Create(field.Id, request.Alias));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure("ذخیره alias انجام نشد؛ احتمالاً تکراری است.");
        }

        return Result.Ok("alias اضافه شد.");
    }
}