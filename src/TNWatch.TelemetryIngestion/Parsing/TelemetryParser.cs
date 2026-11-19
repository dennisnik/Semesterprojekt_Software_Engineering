using System.Globalization;
using System.Text.Json;
using TNWatch.Domain.Telemetry;

namespace TNWatch.TelemetryIngestion.Parsing;

/// <summary>
/// Parses and validates telemetry JSON (F01). Supported field names (case-insensitive):
/// voltage_mV | voltage_V | voltage, current_mA | current_A | current, lat | latitude, lon | lng | longitude,
/// altitude_m, satellites, temperature_C, time | timestamp | _time (ISO-8601 or Unix seconds/ms).
/// The node ID comes from the topic (<c>{nodeId}</c>) or the fields device_id | node_id | nodeId.
/// </summary>
public sealed class TelemetryParser
{
    public const double MaxVoltage = 20.0;
    public const double MaxAbsCurrent = 50.0;

    private readonly IReadOnlyList<TopicPattern> _patterns;
    private readonly TimeSpan _maxClockSkew;

    public TelemetryParser(IngestionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _patterns = options.TelemetryTopicPatterns.Select(p => new TopicPattern(p)).ToList();
        _maxClockSkew = options.MaxClockSkew;
    }

    public IReadOnlyList<TopicPattern> Patterns => _patterns;

    public bool Matches(string topic) => _patterns.Any(p => p.TryMatch(topic, out _));

    public ParseResult<TelemetryReading> Parse(RawMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        IReadOnlyDictionary<string, string> topicValues = new Dictionary<string, string>();
        foreach (var pattern in _patterns)
        {
            if (pattern.TryMatch(message.Topic, out topicValues))
            {
                break;
            }
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(message.Payload);
        }
        catch (JsonException ex)
        {
            return ParseResult<TelemetryReading>.Fail($"Invalid JSON: {ex.Message}");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ParseResult<TelemetryReading>.Fail("Payload is not a JSON object.");
            }

            try
            {
                return Build(root, topicValues, message);
            }
            catch (FormatException ex)
            {
                return ParseResult<TelemetryReading>.Fail(ex.Message);
            }
        }
    }

    private ParseResult<TelemetryReading> Build(JsonElement root, IReadOnlyDictionary<string, string> topicValues, RawMessage message)
    {
        var nodeId = topicValues.GetValueOrDefault("nodeId") ?? JsonFields.String(root, "device_id", "node_id", "nodeId", "trafficNode");
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return ParseResult<TelemetryReading>.Fail("Node ID missing (neither in topic nor payload).");
        }

        var tenant = topicValues.GetValueOrDefault("tenant") ?? JsonFields.String(root, "tenant");

        var voltage = JsonFields.FirstNumber(root, ("voltage_mV", 0.001), ("voltage_V", 1), ("voltage", 1));
        var current = JsonFields.FirstNumber(root, ("current_mA", 0.001), ("current_A", 1), ("current", 1));
        if (voltage is null)
        {
            return ParseResult<TelemetryReading>.Fail("Voltage missing.");
        }

        if (current is null)
        {
            return ParseResult<TelemetryReading>.Fail("Current missing.");
        }

        if (voltage <= 0 || voltage > MaxVoltage || double.IsNaN(voltage.Value))
        {
            return ParseResult<TelemetryReading>.Fail($"Voltage {voltage:F3} V out of plausible range (0, {MaxVoltage}].");
        }

        if (Math.Abs(current.Value) > MaxAbsCurrent || double.IsNaN(current.Value))
        {
            return ParseResult<TelemetryReading>.Fail($"Current {current:F3} A out of plausible range ±{MaxAbsCurrent}.");
        }

        var (lat, lon) = ReadPosition(root);

        return ParseResult<TelemetryReading>.Ok(new TelemetryReading(
            NodeId: nodeId,
            Tenant: tenant,
            MeasuredAt: ReadTimestamp(root, message.ReceivedAt),
            ReceivedAt: message.ReceivedAt,
            VoltageV: Math.Round(voltage.Value, 3),
            CurrentA: Math.Round(current.Value, 4),
            Latitude: lat,
            Longitude: lon,
            AltitudeM: JsonFields.Number(root, "altitude_m"),
            TemperatureC: JsonFields.FirstNumber(root, ("temperature_C", 1), ("temperature", 1)),
            Satellites: JsonFields.Number(root, "satellites") is { } sats ? (int)sats : null,
            Source: message.Source));
    }

    /// <summary>Position is optional; out-of-range values and the 0/0 "no fix" position are treated as unknown.</summary>
    private static (double? Lat, double? Lon) ReadPosition(JsonElement root)
    {
        var lat = JsonFields.FirstNumber(root, ("lat", 1), ("latitude", 1));
        var lon = JsonFields.FirstNumber(root, ("lon", 1), ("lng", 1), ("longitude", 1));
        if (lat is null || lon is null || Math.Abs(lat.Value) > 90 || Math.Abs(lon.Value) > 180 || (lat == 0 && lon == 0))
        {
            return (null, null);
        }

        return (lat, lon);
    }

    private DateTimeOffset ReadTimestamp(JsonElement root, DateTimeOffset receivedAt)
    {
        var raw = JsonFields.String(root, "time", "timestamp", "_time");
        DateTimeOffset? parsed = null;
        if (raw is not null)
        {
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            {
                parsed = dto;
            }
            else if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
            {
                parsed = unix > 100_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds(unix) : DateTimeOffset.FromUnixTimeSeconds(unix);
            }
        }

        // Missing, unparsable or implausible future device time → fall back to receive time.
        return parsed is { } t && t - receivedAt <= _maxClockSkew ? t : receivedAt;
    }
}
