using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.MappingProfiles.DeleteMappingProfile;

/// <summary>حذف منطقی پروفایل و قاعده‌هایش. دسته‌های ایمپورت قبلی همچنان سالم می‌مانند.</summary>
public sealed record DeleteMappingProfileCommand(long Id) : IRequest<Result>;

public sealed class DeleteMappingProfileHandler
    : IRequestHandler<DeleteMappingProfileCommand, Result>
{
    private readonly IAppDbContext _db;

    public DeleteMappingProfileHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        DeleteMappingProfileCommand request,
        CancellationToken cancellationToken)
    {
        var exists = await _db.MappingProfiles.AnyAsync(p => p.Id == request.Id, cancellationToken);

        if (!exists)
            return Result.Failure("پروفایل نگاشت موردنظر پیدا نشد.");

        var now = DateTime.UtcNow;

        return await _db.InTransactionAsync(async () =>
        {
            await _db.MappingRules
                .Where(r => r.MappingProfileId == request.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.IsDeleted, true)
                    .SetProperty(r => r.DeletedAt, (DateTime?)now),
                    cancellationToken);

            var profile = await _db.MappingProfiles
                .FirstAsync(p => p.Id == request.Id, cancellationToken);

            profile.MarkAsDeleted();
            await _db.SaveChangesAsync(cancellationToken);

            return Result.Ok("پروفایل نگاشت حذف شد.");
        }, cancellationToken);
    }
}
