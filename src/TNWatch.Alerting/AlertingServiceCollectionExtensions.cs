using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TNWatch.Domain.Diagnostics;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Status;

namespace TNWatch.Alerting;

public static class AlertingServiceCollectionExtensions
{
    /// <summary>Registers status evaluation, telemetry/battery handlers, the offline watchdog and the live notifier.</summary>
    public static IServiceCollection AddTnWatchAlerting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StatusRules>(configuration.GetSection(StatusRules.SectionName));
        services.Configure<AlertingOptions>(configuration.GetSection(AlertingOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sp => new StatusEvaluator(sp.GetRequiredService<IOptions<StatusRules>>().Value));
        services.AddSingleton<IFleetNotifier, FleetNotifier>();
        services.AddSingleton<IngestionStats>();
        services.AddScoped<ITelemetryHandler, TelemetryProcessor>();
        services.AddScoped<IBatteryLifecycleHandler, BatteryEventProcessor>();
        services.AddSingleton<OfflineWatchdog>();
        services.AddHostedService(sp => sp.GetRequiredService<OfflineWatchdog>());
        return services;
    }
}
