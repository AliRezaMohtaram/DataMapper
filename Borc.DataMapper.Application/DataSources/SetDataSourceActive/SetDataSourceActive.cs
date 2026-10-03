using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataSources.SetDataSourceActive;

public sealed record SetDataSourceActiveCommand(long Id, bool IsActive) : IRequest<Result>;

public sealed class SetDataSourceActiveHandler
    : IRequestHandler<SetDataSourceActiveCommand, Result>
{
    private readonly IAppDbContext _db;

    public SetDataSourceActiveHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        SetDataSourceActiveCommand request,
        CancellationToken cancellationToken)
    {
        var ds = await _db.DataSources
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ds is null)
            return Result.Failure("منبع داده موردنظر پیدا نشد.");

        if (request.IsActive)
            ds.Activate();
        else
            ds.Deactivate();

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok(request.IsActive ? "منبع داده فعال شد." : "منبع داده غیرفعال شد.");
    }
}