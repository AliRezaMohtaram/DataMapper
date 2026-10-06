using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.MappingProfiles.SetMappingProfileStatus;

public sealed record SetMappingProfileStatusCommand(long Id, bool IsActive) : IRequest<Result>;

public sealed class SetMappingProfileStatusHandler
    : IRequestHandler<SetMappingProfileStatusCommand, Result>
{
    private readonly IAppDbContext _db;

    public SetMappingProfileStatusHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        SetMappingProfileStatusCommand request,
        CancellationToken cancellationToken)
    {
        var profile = await _db.MappingProfiles
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (profile is null)
            return Result.Failure("پروفایل نگاشت موردنظر پیدا نشد.");

        if (request.IsActive)
            profile.Activate();
        else
            profile.Deactivate();

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok(request.IsActive ? "پروفایل فعال شد." : "پروفایل غیرفعال شد.");
    }
}
