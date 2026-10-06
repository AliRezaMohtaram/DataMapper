using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Mappings;
using Borc.DataMapper.Domain.Templates;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.MappingProfiles.CreateMappingProfile;

public sealed record CreateMappingProfileCommand(
    string Name,
    long TemplateVersionId,
    ImportSourceType SourceType
) : IRequest<Result<long>>;

public sealed class CreateMappingProfileValidator
    : AbstractValidator<CreateMappingProfileCommand>
{
    public CreateMappingProfileValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.TemplateVersionId).GreaterThan(0).WithMessage("نسخه قالب را انتخاب کنید.");
        RuleFor(x => x.SourceType).IsInEnum();
    }
}

public sealed class CreateMappingProfileHandler
    : IRequestHandler<CreateMappingProfileCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public CreateMappingProfileHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        CreateMappingProfileCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.TemplateVersionId, cancellationToken);

        if (version is null)
            return Result<long>.Failure("نسخهٔ انتخاب‌شده پیدا نشد.");

        if (version.Status == TemplateVersionStatus.Archived)
            return Result<long>.Failure("برای نسخهٔ بایگانی‌شده نمی‌توان پروفایل ساخت.");

        var name = request.Name.Trim();

        var exists = await _db.MappingProfiles.AnyAsync(
            p => p.TemplateVersionId == version.Id && p.Name == name, cancellationToken);

        if (exists)
            return Result<long>.Failure("پروفایلی با این نام برای این نسخه وجود دارد.");

        var profile = MappingProfile.Create(name, version.Id, request.SourceType);
        _db.MappingProfiles.Add(profile);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<long>.Failure("ذخیرهٔ پروفایل انجام نشد؛ احتمالاً نام تکراری است.");
        }

        return Result<long>.Ok(profile.Id, "پروفایل نگاشت ایجاد شد؛ قاعده‌ها را اضافه کنید.");
    }
}
