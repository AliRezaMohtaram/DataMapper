using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateLayouts.DeleteTemplateLayout;

/// <summary>حذف منطقی Layout یک نسخه پیش‌نویس؛ ورود داده به فرم داینامیک برمی‌گردد.</summary>
public sealed record DeleteTemplateLayoutCommand(long TemplateVersionId) : IRequest<Result>;

public sealed class DeleteTemplateLayoutHandler
    : IRequestHandler<DeleteTemplateLayoutCommand, Result>
{
    private readonly IAppDbContext _db;

    public DeleteTemplateLayoutHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> Handle(
        DeleteTemplateLayoutCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.TemplateVersionId, cancellationToken);

        if (version is null)
            return Result.Failure("نسخه موردنظر پیدا نشد.");

        if (!version.IsEditable)
            return Result.Failure("فقط Layout نسخه پیش‌نویس قابل حذف است.");

        var layouts = await _db.TemplateLayouts
            .Where(l => l.TemplateVersionId == version.Id)
            .ToListAsync(cancellationToken);

        if (layouts.Count == 0)
            return Result.Failure("برای این نسخه Layout ثبت نشده است.");

        foreach (var l in layouts)
            l.MarkAsDeleted();

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok("Layout حذف شد.");
    }
}
