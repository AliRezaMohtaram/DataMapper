using Borc.DataMapper.Application.Abstractions.Persistence;
using Borc.DataMapper.Infrastructure.Persistence;
using Borc.DataMapper.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Borc.DataMapper.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<BorcDataMapperDbContext>(options =>
        {
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