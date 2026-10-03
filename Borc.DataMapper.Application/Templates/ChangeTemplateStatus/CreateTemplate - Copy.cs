using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Templates.ChangeTemplateStatus;

public sealed record ChangeTemplateStatusCommand(
    long Id,
    TemplateStatus Status
) : IRequest<Result>;

public sealed class ChangeTemplateStatusHandler
    : IRequestHandler<ChangeTemplateStatusCommand, Result>
{
    private readonly IAppDbContext _db;

    public ChangeTemplateStatusHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        ChangeTemplateStatusCommand request,
        CancellationToken cancellationToken)
    {
        var template = await _db.Templates
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (template is null)
            return Result.Failure("قالب موردنظر پیدا نشد.");

        if (!template.CanChangeStatusTo(request.Status))
            return Result.Failure("این تغییر وضعیت برای قالب مجاز نیست.");

        // TODO: قاعده فعال‌سازی (مثلاً داشتن حداقل یک نسخه منتشرشده) بعد از Sliceهای TemplateVersion.
        template.ChangeStatus(request.Status);

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok("وضعیت قالب تغییر کرد.");
    }
}