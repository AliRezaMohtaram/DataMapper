using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Application.Common.Results;
using Borc.DataMapper.Domain.Templates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Templates.SetTemplateUnits;

/// <summary>
/// واحدهای مجاز قالب برای ثبت داده و ایمپورت را جایگزین می‌کند (کلیدهای پایدار چارت؛ وب آن‌ها را از واحدهای فعال
/// چارت انتخاب و بررسی می‌کند). Replaces the template's allowed units.
/// </summary>
public sealed record SetTemplateUnitsCommand(long TemplateId, IReadOnlyCollection<string> OrgUnitKeys) : IRequest<Result>;

public sealed class SetTemplateUnitsHandler(IAppDbContext db) : IRequestHandler<SetTemplateUnitsCommand, Result>
{
    public async Task<Result> Handle(SetTemplateUnitsCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Templates.AnyAsync(t => t.Id == request.TemplateId, cancellationToken))
            return Result.Failure("قالب موردنظر پیدا نشد.");

        HashSet<string> wanted = request.OrgUnitKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<TemplateOrgUnit> current = await db.TemplateOrgUnits
            .Where(u => u.TemplateId == request.TemplateId)
            .ToListAsync(cancellationToken);

        db.TemplateOrgUnits.RemoveRange(current.Where(u => !wanted.Contains(u.OrgUnitKey)));
        HashSet<string> kept = current.Select(u => u.OrgUnitKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        db.TemplateOrgUnits.AddRange(wanted.Where(k => !kept.Contains(k)).Select(k => TemplateOrgUnit.Create(request.TemplateId, k)));

        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok("واحدهای مجاز قالب ذخیره شد.");
    }
}
