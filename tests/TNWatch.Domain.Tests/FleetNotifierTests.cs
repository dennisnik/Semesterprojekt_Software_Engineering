using TNWatch.Domain.Fleet;
using TNWatch.Domain.Status;

namespace TNWatch.Domain.Tests;

public class FleetNotifierTests
{
    private static NodeSnapshot Snapshot(string id) =>
        new(id, null, false, NodeStatus.Normal, 12.6, 12.6, 0.5, 90, 48.7, 9.1, DateTimeOffset.UtcNow, null);

    [Fact]
    public async Task Publishing_without_subscribers_does_nothing()
    {
        var sut = new FleetNotifier();
        await sut.PublishNodeUpdatedAsync(Snapshot("TN-001"));
        await sut.PublishFleetResetAsync();
    }

    [Fact]
    public async Task Failing_subscriber_does_not_block_others()
    {
        var sut = new FleetNotifier();
        var received = new List<string>();
        sut.NodeUpdated += _ => throw new InvalidOperationException("circuit gone");
        sut.NodeUpdated += s => { received.Add(s.Id); return Task.CompletedTask; };

        await sut.PublishNodeUpdatedAsync(Snapshot("TN-001"));

        Assert.Equal(["TN-001"], received);
    }

    [Fact]
    public async Task Status_changes_and_resets_reach_subscribers()
    {
        var sut = new FleetNotifier();
        StatusChange? change = null;
        var resets = 0;
        sut.StatusChanged += c => { change = c; return Task.CompletedTask; };
        sut.FleetReset += () => { resets++; return Task.CompletedTask; };

        await sut.PublishStatusChangedAsync(new StatusChange("TN-001", NodeStatus.Warning, NodeStatus.Critical, 11.7, DateTimeOffset.UtcNow));
        await sut.PublishFleetResetAsync();

        Assert.Equal(NodeStatus.Critical, change?.To);
        Assert.Equal(1, resets);
    }
}
