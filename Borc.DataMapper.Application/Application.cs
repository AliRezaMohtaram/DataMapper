using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Borc.DataMapper.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(
                typeof(DependencyInjection).Assembly);
        });

        services.AddValidatorsFromAssembly(
            typeof(DependencyInjection).Assembly);

        services.AddMemoryCache();
        services.AddScoped<DataSources.Common.DataSourceOptionService>();

        return services;
    }
}