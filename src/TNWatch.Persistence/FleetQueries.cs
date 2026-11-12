using Microsoft.EntityFrameworkCore;
using TNWatch.Domain.Battery;
using TNWatch.Domain.Fleet;

namespace TNWatch.Persistence;

/// <summary>
/// Read side for UI and API. Uses a short-lived context per call, so it is safe to use from
/// long-lived Blazor Server circuits (no shared DbContext across concurrent renders).
/// </summary>
public sealed class FleetQueries(IDbContextFactory<TnWatchDbContext> factory) : IFleetQueries
{
    public async Task<IReadOnlyList<NodeSnapshot>> GetNodesAsync(bool includeSimulated, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var nodes = await db.Nodes.AsNoTracking()
            .Where(n => includeSimulated || !n.IsSimulated)
            .OrderBy(n => n.Id)
            .ToListAsync(ct);
        return nodes.Select(n => n.ToSnapshot()).ToList();
    }

    public async Task<NodeSnapshot?> GetNodeAsync(string nodeId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var node = await db.Nodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == nodeId, ct);
        return node?.ToSnapshot();
    }

    public async Task<IReadOnlyList<MeasurementPoint>> GetMeasurementsAsync(string nodeId, DateTimeOffset since, DateTimeOffset until, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Measurements.AsNoTracking()
            .Where(m => m.NodeId == nodeId && m.MeasuredAt >= since && m.MeasuredAt <= until)
            .OrderBy(m => m.MeasuredAt)
            .Select(m => new MeasurementPoint(m.MeasuredAt, m.VoltageV, m.CurrentA, m.Latitude, m.Longitude, m.TemperatureC))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StatusChange>> GetStatusChangesAsync(string nodeId, int limit, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.StatusChanges.AsNoTracking()
            .Where(c => c.NodeId == nodeId)
            .OrderByDescending(c => c.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<BatteryEvent>> GetBatteryEventsAsync(string nodeId, int limit, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.BatteryEvents.AsNoTracking()
            .Where(b => b.NodeId == nodeId)
            .OrderByDescending(b => b.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<FleetSummary> GetSummaryAsync(bool includeSimulated, CancellationToken ct) =>
        (await GetNodesAsync(includeSimulated, ct)).Summarize();
}
