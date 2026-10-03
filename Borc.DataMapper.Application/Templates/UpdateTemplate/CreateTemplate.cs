using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Templates.UpdateTemplate;

/// <summary>Code و Status از این مسیر تغییر نمی‌کنند.</summary>
public sealed record UpdateTemplateCommand(
    long Id,
    string Name,
    string? Description
) : IRequest<Result>;

public sealed class UpdateTemplateValidator
    : AbstractValidator<UpdateTemplateCommand>
{
    public UpdateTemplateValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000);
    }
}

public sealed class UpdateTemplateHandler
    : IRequestHandler<UpdateTemplateCommand, Result>
{
    private readonly IAppDbContext _db;

    public UpdateTemplateHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        UpdateTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var template = await _db.Templates
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (template is null)
            return Result.Failure("قالب موردنظر پیدا نشد.");

        template.Update(request.Name, request.Description);

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok("قالب ویرایش شد.");
    }
}