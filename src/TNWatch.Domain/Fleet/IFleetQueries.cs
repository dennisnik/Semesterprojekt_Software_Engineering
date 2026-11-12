using TNWatch.Domain.Battery;
using TNWatch.Domain.Status;

namespace TNWatch.Domain.Fleet;

public sealed record MeasurementPoint(DateTimeOffset MeasuredAt, double VoltageV, double CurrentA, double? Latitude, double? Longitude, double? TemperatureC);

public sealed record FleetSummary(int Total, int Normal, int Warning, int Critical, int Offline, int Unknown);

/// <summary>Read side for UI and REST API (F03–F06, F08, F10).</summary>
public interface IFleetQueries
{
    Task<IReadOnlyList<NodeSnapshot>> GetNodesAsync(bool includeSimulated, CancellationToken ct);

    Task<NodeSnapshot?> GetNodeAsync(string nodeId, CancellationToken ct);

    Task<IReadOnlyList<MeasurementPoint>> GetMeasurementsAsync(string nodeId, DateTimeOffset since, DateTimeOffset until, CancellationToken ct);

    Task<IReadOnlyList<StatusChange>> GetStatusChangesAsync(string nodeId, int limit, CancellationToken ct);

    Task<IReadOnlyList<BatteryEvent>> GetBatteryEventsAsync(string nodeId, int limit, CancellationToken ct);

    Task<FleetSummary> GetSummaryAsync(bool includeSimulated, CancellationToken ct);
}

public static class FleetSummaryExtensions
{
    public static FleetSummary Summarize(this IEnumerable<NodeSnapshot> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var list = nodes as IReadOnlyCollection<NodeSnapshot> ?? nodes.ToList();
        return new FleetSummary(
            list.Count,
            list.Count(n => n.Status == NodeStatus.Normal),
            list.Count(n => n.Status == NodeStatus.Warning),
            list.Count(n => n.Status == NodeStatus.Critical),
            list.Count(n => n.Status == NodeStatus.Offline),
            list.Count(n => n.Status == NodeStatus.Unknown));
    }
}
