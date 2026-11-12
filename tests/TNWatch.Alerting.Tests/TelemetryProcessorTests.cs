using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TNWatch.Domain.Diagnostics;
using TNWatch.Domain.Status;
using TNWatch.Domain.Telemetry;

namespace TNWatch.Alerting.Tests;

public class TelemetryProcessorTests
{
    private readonly FakeTelemetryRepository _repo = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly IngestionStats _stats = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));
    private readonly TelemetryProcessor _sut;

    public TelemetryProcessorTests()
    {
        _sut = new TelemetryProcessor(_repo, new StatusEvaluator(new StatusRules()), _notifier, _stats, _time, NullLogger<TelemetryProcessor>.Instance);
    }

    private TelemetryReading Reading(double v, TelemetrySource source = TelemetrySource.Mqtt)
    {
        var now = _time.GetUtcNow();
        return new TelemetryReading("TSN-002", "dieringer-stark", now, now, v, 0.55, 48.2, 8.65, null, null, null, source);
    }

    [Fact]
    public async Task First_message_creates_node_persists_and_notifies()
    {
        await _sut.HandleAsync(Reading(11.3), CancellationToken.None);

        var node = Assert.Single(_repo.Nodes);
        Assert.False(node.IsSimulated);
        Assert.Equal(NodeStatus.Critical, node.Status);
        Assert.Single(_repo.Measurements);
        var change = Assert.Single(_repo.StatusChanges);
        Assert.Equal(NodeStatus.Unknown, change.From);
        Assert.Equal(1, _repo.SaveCount);
        Assert.Equal("TSN-002", Assert.Single(_notifier.Updates).Id);
        Assert.Single(_notifier.Changes);
        Assert.Equal(1, _stats.Snapshot().Processed);
    }

    [Fact]
    public async Task Smoothing_uses_stored_history_and_suppresses_single_dip()
    {
        await _sut.HandleAsync(Reading(12.6), CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(1));
        await _sut.HandleAsync(Reading(12.6), CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(1));

        await _sut.HandleAsync(Reading(11.9), CancellationToken.None); // Ø 12.37 → Warning, not Critical

        Assert.Equal(NodeStatus.Warning, _repo.Nodes[0].Status);
        Assert.Equal([NodeStatus.Normal, NodeStatus.Warning], _repo.StatusChanges.Select(c => c.To));
        Assert.Equal(3, _repo.Measurements.Count);
        Assert.Equal(3, _notifier.Updates.Count);
        Assert.Equal(2, _notifier.Changes.Count);
    }

    [Fact]
    public async Task Simulator_readings_create_simulated_nodes()
    {
        await _sut.HandleAsync(Reading(12.8, TelemetrySource.Simulator), CancellationToken.None);
        Assert.True(_repo.Nodes[0].IsSimulated);
    }
}
