using Borc.DataMapper.Application.Abstractions.Identity;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Templates.Entry;

/// <param name="OrgUnitKey">Unit the new record/import gets (the user's unit that the template allows).</param>
public sealed record EntryDecision(bool Allowed, string? OrgUnitKey)
{
    public static EntryDecision Denied { get; } = new(false, null);
}

/// <summary>
/// چه کسی با یک قالب داده ثبت یا ایمپورت می‌کند (تصمیم کاربر): فقط کسانی که در یکی از واحدهای مجاز قالب سمت دارند؛
/// کاربری با محدودهٔ «همه» روی داده‌ها برای هر قالبی. رکورد/ایمپورت واحد همان سمت را می‌گیرد.
/// Who may enter records or import for a template: holders of a position in one of the template's units
/// (<c>TemplateOrgUnit</c>); users whose record data scope is "all" may for every template.
/// </summary>
public interface ITemplateEntryPolicy
{
    Task<EntryDecision> DecideAsync(long templateId, CancellationToken cancellationToken = default);

    /// <summary>Templates the user may enter data for; null = all of them.</summary>
    Task<IReadOnlySet<long>?> AllowedTemplateIdsAsync(CancellationToken cancellationToken = default);
}

public sealed class TemplateEntryPolicy(IAppDbContext db, ICurrentUserUnits units) : ITemplateEntryPolicy
{
    public const string DeniedMessage = "واحد سازمانی شما جزو واحدهای مجاز این قالب نیست؛ ثبت داده و ایمپورت با آن ممکن نیست.";

    public async Task<EntryDecision> DecideAsync(long templateId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> held = await units.HeldUnitKeysAsync(cancellationToken);
        List<string> allowed = await db.TemplateOrgUnits.AsNoTracking()
            .Where(u => u.TemplateId == templateId)
            .Select(u => u.OrgUnitKey)
            .ToListAsync(cancellationToken);

        string? match = held.FirstOrDefault(h => allowed.Contains(h, StringComparer.OrdinalIgnoreCase));
        if (match is not null)
        {
            return new EntryDecision(true, match);
        }

        return units.CanEnterForAnyTemplate
            ? new EntryDecision(true, held.FirstOrDefault() ?? allowed.FirstOrDefault())
            : EntryDecision.Denied;
    }

    public async Task<IReadOnlySet<long>?> AllowedTemplateIdsAsync(CancellationToken cancellationToken = default)
    {
        if (units.CanEnterForAnyTemplate)
        {
            return null;
        }

        IReadOnlyList<string> held = await units.HeldUnitKeysAsync(cancellationToken);
        List<long> ids = await db.TemplateOrgUnits.AsNoTracking()
            .Where(u => held.Contains(u.OrgUnitKey))
            .Select(u => u.TemplateId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }
}
