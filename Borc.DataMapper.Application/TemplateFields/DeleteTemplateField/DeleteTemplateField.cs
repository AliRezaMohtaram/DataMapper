using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.TemplateFields.DeleteTemplateField;

/// <summary>حذف منطقی فیلد و aliasهایش. خروجی: شناسه نسخه.</summary>
public sealed record DeleteTemplateFieldCommand(long Id) : IRequest<Result<long>>;

public sealed class DeleteTemplateFieldHandler
    : IRequestHandler<DeleteTemplateFieldCommand, Result<long>>
{
    private readonly IAppDbContext _db;

    public DeleteTemplateFieldHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<long>> Handle(
        DeleteTemplateFieldCommand request,
        CancellationToken cancellationToken)
    {
        var field = await _db.TemplateFields
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);

        if (field is null)
            return Result<long>.Failure("فیلد موردنظر پیدا نشد.");

        var version = await _db.TemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == field.TemplateVersionId, cancellationToken);

        if (version is null || !version.IsEditable)
            return Result<long>.Failure("فقط فیلدهای نسخه پیش‌نویس قابل حذف‌اند.");

        var usedByRules = await _db.MappingRules
            .AnyAsync(r => r.TargetFieldId == field.Id, cancellationToken);

        if (usedByRules)
            return Result<long>.Failure("این فیلد در قاعده‌های نگاشت استفاده شده و حذف نمی‌شود.");

        var aliases = await _db.TemplateFieldAliases
            .Where(a => a.TemplateFieldId == field.Id)
            .ToListAsync(cancellationToken);

        foreach (var a in aliases)
            a.MarkAsDeleted();

        field.MarkAsDeleted();

        await _db.SaveChangesAsync(cancellationToken);

        return Result<long>.Ok(field.TemplateVersionId, "فیلد حذف شد.");
    }
}