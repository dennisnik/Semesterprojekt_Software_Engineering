using Microsoft.Extensions.Logging.Abstractions;
using TNWatch.Domain.Battery;
using TNWatch.Domain.Fleet;

namespace TNWatch.Alerting.Tests;

public class BatteryEventProcessorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 5, 10, 30, 0, TimeSpan.Zero);
    private readonly FakeTelemetryRepository _repo = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly BatteryEventProcessor _sut;

    public BatteryEventProcessorTests() => _sut = new BatteryEventProcessor(_repo, _notifier, NullLogger<BatteryEventProcessor>.Instance);

    private static BatteryEvent Exchange(Guid id, string node = "TN0001") =>
        new(id, BatteryEventType.Exchange, T0, node, "BAT0001", "BAT0002", 52.52, 13.405, 8);

    [Fact]
    public async Task Exchange_updates_installed_battery_of_known_node()
    {
        _repo.Nodes.Add(new TrafficNode("TN0001", null, false, T0));

        await _sut.HandleAsync(Exchange(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("BAT0001", _repo.Nodes[0].BatterySerial);
        Assert.Single(_repo.BatteryEvents);
        Assert.Equal("BAT0001", Assert.Single(_notifier.Updates).BatterySerial);
    }

    [Fact]
    public async Task Event_for_unknown_node_is_stored_without_notification()
    {
        await _sut.HandleAsync(Exchange(Guid.NewGuid(), "UNKNOWN"), CancellationToken.None);

        Assert.Single(_repo.BatteryEvents);
        Assert.Empty(_notifier.Updates);
    }

    [Fact]
    public async Task Duplicate_event_is_ignored()
    {
        var id = Guid.NewGuid();
        await _sut.HandleAsync(Exchange(id), CancellationToken.None);
        await _sut.HandleAsync(Exchange(id), CancellationToken.None);

        Assert.Single(_repo.BatteryEvents);
        Assert.Equal(1, _repo.SaveCount);
    }

    [Fact]
    public async Task Discard_is_stored()
    {
        await _sut.HandleAsync(new BatteryEvent(Guid.NewGuid(), BatteryEventType.Discard, T0, null, null, "BAT0002", null, null, null), CancellationToken.None);
        Assert.Equal(BatteryEventType.Discard, Assert.Single(_repo.BatteryEvents).Type);
    }
}
