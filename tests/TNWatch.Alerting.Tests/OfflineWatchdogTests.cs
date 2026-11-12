using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Status;
using TNWatch.Domain.Telemetry;

namespace TNWatch.Alerting.Tests;

public class OfflineWatchdogTests
{
    private readonly FakeTelemetryRepository _repo = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));
    private readonly StatusEvaluator _eval = new(new StatusRules());
    private readonly OfflineWatchdog _sut;

    public OfflineWatchdogTests()
    {
        var services = new ServiceCollection().AddSingleton<ITelemetryRepository>(_repo).BuildServiceProvider();
        _sut = new OfflineWatchdog(
            services.GetRequiredService<IServiceScopeFactory>(), _eval, _notifier, _time,
            Options.Create(new AlertingOptions { OfflineCheckInterval = TimeSpan.FromSeconds(1) }), NullLogger<OfflineWatchdog>.Instance);
    }

    private void AddNodeSeenAt(string id, DateTimeOffset at)
    {
        var node = new TrafficNode(id, null, false, at);
        node.ApplyReading(new TelemetryReading(id, null, at, at, 12.6, 0.5, null, null, null, null, null, TelemetrySource.Mqtt), [12.6], _eval, at);
        _repo.Nodes.Add(node);
    }

    [Fact]
    public async Task Marks_only_stale_nodes_offline_and_notifies()
    {
        var now = _time.GetUtcNow();
        AddNodeSeenAt("OLD", now.AddMinutes(-31));
        AddNodeSeenAt("FRESH", now.AddMinutes(-5));

        var count = await _sut.CheckOnceAsync(CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Equal(NodeStatus.Offline, _repo.Nodes.Single(n => n.Id == "OLD").Status);
        Assert.Equal(NodeStatus.Normal, _repo.Nodes.Single(n => n.Id == "FRESH").Status);
        Assert.Equal("OLD", Assert.Single(_repo.StatusChanges).NodeId);
        Assert.Single(_notifier.Updates);
        Assert.Single(_notifier.Changes);
    }

    [Fact]
    public async Task Nothing_to_do_does_not_save()
    {
        AddNodeSeenAt("FRESH", _time.GetUtcNow());
        Assert.Equal(0, await _sut.CheckOnceAsync(CancellationToken.None));
        Assert.Equal(0, _repo.SaveCount);
    }

    [Fact]
    public async Task Background_loop_runs_checks_until_stopped()
    {
        AddNodeSeenAt("OLD", _time.GetUtcNow().AddHours(-1));

        await _sut.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await _sut.StopAsync(CancellationToken.None);

        Assert.Equal(NodeStatus.Offline, _repo.Nodes[0].Status);
    }
}
