using TNWatch.Domain.Status;
using TNWatch.TelemetryIngestion.Simulation;

namespace TNWatch.TelemetryIngestion.Tests;

public class ReplayTests
{
    [Fact]
    public void Embedded_tsn002_profile_contains_the_real_series()
    {
        var samples = ReplayProfile.Tsn002;

        Assert.Equal(2229, samples.Count);
        Assert.Equal(new ReplaySample(11.276, 0.551), samples[0]);  // first row of data/traffic_devices_signals.csv
        Assert.Equal(new ReplaySample(10.700, 0.702), samples[^1]); // last row
        Assert.All(samples, s => Assert.InRange(s.VoltageV, 10.3, 11.4));
        Assert.All(samples, s => Assert.InRange(s.CurrentA, 0.3, 1.1));
    }

    [Fact]
    public void Replay_node_plays_samples_in_order_and_loops()
    {
        var node = new SimulatedNode("SIM-X", 48, 9, 0, SimulationProfile.Replay, [new(11.1, 0.5), new(11.0, 0.6)]);
        var random = new Random(1);

        Assert.Equal((11.1, 0.5), node.NextSample(random));
        Assert.Equal((11.0, 0.6), node.NextSample(random));
        Assert.Equal((11.1, 0.5), node.NextSample(random)); // loops
        Assert.Equal(11.1, node.BaseVoltage);
        Assert.Null(node.ExchangeBatteryIfEmpty()); // replay nodes never get a simulated exchange
    }

    [Fact]
    public void Replay_node_without_samples_is_rejected()
        => Assert.Throws<ArgumentException>(() => new SimulatedNode("SIM-X", 48, 9, 0, SimulationProfile.Replay));

    [Fact]
    public void Demo_node_sim001_replays_tsn002_and_is_critical()
    {
        var node = SimulatedNode.DemoFleet().Single(n => n.Id == "SIM-001");
        Assert.Equal(SimulationProfile.Replay, node.Profile);

        var evaluator = new StatusEvaluator(new StatusRules());
        var (v, _) = node.NextSample(new Random(1));
        Assert.Equal(11.276, v);
        Assert.Equal(NodeStatus.Critical, evaluator.Classify(v)); // the real node ran below 11.8 V the whole time
    }

    [Theory]
    [InlineData("")]
    [InlineData("# only a comment\nvoltage_v,current_a\n")]
    public void Empty_profiles_are_rejected(string content)
        => Assert.Throws<InvalidOperationException>(() => ReplayProfile.Parse(new StringReader(content)));

    [Fact]
    public void Unknown_profile_resource_is_reported()
        => Assert.Throws<InvalidOperationException>(() => ReplayProfile.Load("does-not-exist.csv"));
}
