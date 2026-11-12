using TNWatch.Domain.Telemetry;

namespace TNWatch.Persistence.Entities;

/// <summary>Row in <c>telemetry.measurements</c>.</summary>
public sealed class MeasurementEntity
{
    public long Id { get; set; }

    public required string NodeId { get; set; }

    public DateTimeOffset MeasuredAt { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public double VoltageV { get; set; }

    public double CurrentA { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public double? AltitudeM { get; set; }

    public double? TemperatureC { get; set; }

    public int? Satellites { get; set; }

    public TelemetrySource Source { get; set; }

    public static MeasurementEntity From(TelemetryReading r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new MeasurementEntity
        {
            NodeId = r.NodeId,
            MeasuredAt = r.MeasuredAt,
            ReceivedAt = r.ReceivedAt,
            VoltageV = r.VoltageV,
            CurrentA = r.CurrentA,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            AltitudeM = r.AltitudeM,
            TemperatureC = r.TemperatureC,
            Satellites = r.Satellites,
            Source = r.Source,
        };
    }
}
