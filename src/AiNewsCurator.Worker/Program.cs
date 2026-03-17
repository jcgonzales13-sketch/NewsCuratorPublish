using AiNewsCurator.Application;
using AiNewsCurator.Application.Configuration;
using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure;
using AiNewsCurator.Worker.HostedServices;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddMappedEnvironmentVariables();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<DailySchedulerService>();

var host = builder.Build();
using (var scope = host.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    var sourceRepository = scope.ServiceProvider.GetRequiredService<ISourceRepository>();
    await initializer.InitializeAsync(CancellationToken.None);
    await sourceRepository.SeedDefaultsAsync(CancellationToken.None);
}

await host.RunAsync();
