using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataSources.DeleteDataSource;

public sealed record DeleteDataSourceCommand(long Id) : IRequest<Result>;

public sealed class DeleteDataSourceHandler
    : IRequestHandler<DeleteDataSourceCommand, Result>
{
    private readonly IAppDbContext _db;

    public DeleteDataSourceHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        DeleteDataSourceCommand request,
        CancellationToken cancellationToken)
    {
        var ds = await _db.DataSources
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ds is null)
            return Result.Failure("منبع داده موردنظر پیدا نشد.");

        var used = await _db.TemplateFields
            .AnyAsync(f => f.DataSourceId == ds.Id, cancellationToken);

        if (used)
            return Result.Failure("این منبع داده توسط فیلدهای قالب‌ها استفاده می‌شود و حذف نمی‌شود.");

        ds.MarkAsDeleted();

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok("منبع داده حذف شد.");
    }
}