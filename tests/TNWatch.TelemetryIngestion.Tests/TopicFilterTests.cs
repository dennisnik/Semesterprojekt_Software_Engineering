using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TNWatch.TelemetryIngestion.Mqtt;
using TNWatch.TelemetryIngestion.Parsing;

namespace TNWatch.TelemetryIngestion.Tests;

public class TopicFilterTests
{
    [Fact]
    public void Worker_subscribes_to_filters_derived_from_patterns_plus_extras()
    {
        var ingestion = new IngestionOptions();
        ingestion.BatteryEventTopicPatterns.Add("traffic-device/{tenant}/battery/{*}");
        var mqtt = new MqttOptions();
        mqtt.ExtraSubscriptions.Add("extra/#");
        mqtt.ExtraSubscriptions.Add("traffic-device/+/trafficnode/+/status"); // duplicate

        var worker = new MqttIngestionWorker(Options.Create(mqtt), new TelemetryParser(ingestion), new BatteryEventParser(ingestion),
            new IngestionChannel(Options.Create(ingestion)), new FakeTimeProvider(), NullLogger<MqttIngestionWorker>.Instance);

        Assert.Equal(
            ["traffic-device/+/trafficnode/+/status", "traffic-device/+/battery/+", "extra/#"],
            worker.SubscriptionFilters);
        Assert.False(worker.IsConnected);
    }

    [Fact]
    public async Task Worker_without_credentials_or_disabled_exits_cleanly()
    {
        var ingestion = new IngestionOptions();
        foreach (var options in new[] { new MqttOptions { Enabled = false }, new MqttOptions() })
        {
            var worker = new MqttIngestionWorker(Options.Create(options), new TelemetryParser(ingestion), new BatteryEventParser(ingestion),
                new IngestionChannel(Options.Create(ingestion)), new FakeTimeProvider(), NullLogger<MqttIngestionWorker>.Instance);
            await worker.StartAsync(CancellationToken.None);
            await worker.ExecuteTask!;
            Assert.False(worker.IsConnected);
        }
    }
}
