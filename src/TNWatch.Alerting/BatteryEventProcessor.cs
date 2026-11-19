using Microsoft.Extensions.Logging;
using TNWatch.Domain.Assignments;
using TNWatch.Domain.Battery;
using TNWatch.Domain.Fleet;

namespace TNWatch.Alerting;

/// <summary>Stores battery lifecycle events idempotently (by eventId) and updates the installed battery of the node.</summary>
public sealed class BatteryEventProcessor(
    ITelemetryRepository repository,
    IFleetNotifier notifier,
    IAssignmentService assignments,
    ILogger<BatteryEventProcessor> logger) : IBatteryLifecycleHandler
{
    public async Task HandleAsync(BatteryEvent batteryEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(batteryEvent);

        if (await repository.BatteryEventExistsAsync(batteryEvent.EventId, ct))
        {
            // Already stored. If the node was recreated in the meantime (no battery known), restore its battery from
            // the event; never overwrite a known battery with an old, re-delivered event.
            if (batteryEvent is { Type: BatteryEventType.Exchange, NodeId: not null, NewBatterySerial: not null } &&
                await repository.FindNodeAsync(batteryEvent.NodeId, ct) is { BatterySerial: null } recreated)
            {
                recreated.InstallBattery(batteryEvent.NewBatterySerial);
                await repository.SaveChangesAsync(ct);
                await notifier.PublishNodeUpdatedAsync(recreated.ToSnapshot());
            }

            logger.LogDebug("Duplicate battery event {EventId} ignored", batteryEvent.EventId);
            return;
        }

        repository.AddBatteryEvent(batteryEvent);

        TrafficNode? node = null;
        if (batteryEvent is { Type: BatteryEventType.Exchange, NodeId: not null, NewBatterySerial: not null })
        {
            node = await repository.FindNodeAsync(batteryEvent.NodeId, ct);
            node?.InstallBattery(batteryEvent.NewBatterySerial);
        }

        await repository.SaveChangesAsync(ct);
        logger.LogInformation("Battery event {Type} {EventId} for node {NodeId}", batteryEvent.Type, batteryEvent.EventId, batteryEvent.NodeId);

        if (node is not null)
        {
            await notifier.PublishNodeUpdatedAsync(node.ToSnapshot());
        }

        // F11: the job is done once the battery was exchanged → the node is free again.
        if (batteryEvent is { Type: BatteryEventType.Exchange, NodeId: not null })
        {
            await assignments.ReleaseAfterBatteryExchangeAsync(batteryEvent.NodeId, ct);
        }
    }
}
