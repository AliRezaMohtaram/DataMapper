using Borc.DataMapper.Application.Abstractions.Identity;
using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Infrastructure.Persistence;
using Borc.DataMapper.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Borc.DataMapper.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // کاربر جاری: میزبان وب پیاده‌سازی واقعی را ثبت می‌کند؛ بیرون از درخواست، کاربری نیست.
        services.TryAddScoped<ICurrentUser, NoCurrentUser>();
        services.TryAddScoped<IOrgUnitSelection, NoOrgUnitSelection>();
        services.AddScoped<AuditStampInterceptor>();

        services.AddDbContext<BorcDataMapperDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetRequiredService<AuditStampInterceptor>());
            options.UseSqlServer(
                configuration.GetConnectionString(
                    "BorcDataMapper"),
                sql =>
                {
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
        });

        services.AddScoped<
            ITemplateRepository,
            TemplateRepository>();

        services.AddScoped<
            IUnitOfWork,
            UnitOfWork>();

        return services;
    }
}