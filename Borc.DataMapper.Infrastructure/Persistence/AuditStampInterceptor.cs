using Borc.DataMapper.Application.Abstractions.Identity;
using Borc.DataMapper.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Borc.DataMapper.Infrastructure.Persistence;

/// <summary>
/// پر کردن CreatedBy / UpdatedBy / DeletedBy از کاربر واردشده هنگام ذخیره.
/// Fills CreatedBy / UpdatedBy / DeletedBy from the signed-in user on save (values set explicitly are kept).
/// </summary>
public sealed class AuditStampInterceptor(ICurrentUser currentUser, IOrgUnitSelection orgUnits) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        StampOrgUnitsAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        await StampOrgUnitsAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// ردیف‌های جدید بدون واحد: واحد انتخاب‌شده در فرم، وگرنه واحد پیش‌فرض کاربر. رکوردهای ایمپورت واحد ایمپورت را دارند.
    /// New rows without a unit get the unit chosen in the form, else the user's default unit. Records created by an
    /// import keep the import's unit (also when that is "public").
    /// </summary>
    private async Task StampOrgUnitsAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<IOrgUnitOwned>().Where(e => e.State == EntityState.Added).ToList())
        {
            if (entry.Entity.OrgUnitKey is not null || entry.Entity is Domain.Records.DataRecord { ImportBatchId: not null })
            {
                continue;
            }

            entry.Entity.AssignOrgUnit(orgUnits.IsExplicit ? orgUnits.Key : await orgUnits.GetDefaultAsync(cancellationToken));
        }
    }

    private void Stamp(DbContext? context)
    {
        if (context is null || currentUser.UserId is not { } userId)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<EntityBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedBy).CurrentValue ??= userId;
                    break;

                case EntityState.Modified:
                    var deleted = entry.Property(e => e.IsDeleted);
                    if (deleted.IsModified && deleted.CurrentValue && !deleted.OriginalValue)
                    {
                        entry.Property(e => e.DeletedBy).CurrentValue ??= userId;
                    }
                    else
                    {
                        entry.Property(e => e.UpdatedBy).CurrentValue = userId;
                    }

                    break;
            }
        }
    }
}

internal sealed class NoCurrentUser : ICurrentUser
{
    public long? UserId => null;
}

/// <summary>No selection outside the web host: new rows stay public unless their code sets a unit.</summary>
internal sealed class NoOrgUnitSelection : IOrgUnitSelection
{
    public bool IsExplicit { get; private set; }

    public string? Key { get; private set; }

    public void Choose(string? orgUnitKey) => (IsExplicit, Key) = (true, string.IsNullOrWhiteSpace(orgUnitKey) ? null : orgUnitKey);

    public Task<string?> GetDefaultAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}
