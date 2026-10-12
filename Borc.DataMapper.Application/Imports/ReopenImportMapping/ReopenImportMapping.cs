using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Imports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Imports.ReopenImportMapping;

/// <summary>بازگشت از «اعتبارسنجی‌شده» به مرحلهٔ تطبیق ستون‌ها.</summary>
public sealed record ReopenImportMappingCommand(long Id) : IRequest<Result>;

public sealed class ReopenImportMappingHandler
    : IRequestHandler<ReopenImportMappingCommand, Result>
{
    private readonly IAppDbContext _db;

    public ReopenImportMappingHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        ReopenImportMappingCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (batch is null)
            return Result.Failure("ایمپورت موردنظر پیدا نشد.");

        // an interrupted commit has already turned some rows into records: validating again would commit them twice
        if (await _db.ImportRows.AnyAsync(r => r.ImportBatchId == batch.Id && r.Status == ImportRowStatus.Imported, cancellationToken))
            return Result.Failure("بخشی از سطرها ثبت نهایی شده‌اند؛ تطبیق دوباره ممکن نیست. ثبت نهایی را ادامه دهید.");

        if (batch.Status != ImportBatchStatus.Validated)
            return Result.Failure("فقط ایمپورت اعتبارسنجی‌شده را می‌توان دوباره تطبیق داد.");

        batch.ReopenForMapping();

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok("می‌توانید تطبیق ستون‌ها را تغییر دهید.");
    }
}