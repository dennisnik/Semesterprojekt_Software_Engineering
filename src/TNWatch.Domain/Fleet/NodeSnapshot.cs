using TNWatch.Domain.Status;

namespace TNWatch.Domain.Fleet;

/// <summary>Current state of one TrafficNode as shown in map, popup and dashboard.</summary>
public sealed record NodeSnapshot(
    string Id,
    string? Tenant,
    bool IsSimulated,
    NodeStatus Status,
    double? VoltageV,
    double? SmoothedVoltageV,
    double? CurrentA,
    int? StateOfChargePercent,
    double? Latitude,
    double? Longitude,
    DateTimeOffset? LastSeenAt,
    string? BatterySerial);
