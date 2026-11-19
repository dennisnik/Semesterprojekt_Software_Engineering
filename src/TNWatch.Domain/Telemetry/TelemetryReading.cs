namespace TNWatch.Domain.Telemetry;

/// <summary>Origin of a telemetry message.</summary>
public enum TelemetrySource
{
    Mqtt = 0,
    Simulator = 1,
}

/// <summary>A validated, unit-normalized telemetry measurement (V, A, degrees).</summary>
public sealed record TelemetryReading(
    string NodeId,
    string? Tenant,
    DateTimeOffset MeasuredAt,
    DateTimeOffset ReceivedAt,
    double VoltageV,
    double CurrentA,
    double? Latitude,
    double? Longitude,
    double? AltitudeM,
    double? TemperatureC,
    int? Satellites,
    TelemetrySource Source);
