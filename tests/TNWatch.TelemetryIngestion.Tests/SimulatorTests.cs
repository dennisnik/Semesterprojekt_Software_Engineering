using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Telemetry;
using TNWatch.TelemetryIngestion.Parsing;
using TNWatch.TelemetryIngestion.Simulation;

namespace TNWatch.TelemetryIngestion.Tests;

public class SimulatorTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));
    private readonly IngestionChannel _channel = new(Options.Create(new IngestionOptions()));
    private readonly FleetNotifier _notifier = new();

    private TelemetrySimulator Create(int nodeCount = 7, bool start = false) =>
        new(Options.Create(new SimulatorOptions { NodeCount = nodeCount, StartEnabled = start }),
            Options.Create(new IngestionOptions()), _channel, _notifier, _time, NullLogger<TelemetrySimulator>.Instance);

    private List<RawMessage> Drain()
    {
        var list = new List<RawMessage>();
        while (_channel.Reader.TryRead(out var m))
        {
            list.Add(m);
        }

        return list;
    }

    [Fact]
    public async Task Tick_produces_parsable_messages_for_every_demo_node()
    {
        var sim = Create();
        await sim.TickAsync(CancellationToken.None);

        var messages = Drain();
        Assert.Equal(7, messages.Count); // silent node sends its single (old) message once
        var parser = new TelemetryParser(new IngestionOptions());
        foreach (var m in messages)
        {
            Assert.Equal(TelemetrySource.Simulator, m.Source);
            var r = parser.Parse(m);
            Assert.True(r.IsSuccess, r.Error);
            Assert.Equal("simulation", r.Value!.Tenant);
            Assert.StartsWith("SIM-", r.Value.NodeId, StringComparison.Ordinal);
            Assert.NotNull(r.Value.Latitude);
        }

        var silent = parser.Parse(messages.Single(m => m.Topic.Contains("SIM-007", StringComparison.Ordinal))).Value!;
        Assert.Equal(_time.GetUtcNow().AddMinutes(-40), silent.MeasuredAt);

        await sim.TickAsync(CancellationToken.None);
        Assert.Equal(6, Drain().Count); // silent node stays silent
    }

    [Fact]
    public async Task Toggle_raises_events_and_fleet_reset()
    {
        var sim = Create();
        var states = new List<bool>();
        var resets = 0;
        sim.RunningChanged += s => { states.Add(s); return Task.CompletedTask; };
        _notifier.FleetReset += () => { resets++; return Task.CompletedTask; };

        await sim.SetRunningAsync(true);
        await sim.SetRunningAsync(true); // no-op
        await sim.SetRunningAsync(false);

        Assert.Equal([true, false], states);
        Assert.Equal(2, resets);
        Assert.False(sim.IsRunning);
        Assert.Equal(7, Drain().Count); // switching on emits an immediate tick
    }

    [Fact]
    public void Extra_nodes_are_generated_for_load_tests()
    {
        var sim = Create(nodeCount: 100);
        Assert.Equal(100, sim.Nodes.Count);
        Assert.Equal(100, sim.Nodes.Select(n => n.Id).Distinct().Count());
        Assert.All(sim.Nodes, n => Assert.InRange(n.Latitude, 48.0, 49.5));
    }

    [Fact]
    public void Discharging_node_loses_voltage_but_not_below_floor()
    {
        var node = new SimulatedNode("X", 48, 9, 10.81, SimulationProfile.Discharging);
        var random = new Random(1);
        for (var i = 0; i < 10; i++)
        {
            node.NextSample(random);
        }

        Assert.Equal(10.8, node.BaseVoltage, 3);
    }

    [Fact]
    public async Task Background_loop_ticks_only_while_running()
    {
        var sim = Create(start: true);
        await sim.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await sim.StopAsync(CancellationToken.None);

        Assert.NotEmpty(Drain());
    }
}
