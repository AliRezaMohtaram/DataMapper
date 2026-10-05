using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Abstractions.Persistence;

public static class AppDbContextExtensions
{
    /// <summary>
    /// اجرای یک کار چندمرحله‌ای داخل تراکنش، سازگار با SqlServerRetryingExecutionStrategy.
    /// کل delegate در صورت خطای گذرا از اول اجرا می‌شود؛ پس هر بار ChangeTracker پاک می‌شود
    /// و Entityهای لازم باید داخل delegate دوباره خوانده شوند.
    /// </summary>
    public static async Task<T> InTransactionAsync<T>(
        this IAppDbContext db,
        Func<Task<T>> work,
        CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();

            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await work();
            await tx.CommitAsync(cancellationToken);

            return result;
        });
    }
}