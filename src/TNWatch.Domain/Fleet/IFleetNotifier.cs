using TNWatch.Domain.Status;

namespace TNWatch.Domain.Fleet;

/// <summary>A node's status changed (F06).</summary>
public sealed record StatusChange(string NodeId, NodeStatus From, NodeStatus To, double? SmoothedVoltageV, DateTimeOffset OccurredAt);

/// <summary>
/// In-process publish/subscribe for live UI updates (ADR-07). Blazor components and the SignalR hub subscribe;
/// the ingestion pipeline and the offline watchdog publish.
/// </summary>
public interface IFleetNotifier
{
    event Func<NodeSnapshot, Task>? NodeUpdated;

    event Func<StatusChange, Task>? StatusChanged;

    event Func<Task>? FleetReset;

    Task PublishNodeUpdatedAsync(NodeSnapshot snapshot);

    Task PublishStatusChangedAsync(StatusChange change);

    /// <summary>The set of visible nodes changed (e.g. simulator switched off).</summary>
    Task PublishFleetResetAsync();
}
