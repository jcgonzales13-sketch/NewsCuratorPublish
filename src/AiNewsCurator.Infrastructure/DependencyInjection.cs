using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure.Integrations.AI;
using AiNewsCurator.Infrastructure.Integrations.LinkedIn;
using AiNewsCurator.Infrastructure.Integrations.NewsCollectors;
using AiNewsCurator.Infrastructure.Persistence;
using AiNewsCurator.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiNewsCurator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddScoped<IDatabaseInitializer, SqliteDatabaseInitializer>();

        services.AddScoped<ISourceRepository, SourceRepository>();
        services.AddScoped<INewsItemRepository, NewsItemRepository>();
        services.AddScoped<ICurationResultRepository, CurationResultRepository>();
        services.AddScoped<IPostDraftRepository, PostDraftRepository>();
        services.AddScoped<IPublicationRepository, PublicationRepository>();
        services.AddScoped<IExecutionRunRepository, ExecutionRunRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();

        services.AddScoped<HeuristicAiCurationService>();
        services.AddScoped<OpenAiResponsesAiCurationService>();
        services.AddScoped<IAiCurationService, ResilientAiCurationService>();
        services.AddScoped<ILinkedInAuthService, LinkedInAuthService>();
        services.AddScoped<ILinkedInPublisher, LinkedInPublisher>();
        services.AddScoped<INewsCollector, RssNewsCollector>();
        services.AddScoped<INewsImageEnrichmentService, NewsImageEnrichmentService>();

        services.AddHttpClient("rss", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("AiNewsCurator/1.0");
        });

        services.AddHttpClient("linkedin", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Add("LinkedIn-Version", "202401");
            client.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");
        });

        services.AddHttpClient("linkedin-auth", client =>
        {
            client.BaseAddress = new Uri("https://www.linkedin.com");
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddHttpClient("openai", client =>
        {
            client.BaseAddress = new Uri(configuration["AiBaseUrl"] ?? "https://api.openai.com");
            client.Timeout = TimeSpan.FromSeconds(45);
        });

        return services;
    }
}
