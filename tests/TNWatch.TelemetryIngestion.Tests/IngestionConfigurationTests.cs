using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TNWatch.TelemetryIngestion.Mqtt;

namespace TNWatch.TelemetryIngestion.Tests;

public class IngestionConfigurationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> values) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddTnWatchIngestion(new ConfigurationBuilder().AddInMemoryCollection(values).Build())
            .BuildServiceProvider();

    [Fact]
    public void Configured_topic_patterns_replace_the_defaults()
    {
        var options = Build(new()
        {
            ["Ingestion:TelemetryTopicPatterns:0"] = "group/{nodeId}/data",
            ["Ingestion:BatteryEventTopicPatterns:0"] = "group/{nodeId}/battery",
            ["Mqtt:ExtraSubscriptions:0"] = "group/#",
        });

        Assert.Equal(["group/{nodeId}/data"], options.GetRequiredService<IOptions<IngestionOptions>>().Value.TelemetryTopicPatterns);
        Assert.Equal(["group/{nodeId}/battery"], options.GetRequiredService<IOptions<IngestionOptions>>().Value.BatteryEventTopicPatterns);
        Assert.Equal(["group/#"], options.GetRequiredService<IOptions<MqttOptions>>().Value.ExtraSubscriptions);
    }

    [Fact]
    public void Without_configuration_the_defaults_apply()
    {
        var options = Build([]).GetRequiredService<IOptions<IngestionOptions>>().Value;
        Assert.Equal(new IngestionOptions().TelemetryTopicPatterns, options.TelemetryTopicPatterns);
        Assert.Equal(new MqttOptions().ExtraSubscriptions, Build([]).GetRequiredService<IOptions<MqttOptions>>().Value.ExtraSubscriptions);
    }
}
