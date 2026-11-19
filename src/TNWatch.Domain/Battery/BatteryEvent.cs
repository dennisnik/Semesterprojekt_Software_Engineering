namespace TNWatch.Domain.Battery;

public enum BatteryEventType
{
    /// <summary>Battery was exchanged at a TrafficNode (newBat replaces oldBat).</summary>
    Exchange = 0,

    /// <summary>Battery was discarded (end of life).</summary>
    Discard = 1,
}

/// <summary>A battery lifecycle event (data/exchange.example.json, data/discard.example.json).</summary>
public sealed record BatteryEvent(
    Guid EventId,
    BatteryEventType Type,
    DateTimeOffset OccurredAt,
    string? NodeId,
    string? NewBatterySerial,
    string? OldBatterySerial,
    double? Latitude,
    double? Longitude,
    double? AccuracyM);
