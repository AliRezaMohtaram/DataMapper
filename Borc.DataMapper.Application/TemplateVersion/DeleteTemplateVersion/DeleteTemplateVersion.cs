using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateVersions.DeleteTemplateVersion;

/// <summary>حذف منطقی نسخه پیش‌نویس همراه فیلدها، aliasها و Layoutهایش. خروجی: شناسه قالب.</summary>
public sealed record DeleteTemplateVersionCommand(long Id) : IRequest<Result<long>>;

public sealed class DeleteTemplateVersionHandler
    : IRequestHandler<DeleteTemplateVersionCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public DeleteTemplateVersionHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        DeleteTemplateVersionCommand request,
        CancellationToken cancellationToken)
    {
        var version = await _db.TemplateVersions
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (version is null)
            return Result<long>.Failure("نسخه موردنظر پیدا نشد.");

        if (version.Status != TemplateVersionStatus.Draft)
            return Result<long>.Failure("فقط نسخه پیش‌نویس قابل حذف است.");

        var id = version.Id;

        var inUse =
            await _db.MappingProfiles.AnyAsync(p => p.TemplateVersionId == id, cancellationToken) ||
            await _db.ImportBatches.AnyAsync(b => b.TemplateVersionId == id, cancellationToken) ||
            await _db.DataRecords.AnyAsync(r => r.TemplateVersionId == id, cancellationToken);

        if (inUse)
            return Result<long>.Failure("این نسخه در پروفایل نگاشت، ایمپورت یا داده‌ها استفاده شده و حذف نمی‌شود.");

        var fields = await _db.TemplateFields
            .Where(f => f.TemplateVersionId == id)
            .ToListAsync(cancellationToken);

        var fieldIds = fields.Select(f => f.Id).ToList();

        var aliases = await _db.TemplateFieldAliases
            .Where(a => fieldIds.Contains(a.TemplateFieldId))
            .ToListAsync(cancellationToken);

        var layouts = await _db.TemplateLayouts
            .Where(l => l.TemplateVersionId == id)
            .ToListAsync(cancellationToken);

        foreach (var a in aliases) a.MarkAsDeleted();
        foreach (var f in fields) f.MarkAsDeleted();
        foreach (var l in layouts) l.MarkAsDeleted();
        version.MarkAsDeleted();

        await _db.SaveChangesAsync(cancellationToken);

        return Result<long>.Ok(version.TemplateId, "نسخه حذف شد.");
    }
}