using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Application.Services;
using AiNewsCurator.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiNewsCurator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppOptions>(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<INewsPipelineService, NewsPipelineService>();
        services.AddScoped<IOpsAuthService, OpsAuthService>();
        return services;
    }
}
