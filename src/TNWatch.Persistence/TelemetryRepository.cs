using Microsoft.EntityFrameworkCore;
using TNWatch.Domain.Battery;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Status;
using TNWatch.Domain.Telemetry;
using TNWatch.Persistence.Entities;

namespace TNWatch.Persistence;

/// <summary>EF Core implementation of the ingestion unit of work. One SaveChanges = one transaction (NFA04).</summary>
public sealed class TelemetryRepository(TnWatchDbContext db) : ITelemetryRepository
{
    public Task<TrafficNode?> FindNodeAsync(string nodeId, CancellationToken ct) =>
        db.Nodes.FirstOrDefaultAsync(n => n.Id == nodeId, ct);

    public void AddNode(TrafficNode node) => db.Nodes.Add(node);

    public async Task<IReadOnlyList<double>> GetRecentVoltagesAsync(string nodeId, int count, CancellationToken ct)
    {
        if (count <= 0)
        {
            return [];
        }

        return await db.Measurements.AsNoTracking()
            .Where(m => m.NodeId == nodeId)
            .OrderByDescending(m => m.MeasuredAt)
            .Take(count)
            .Select(m => m.VoltageV)
            .ToListAsync(ct);
    }

    public void AddMeasurement(TelemetryReading reading) => db.Measurements.Add(MeasurementEntity.From(reading));

    public void AddStatusChange(StatusChange change) => db.StatusChanges.Add(StatusChangeEntity.From(change));

    public Task<bool> BatteryEventExistsAsync(Guid eventId, CancellationToken ct) =>
        db.BatteryEvents.AnyAsync(b => b.EventId == eventId, ct);

    public void AddBatteryEvent(BatteryEvent batteryEvent) => db.BatteryEvents.Add(BatteryEventEntity.From(batteryEvent));

    public async Task<IReadOnlyList<TrafficNode>> GetStaleNodesAsync(DateTimeOffset threshold, CancellationToken ct) =>
        await db.Nodes
            .Where(n => n.Status != NodeStatus.Offline && n.LastSeenAt != null && n.LastSeenAt <= threshold)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
