using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Common.Validation;
using Borc.DataMapper.Domain.DataSources;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataSources.CreateDataSource;

public sealed record CreateDataSourceCommand(
    string Code,
    string Name,
    DataSourceType SourceType,
    string? ConfigJson
) : IRequest<Result<long>>;

public sealed class CreateDataSourceValidator
    : AbstractValidator<CreateDataSourceCommand>
{
    public CreateDataSourceValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("Code can only contain letters, numbers, _ and -.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(x => x.SourceType).IsInEnum();

        RuleFor(x => x.ConfigJson)
            .Must(ValidationRules.IsValidJson)
            .WithMessage("ConfigJson معتبر نیست؛ باید JSON درست باشد.");
    }
}

public sealed class CreateDataSourceHandler
    : IRequestHandler<CreateDataSourceCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public CreateDataSourceHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        CreateDataSourceCommand request,
        CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        var exists = await _db.DataSources
            .AnyAsync(x => x.Code == code, cancellationToken);

        if (exists)
            return Result<long>.Failure("منبع داده‌ای با این کد از قبل وجود دارد.");

        var config = string.IsNullOrWhiteSpace(request.ConfigJson) ? null : request.ConfigJson;

        var ds = DataSource.Create(code, request.Name, request.SourceType, config);
        _db.DataSources.Add(ds);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<long>.Failure("ذخیره منبع داده انجام نشد؛ احتمالاً کد تکراری است.");
        }

        return Result<long>.Ok(ds.Id, "منبع داده ایجاد شد.");
    }
}