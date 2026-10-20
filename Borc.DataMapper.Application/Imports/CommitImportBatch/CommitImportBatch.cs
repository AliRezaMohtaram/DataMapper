using Borc.DataMapper.Application.Abstractions.Imports;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Application.Imports.Common;
using Borc.DataMapper.Domain.Imports;
using Borc.DataMapper.Domain.Records;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Imports.CommitImportBatch;

/// <summary>Queues the commit (RunImportJob, in the background): valid rows become records, invalid ones are skipped.</summary>
public sealed record CommitImportBatchCommand(long Id) : IRequest<Result>;

public sealed class CommitImportBatchHandler
    : IRequestHandler<CommitImportBatchCommand, Result>
{
    private readonly IAppDbContext _db;
    private readonly IImportJobQueue _queue;

    public CommitImportBatchHandler(IAppDbContext db, IImportJobQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task<Result> Handle(
        CommitImportBatchCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await _db.ImportBatches
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (batch is null)
            return Result.Failure("ایمپورت موردنظر پیدا نشد.");

        if (batch.Status != ImportBatchStatus.Validated)
            return Result.Failure("فقط ایمپورت اعتبارسنجی‌شده قابل ثبت نهایی است.");

        if (batch.ValidRows == 0)
            return Result.Failure("هیچ سطر معتبری برای ثبت وجود ندارد.");

        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == batch.TemplateVersionId, cancellationToken);

        if (version is null || version.Status != TemplateVersionStatus.Published)
            return Result.Failure("نسخهٔ این ایمپورت دیگر منتشرشده نیست؛ ثبت نهایی ممکن نیست.");

        if (!await _db.Templates.AnyAsync(t => t.Id == version.TemplateId
                && t.Status != Domain.Templates.TemplateStatus.Inactive && t.Status != Domain.Templates.TemplateStatus.Archived, cancellationToken))
            return Result.Failure(Templates.Entry.TemplateEntryPolicy.InactiveMessage);

        batch.StartCommit();
        await _db.SaveChangesAsync(cancellationToken);
        _queue.Enqueue(batch.Id);

        return Result.Ok("ثبت نهایی شروع شد؛ پیشرفت آن در همین صفحه نمایش داده می‌شود.");
    }
}
