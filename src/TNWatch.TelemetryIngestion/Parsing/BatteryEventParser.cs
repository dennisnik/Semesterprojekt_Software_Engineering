using System.Globalization;
using System.Text.Json;
using TNWatch.Domain.Battery;

namespace TNWatch.TelemetryIngestion.Parsing;

/// <summary>
/// Parses battery lifecycle events (data/exchange.example.json, data/discard.example.json).
/// Exchange: eventId, date, time, newBat, oldBat, trafficNode, position{latitude, longitude, accuracy}.
/// Discard:  eventId, date, time, serial.
/// </summary>
public sealed class BatteryEventParser
{
    private readonly IReadOnlyList<TopicPattern> _patterns;
    private readonly TimeZoneInfo _timeZone;

    public BatteryEventParser(IngestionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _patterns = options.BatteryEventTopicPatterns.Select(p => new TopicPattern(p)).ToList();
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.EventTimeZone);
    }

    public IReadOnlyList<TopicPattern> Patterns => _patterns;

    public bool Matches(string topic) => _patterns.Any(p => p.TryMatch(topic, out _));

    /// <summary>Heuristic for payloads on unknown topics: battery events always carry an eventId.</summary>
    public static bool LooksLikeBatteryEvent(ReadOnlySpan<byte> payload) =>
        payload.IndexOf("\"eventId\""u8) >= 0;

    public ParseResult<BatteryEvent> Parse(RawMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        try
        {
            using var doc = JsonDocument.Parse(message.Payload);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ParseResult<BatteryEvent>.Fail("Payload is not a JSON object.");
            }

            if (!Guid.TryParse(JsonFields.String(root, "eventId"), out var eventId))
            {
                return ParseResult<BatteryEvent>.Fail("eventId missing or not a UUID.");
            }

            var occurredAt = ReadOccurredAt(root) ?? message.ReceivedAt;
            var newBat = JsonFields.String(root, "newBat");
            var oldBat = JsonFields.String(root, "oldBat");
            var serial = JsonFields.String(root, "serial");
            var node = JsonFields.String(root, "trafficNode");

            double? lat = null, lon = null, acc = null;
            if (JsonFields.TryGet(root, "position", out var pos) && pos.ValueKind == JsonValueKind.Object)
            {
                lat = JsonFields.FirstNumber(pos, ("latitude", 1), ("lat", 1));
                lon = JsonFields.FirstNumber(pos, ("longitude", 1), ("lon", 1));
                acc = JsonFields.Number(pos, "accuracy");
            }

            if (newBat is not null)
            {
                if (node is null)
                {
                    return ParseResult<BatteryEvent>.Fail("Exchange event without trafficNode.");
                }

                return ParseResult<BatteryEvent>.Ok(new BatteryEvent(eventId, BatteryEventType.Exchange, occurredAt, node, newBat, oldBat, lat, lon, acc));
            }

            if (serial is not null)
            {
                return ParseResult<BatteryEvent>.Ok(new BatteryEvent(eventId, BatteryEventType.Discard, occurredAt, node, null, serial, lat, lon, acc));
            }

            return ParseResult<BatteryEvent>.Fail("Neither newBat (exchange) nor serial (discard) present.");
        }
        catch (JsonException ex)
        {
            return ParseResult<BatteryEvent>.Fail($"Invalid JSON: {ex.Message}");
        }
        catch (FormatException ex)
        {
            return ParseResult<BatteryEvent>.Fail(ex.Message);
        }
    }

    private DateTimeOffset? ReadOccurredAt(JsonElement root)
    {
        var date = JsonFields.String(root, "date");
        if (date is null)
        {
            return null;
        }

        var time = JsonFields.String(root, "time") ?? "00:00:00";
        if (!DateTime.TryParse($"{date}T{time}", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            throw new FormatException($"Invalid date/time '{date} {time}'.");
        }

        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, _timeZone.GetUtcOffset(unspecified));
    }
}
