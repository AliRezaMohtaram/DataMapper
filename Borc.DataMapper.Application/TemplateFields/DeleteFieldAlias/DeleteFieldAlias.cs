using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateFields.DeleteFieldAlias;

/// <summary>حذف منطقی alias. خروجی: شناسه فیلد (برای بازگشت به صفحه ویرایش فیلد).</summary>
public sealed record DeleteFieldAliasCommand(long Id) : IRequest<Result<long>>;

public sealed class DeleteFieldAliasHandler
    : IRequestHandler<DeleteFieldAliasCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public DeleteFieldAliasHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        DeleteFieldAliasCommand request,
        CancellationToken cancellationToken)
    {
        var alias = await _db.TemplateFieldAliases
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (alias is null)
            return Result<long>.Failure("alias موردنظر پیدا نشد.");

        var versionStatus = await (
            from f in _db.TemplateFields
            join v in _db.TemplateVersions on f.TemplateVersionId equals v.Id
            where f.Id == alias.TemplateFieldId
            select v.Status).FirstOrDefaultAsync(cancellationToken);

        if (versionStatus != TemplateVersionStatus.Draft)
            return Result<long>.Failure("فقط aliasهای فیلدهای نسخه پیش‌نویس قابل حذف‌اند.");

        alias.MarkAsDeleted();

        await _db.SaveChangesAsync(cancellationToken);

        return Result<long>.Ok(alias.TemplateFieldId, "alias حذف شد.");
    }
}