using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Templates.CreateTemplate;

public sealed record CreateTemplateCommand(
    string Code,
    string Name,
    string? Description
) : IRequest<Result<long>>;

public sealed class CreateTemplateValidator
    : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("Code can only contain letters, numbers, _ and -.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(1000);
    }
}

public sealed class CreateTemplateHandler
    : IRequestHandler<CreateTemplateCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public CreateTemplateHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        CreateTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        // collation دیتابیس CI است؛ فیلتر سراسری حذف‌شده‌ها را کنار می‌گذارد.
     
        var exists = await _db.Templates
            .AnyAsync(x => x.Code == code, cancellationToken);

        if (exists)
            return Result<long>.Failure("قالبی با این کد از قبل وجود دارد.");

        var template = Template.Create(code, request.Name, request.Description);

        _db.Templates.Add(template);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // رقابت هم‌زمان: ایندکس یکتای UX_Template_Code
            return Result<long>.Failure("ذخیره قالب انجام نشد؛ احتمالاً کد تکراری است.");
        }

        return Result<long>.Ok(template.Id, "قالب ایجاد شد.");
    }
}