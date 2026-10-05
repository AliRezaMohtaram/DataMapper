using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Imports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Imports.DeleteImportBatch;

/// <summary>حذف منطقی دسته و سطرهایش. دستهٔ ثبت‌شده (Imported) چون رکوردها به آن وابسته‌اند حذف نمی‌شود.</summary>
public sealed record DeleteImportBatchCommand(long Id) : IRequest<Result>;

public sealed class DeleteImportBatchHandler
    : IRequestHandler<DeleteImportBatchCommand, Result>
{
    private readonly IAppDbContext _db;

    public DeleteImportBatchHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        DeleteImportBatchCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (batch is null)
            return Result.Failure("ایمپورت موردنظر پیدا نشد.");

        if (batch.Status == ImportBatchStatus.Imported)
            return Result.Failure("داده‌های ثبت‌شده به این ایمپورت وابسته‌اند و حذف نمی‌شود.");

        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // سطرها ممکن است ده‌ها هزار تا باشند؛ حذف منطقی با یک دستور SQL
        await _db.ImportRows
            .Where(r => r.ImportBatchId == batch.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.IsDeleted, true)
                .SetProperty(r => r.DeletedAt, (DateTime?)now),
                cancellationToken);

        batch.MarkAsDeleted();
        await _db.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);

        return Result.Ok("ایمپورت حذف شد.");
    }
}