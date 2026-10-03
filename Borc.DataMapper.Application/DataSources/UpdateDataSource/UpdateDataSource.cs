using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Common.Validation;
using Borc.DataMapper.Domain.DataSources;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataSources.UpdateDataSource;

/// <summary>Code پس از ایجاد تغییر نمی‌کند.</summary>
public sealed record UpdateDataSourceCommand(
    long Id,
    string Name,
    DataSourceType SourceType,
    string? ConfigJson
) : IRequest<Result>;

public sealed class UpdateDataSourceValidator
    : AbstractValidator<UpdateDataSourceCommand>
{
    public UpdateDataSourceValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(x => x.SourceType).IsInEnum();

        RuleFor(x => x.ConfigJson)
            .Must(ValidationRules.IsValidJson)
            .WithMessage("ConfigJson معتبر نیست؛ باید JSON درست باشد.");
    }
}

public sealed class UpdateDataSourceHandler
    : IRequestHandler<UpdateDataSourceCommand, Result>
{
    private readonly IAppDbContext _db;

    public UpdateDataSourceHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        UpdateDataSourceCommand request,
        CancellationToken cancellationToken)
    {
        var ds = await _db.DataSources
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ds is null)
            return Result.Failure("منبع داده موردنظر پیدا نشد.");

        var config = string.IsNullOrWhiteSpace(request.ConfigJson) ? null : request.ConfigJson;

        ds.Update(request.Name, request.SourceType, config);

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok("منبع داده ویرایش شد.");
    }
}