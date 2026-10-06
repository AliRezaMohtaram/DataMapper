using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Mappings;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.MappingProfiles.UpdateMappingProfile;

/// <summary>نسخهٔ پروفایل پس از ساخت تغییر نمی‌کند (قاعده‌ها به فیلدهای همان نسخه وصل‌اند).</summary>
public sealed record UpdateMappingProfileCommand(
    long Id,
    string Name,
    ImportSourceType SourceType
) : IRequest<Result>;

public sealed class UpdateMappingProfileValidator
    : AbstractValidator<UpdateMappingProfileCommand>
{
    public UpdateMappingProfileValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.SourceType).IsInEnum();
    }
}

public sealed class UpdateMappingProfileHandler
    : IRequestHandler<UpdateMappingProfileCommand, Result>
{
    private readonly IAppDbContext _db;

    public UpdateMappingProfileHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        UpdateMappingProfileCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await _db.MappingProfiles
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (profile is null)
            return Result.Failure("پروفایل نگاشت موردنظر پیدا نشد.");

        var name = request.Name.Trim();

        var exists = await _db.MappingProfiles.AnyAsync(
            p => p.TemplateVersionId == profile.TemplateVersionId && p.Name == name && p.Id != profile.Id,
            cancellationToken);

        if (exists)
            return Result.Failure("پروفایلی با این نام برای این نسخه وجود دارد.");

        profile.Update(name, request.SourceType);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result.Failure("ویرایش پروفایل انجام نشد؛ احتمالاً نام تکراری است.");
        }

        return Result.Ok("پروفایل نگاشت ویرایش شد.");
    }
}
