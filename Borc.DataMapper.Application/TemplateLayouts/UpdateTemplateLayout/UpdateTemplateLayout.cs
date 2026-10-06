using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateLayouts.UpdateTemplateLayout;

/// <summary>حذف منطقی Layout یک نسخه پیش‌نویس؛ ورود داده به فرم داینامیک برمی‌گردد.</summary>
//public sealed record UpdateTemplateLayoutCommand(long TemplateLayoutVersionId) : IRequest<Result>;
public sealed record UpdateTemplateLayoutCommand(
    long TemplateLayoutVersionId,
     string LayoutJson
) : IRequest<Result>;


public sealed class UpdateTemplateLayoutHandler
    : IRequestHandler<UpdateTemplateLayoutCommand, Result>
{
    private readonly IAppDbContext _db;

    public UpdateTemplateLayoutHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        UpdateTemplateLayoutCommand request,
        CancellationToken cancellationToken)
    {
        var versionLayout = await _db.TemplateLayouts
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.TemplateLayoutVersionId, cancellationToken);

        if (versionLayout is null)
            return Result.Failure("قالب موردنظر پیدا نشد.");

        versionLayout.UpdateLayout(request.LayoutJson);

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok("قالب ویرایش شد.");
    }
}
