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

            // validatorهای FluentValidation پیش از هر Handler اجرا می‌شوند
            cfg.AddOpenBehavior(typeof(Common.Validation.ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(
            typeof(DependencyInjection).Assembly);

        Common.Validation.ValidationMessages.Configure();

        services.AddMemoryCache();
        services.AddScoped<DataSources.Common.DataSourceOptionService>();

        return services;
    }
}