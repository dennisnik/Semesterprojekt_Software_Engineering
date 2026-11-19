using TNWatch.Domain.Battery;
using TNWatch.Domain.Telemetry;

namespace TNWatch.Domain.Fleet;

/// <summary>
/// Unit of work for the ingestion side. All changes are committed atomically by <see cref="SaveChangesAsync"/>
/// (NFA04: no partial states).
/// </summary>
public interface ITelemetryRepository
{
    Task<TrafficNode?> FindNodeAsync(string nodeId, CancellationToken ct);

    void AddNode(TrafficNode node);

    /// <summary>Voltages of the newest stored measurements of a node, newest first.</summary>
    Task<IReadOnlyList<double>> GetRecentVoltagesAsync(string nodeId, int count, CancellationToken ct);

    void AddMeasurement(TelemetryReading reading);

    void AddStatusChange(StatusChange change);

    Task<bool> BatteryEventExistsAsync(Guid eventId, CancellationToken ct);

    void AddBatteryEvent(BatteryEvent batteryEvent);

    /// <summary>Nodes that are not yet Offline but were last seen before <paramref name="threshold"/>.</summary>
    Task<IReadOnlyList<TrafficNode>> GetStaleNodesAsync(DateTimeOffset threshold, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
