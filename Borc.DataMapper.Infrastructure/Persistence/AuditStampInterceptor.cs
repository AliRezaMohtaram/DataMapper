using Borc.DataMapper.Application.Abstractions.Identity;
using Borc.DataMapper.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Borc.DataMapper.Infrastructure.Persistence;

/// <summary>
/// پر کردن CreatedBy / UpdatedBy / DeletedBy از کاربر واردشده هنگام ذخیره.
/// Fills CreatedBy / UpdatedBy / DeletedBy from the signed-in user on save (values set explicitly are kept).
/// </summary>
public sealed class AuditStampInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
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
