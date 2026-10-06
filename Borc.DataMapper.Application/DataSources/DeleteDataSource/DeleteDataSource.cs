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
        var exists = await _db.DataSources.AnyAsync(x => x.Id == request.Id, cancellationToken);

        if (!exists)
            return Result.Failure("منبع داده موردنظر پیدا نشد.");

        var used = await _db.TemplateFields
            .AnyAsync(f => f.DataSourceId == request.Id, cancellationToken);

        if (used)
            return Result.Failure("این منبع داده توسط فیلدهای قالب‌ها استفاده می‌شود و حذف نمی‌شود.");

        var now = DateTime.UtcNow;

        return await _db.InTransactionAsync(async () =>
        {
            await _db.DataSourceItems
                .Where(i => i.DataSourceId == request.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.IsDeleted, true)
                    .SetProperty(i => i.DeletedAt, (DateTime?)now),
                    cancellationToken);

            var ds = await _db.DataSources.FirstAsync(x => x.Id == request.Id, cancellationToken);
            ds.MarkAsDeleted();
            await _db.SaveChangesAsync(cancellationToken);

            return Result.Ok("منبع داده حذف شد.");
        }, cancellationToken);
    }
}
