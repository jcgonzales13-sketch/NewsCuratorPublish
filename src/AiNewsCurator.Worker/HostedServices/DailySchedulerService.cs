using AiNewsCurator.Application.DTOs;
using AiNewsCurator.Application.Interfaces;
using AiNewsCurator.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AiNewsCurator.Worker.HostedServices;

public sealed class DailySchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppOptions _options;
    private readonly ILogger<DailySchedulerService> _logger;

    public DailySchedulerService(IServiceProvider serviceProvider, IOptions<AppOptions> options, ILogger<DailySchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun();
            _logger.LogInformation("Next daily run scheduled in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<INewsPipelineService>();

            try
            {
                await pipeline.RunDailyAsync(new TriggerContext
                {
                    TriggerType = TriggerType.Scheduled,
                    InitiatedBy = "worker"
                }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled daily run failed.");
            }
        }
    }

    private TimeSpan GetDelayUntilNextRun()
    {
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(_options.Timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            _logger.LogWarning("Timezone {Timezone} was not found. Falling back to UTC.", _options.Timezone);
            timeZone = TimeZoneInfo.Utc;
        }

        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);
        var nextRun = new DateTimeOffset(
            nowLocal.Year,
            nowLocal.Month,
            nowLocal.Day,
            _options.RunHourLocal,
            _options.RunMinuteLocal,
            0,
            nowLocal.Offset);

        if (nextRun <= nowLocal)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - nowLocal;
    }
}
