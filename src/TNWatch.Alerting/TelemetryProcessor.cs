using Microsoft.Extensions.Logging;
using TNWatch.Domain.Diagnostics;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Status;
using TNWatch.Domain.Telemetry;

namespace TNWatch.Alerting;

/// <summary>
/// Handles validated telemetry (ADR-07): persist the measurement, update the node snapshot,
/// evaluate the status (F02), log status changes (F06) – all in one transaction – then push live updates.
/// </summary>
public sealed class TelemetryProcessor(
    ITelemetryRepository repository,
    StatusEvaluator evaluator,
    IFleetNotifier notifier,
    IngestionStats stats,
    TimeProvider time,
    ILogger<TelemetryProcessor> logger) : ITelemetryHandler
{
    public async Task HandleAsync(TelemetryReading reading, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var now = time.GetUtcNow();

        var node = await repository.FindNodeAsync(reading.NodeId, ct);
        if (node is null)
        {
            node = new TrafficNode(reading.NodeId, reading.Tenant, reading.Source == TelemetrySource.Simulator, now);
            repository.AddNode(node);
            logger.LogInformation("New TrafficNode {NodeId} (tenant {Tenant}, source {Source})", reading.NodeId, reading.Tenant, reading.Source);
        }

        var previous = await repository.GetRecentVoltagesAsync(reading.NodeId, evaluator.Rules.SmoothingWindow - 1, ct);
        var recent = new List<double>(previous.Count + 1) { reading.VoltageV };
        recent.AddRange(previous);

        repository.AddMeasurement(reading);
        var change = node.ApplyReading(reading, recent, evaluator, now);
        if (change is not null)
        {
            repository.AddStatusChange(change);
        }

        await repository.SaveChangesAsync(ct);
        stats.RecordProcessed(time.GetUtcNow() - reading.ReceivedAt);

        await notifier.PublishNodeUpdatedAsync(node.ToSnapshot());
        if (change is not null)
        {
            logger.LogInformation("Status {NodeId}: {From} → {To} (Ø {Voltage} V)", change.NodeId, change.From, change.To, change.SmoothedVoltageV);
            await notifier.PublishStatusChangedAsync(change);
        }
    }
}
