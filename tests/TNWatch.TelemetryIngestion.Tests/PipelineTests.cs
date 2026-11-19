using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TNWatch.Domain.Battery;
using TNWatch.Domain.Diagnostics;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Telemetry;
using TNWatch.TelemetryIngestion.Parsing;

namespace TNWatch.TelemetryIngestion.Tests;

public class PipelineTests
{
    private sealed class CapturingHandlers : ITelemetryHandler, IBatteryLifecycleHandler
    {
        public List<TelemetryReading> Telemetry { get; } = [];

        public List<BatteryEvent> Battery { get; } = [];

        public bool Throw { get; set; }

        public Task HandleAsync(TelemetryReading reading, CancellationToken ct)
        {
            if (Throw)
            {
                throw new InvalidOperationException("db down");
            }

            Telemetry.Add(reading);
            return Task.CompletedTask;
        }

        public Task HandleAsync(BatteryEvent batteryEvent, CancellationToken ct)
        {
            Battery.Add(batteryEvent);
            return Task.CompletedTask;
        }
    }

    private readonly CapturingHandlers _handlers = new();
    private readonly IngestionStats _stats = new();
    private readonly IngestionChannel _channel;
    private readonly IngestionPipelineService _sut;

    public PipelineTests()
    {
        var options = new IngestionOptions();
        options.BatteryEventTopicPatterns.Add("traffic-device/{tenant}/battery/{*}");
        _channel = new IngestionChannel(Options.Create(options));
        var services = new ServiceCollection()
            .AddSingleton<ITelemetryHandler>(_handlers)
            .AddSingleton<IBatteryLifecycleHandler>(_handlers)
            .BuildServiceProvider();
        _sut = new IngestionPipelineService(_channel, new TelemetryParser(options), new BatteryEventParser(options),
            services.GetRequiredService<IServiceScopeFactory>(), _stats, NullLogger<IngestionPipelineService>.Instance);
    }

    [Fact]
    public async Task Routes_telemetry_and_battery_events()
    {
        await _sut.ProcessAsync(TestData.Msg(TestData.SampleMsg), CancellationToken.None);
        await _sut.ProcessAsync(TestData.Msg(TestData.Exchange, "traffic-device/acme/battery/exchange"), CancellationToken.None);
        await _sut.ProcessAsync(TestData.Msg(TestData.Discard, "some/unknown/topic"), CancellationToken.None); // detected by payload

        Assert.Single(_handlers.Telemetry);
        Assert.Equal(2, _handlers.Battery.Count);
    }

    [Fact]
    public async Task Invalid_messages_are_counted_and_unknown_topics_ignored()
    {
        await _sut.ProcessAsync(TestData.Msg("""{ "voltage_mV": 0, "current_mA": 0 }"""), CancellationToken.None);
        await _sut.ProcessAsync(TestData.Msg("""{ "eventId": "x" }""", "traffic-device/acme/battery/exchange"), CancellationToken.None);
        await _sut.ProcessAsync(TestData.Msg("""{ "foo": 1 }""", "other/topic"), CancellationToken.None);

        Assert.Empty(_handlers.Telemetry);
        Assert.Empty(_handlers.Battery);
        Assert.Equal(2, _stats.Snapshot().Rejected);
    }

    [Fact]
    public async Task Simulator_messages_are_telemetry_on_any_topic()
    {
        var msg = new RawMessage("sim/x", Encoding.UTF8.GetBytes("""{ "node_id": "SIM-9", "voltage_V": 12.5, "current_A": 0.4 }"""), TestData.ReceivedAt, TelemetrySource.Simulator);
        await _sut.ProcessAsync(msg, CancellationToken.None);
        Assert.Equal("SIM-9", Assert.Single(_handlers.Telemetry).NodeId);
    }

    [Fact]
    public async Task Background_loop_survives_handler_failures()
    {
        _handlers.Throw = true;
        await _sut.StartAsync(CancellationToken.None);
        await _channel.Writer.WriteAsync(TestData.Msg(TestData.SampleMsg));
        await Task.Delay(50);
        _handlers.Throw = false;
        await _channel.Writer.WriteAsync(TestData.Msg(TestData.SampleMsg));
        await Task.Delay(50);
        await _sut.StopAsync(CancellationToken.None);

        Assert.Single(_handlers.Telemetry);
    }
}
