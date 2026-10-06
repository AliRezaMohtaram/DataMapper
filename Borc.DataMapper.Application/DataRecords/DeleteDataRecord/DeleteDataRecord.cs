using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.DataRecords.DeleteDataRecord;

/// <summary>حذف منطقی رکورد (دستی یا ایمپورت‌شده). ردیف ایمپورت مرتبط دست‌نخورده می‌ماند.</summary>
public sealed record DeleteDataRecordCommand(long Id) : IRequest<Result>;

public sealed class DeleteDataRecordHandler
    : IRequestHandler<DeleteDataRecordCommand, Result>
{
    private readonly IAppDbContext _db;

    public DeleteDataRecordHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        DeleteDataRecordCommand request,
        CancellationToken cancellationToken)
    {
        var record = await _db.DataRecords
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (record is null)
            return Result.Failure("رکورد موردنظر پیدا نشد.");

        record.MarkAsDeleted();
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok("رکورد حذف شد.");
    }
}
