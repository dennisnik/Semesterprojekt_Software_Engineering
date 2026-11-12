using TNWatch.Domain.Battery;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Status;
using TNWatch.Domain.Telemetry;

namespace TNWatch.Alerting.Tests;

/// <summary>In-memory repository: pending changes become visible only after SaveChangesAsync (like a transaction).</summary>
internal sealed class FakeTelemetryRepository : ITelemetryRepository
{
    private readonly List<TrafficNode> _pendingNodes = [];
    private readonly List<TelemetryReading> _pendingMeasurements = [];
    private readonly List<StatusChange> _pendingChanges = [];
    private readonly List<BatteryEvent> _pendingEvents = [];

    public List<TrafficNode> Nodes { get; } = [];

    public List<TelemetryReading> Measurements { get; } = [];

    public List<StatusChange> StatusChanges { get; } = [];

    public List<BatteryEvent> BatteryEvents { get; } = [];

    public int SaveCount { get; private set; }

    public Task<TrafficNode?> FindNodeAsync(string nodeId, CancellationToken ct) =>
        Task.FromResult(Nodes.Concat(_pendingNodes).FirstOrDefault(n => n.Id == nodeId));

    public void AddNode(TrafficNode node) => _pendingNodes.Add(node);

    public Task<IReadOnlyList<double>> GetRecentVoltagesAsync(string nodeId, int count, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<double>>(Measurements.Where(m => m.NodeId == nodeId)
            .OrderByDescending(m => m.MeasuredAt).Take(count).Select(m => m.VoltageV).ToList());

    public void AddMeasurement(TelemetryReading reading) => _pendingMeasurements.Add(reading);

    public void AddStatusChange(StatusChange change) => _pendingChanges.Add(change);

    public Task<bool> BatteryEventExistsAsync(Guid eventId, CancellationToken ct) =>
        Task.FromResult(BatteryEvents.Any(e => e.EventId == eventId));

    public void AddBatteryEvent(BatteryEvent batteryEvent) => _pendingEvents.Add(batteryEvent);

    public Task<IReadOnlyList<TrafficNode>> GetStaleNodesAsync(DateTimeOffset threshold, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TrafficNode>>(Nodes.Where(n => n.Status != NodeStatus.Offline && n.LastSeenAt <= threshold).ToList());

    public Task SaveChangesAsync(CancellationToken ct)
    {
        Nodes.AddRange(_pendingNodes);
        Measurements.AddRange(_pendingMeasurements);
        StatusChanges.AddRange(_pendingChanges);
        BatteryEvents.AddRange(_pendingEvents);
        _pendingNodes.Clear();
        _pendingMeasurements.Clear();
        _pendingChanges.Clear();
        _pendingEvents.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Records everything published to the notifier.</summary>
internal sealed class RecordingNotifier : IFleetNotifier
{
    public event Func<NodeSnapshot, Task>? NodeUpdated;

    public event Func<StatusChange, Task>? StatusChanged;

    public event Func<Task>? FleetReset;

    public List<NodeSnapshot> Updates { get; } = [];

    public List<StatusChange> Changes { get; } = [];

    public int Resets { get; private set; }

    public Task PublishNodeUpdatedAsync(NodeSnapshot snapshot)
    {
        Updates.Add(snapshot);
        return NodeUpdated?.Invoke(snapshot) ?? Task.CompletedTask;
    }

    public Task PublishStatusChangedAsync(StatusChange change)
    {
        Changes.Add(change);
        return StatusChanged?.Invoke(change) ?? Task.CompletedTask;
    }

    public Task PublishFleetResetAsync()
    {
        Resets++;
        return FleetReset?.Invoke() ?? Task.CompletedTask;
    }
}
