using TNWatch.Domain.Battery;

namespace TNWatch.Persistence.Entities;

/// <summary>Row in <c>core.battery_events</c>. <see cref="EventId"/> is the primary key → idempotent ingestion.</summary>
public sealed class BatteryEventEntity
{
    public Guid EventId { get; set; }

    public BatteryEventType Type { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string? NodeId { get; set; }

    public string? NewBatterySerial { get; set; }

    public string? OldBatterySerial { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public double? AccuracyM { get; set; }

    public static BatteryEventEntity From(BatteryEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return new BatteryEventEntity
        {
            EventId = e.EventId,
            Type = e.Type,
            OccurredAt = e.OccurredAt,
            NodeId = e.NodeId,
            NewBatterySerial = e.NewBatterySerial,
            OldBatterySerial = e.OldBatterySerial,
            Latitude = e.Latitude,
            Longitude = e.Longitude,
            AccuracyM = e.AccuracyM,
        };
    }

    public BatteryEvent ToDomain() =>
        new(EventId, Type, OccurredAt, NodeId, NewBatterySerial, OldBatterySerial, Latitude, Longitude, AccuracyM);
}
