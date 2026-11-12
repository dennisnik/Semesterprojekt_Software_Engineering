using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TNWatch.Domain.Fleet;
using TNWatch.TelemetryIngestion.Mqtt;
using TNWatch.TelemetryIngestion.Parsing;
using TNWatch.TelemetryIngestion.Simulation;

namespace TNWatch.TelemetryIngestion;

public static class IngestionServiceCollectionExtensions
{
    /// <summary>Registers parsers, the ingestion channel + pipeline, the MQTT worker and the simulator.</summary>
    public static IServiceCollection AddTnWatchIngestion(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));
        services.Configure<MqttOptions>(configuration.GetSection(MqttOptions.SectionName));
        services.Configure<SimulatorOptions>(configuration.GetSection(SimulatorOptions.SectionName));

        services.AddSingleton(sp => new TelemetryParser(sp.GetRequiredService<IOptions<IngestionOptions>>().Value));
        services.AddSingleton(sp => new BatteryEventParser(sp.GetRequiredService<IOptions<IngestionOptions>>().Value));
        services.AddSingleton<IngestionChannel>();

        services.AddSingleton<IngestionPipelineService>();
        services.AddHostedService(sp => sp.GetRequiredService<IngestionPipelineService>());

        services.AddSingleton<MqttIngestionWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<MqttIngestionWorker>());

        services.AddSingleton<TelemetrySimulator>();
        services.AddSingleton<ISimulatorControl>(sp => sp.GetRequiredService<TelemetrySimulator>());
        services.AddHostedService(sp => sp.GetRequiredService<TelemetrySimulator>());
        return services;
    }
}
