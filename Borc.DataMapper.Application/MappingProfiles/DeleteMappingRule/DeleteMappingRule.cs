using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.MappingProfiles.DeleteMappingRule;

/// <summary>حذف منطقی قاعده؛ شناسهٔ پروفایل را برای بازگشت به صفحهٔ جزئیات برمی‌گرداند.</summary>
public sealed record DeleteMappingRuleCommand(long Id) : IRequest<Result<long>>;

public sealed class DeleteMappingRuleHandler
    : IRequestHandler<DeleteMappingRuleCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public DeleteMappingRuleHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        DeleteMappingRuleCommand request,
        CancellationToken cancellationToken)
    {
        var rule = await _db.MappingRules
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (rule is null)
            return Result<long>.Failure("قاعدهٔ موردنظر پیدا نشد.");

        rule.MarkAsDeleted();
        await _db.SaveChangesAsync(cancellationToken);

        return Result<long>.Ok(rule.MappingProfileId, "قاعده حذف شد.");
    }
}
