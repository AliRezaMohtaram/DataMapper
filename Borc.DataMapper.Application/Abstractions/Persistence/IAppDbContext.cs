using Borc.DataMapper.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace Borc.DataMapper.Application.Abstractions.Persistence;

/// <summary>
/// دسترسی Handlerها به دیتابیس. با نوشته شدن هر Entity، DbSet آن اینجا اضافه می‌شود.
/// پیاده‌سازی: BorcDataMapperDbContext.
/// </summary>
public interface IAppDbContext
{
    DbSet<Template> Templates { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}