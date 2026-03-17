using AiNewsCurator.Api.Middleware;
using AiNewsCurator.Application;
using AiNewsCurator.Application.Configuration;
using AiNewsCurator.Application.HostedServices;
using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Domain.Interfaces;
using AiNewsCurator.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddMappedEnvironmentVariables();

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

if (builder.Configuration.GetValue<bool>("EnableScheduler"))
{
    builder.Services.AddHostedService<DailySchedulerService>();
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
    var sourceRepository = scope.ServiceProvider.GetRequiredService<ISourceRepository>();
    await initializer.InitializeAsync(CancellationToken.None);
    await sourceRepository.SeedDefaultsAsync(CancellationToken.None);
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseMiddleware<InternalApiKeyMiddleware>();
app.MapControllers();

app.Run();
