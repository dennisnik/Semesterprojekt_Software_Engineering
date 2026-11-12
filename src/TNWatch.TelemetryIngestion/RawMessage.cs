using TNWatch.Domain.Telemetry;

namespace TNWatch.TelemetryIngestion;

/// <summary>An unparsed message as received from MQTT or produced by the simulator.</summary>
public sealed record RawMessage(string Topic, ReadOnlyMemory<byte> Payload, DateTimeOffset ReceivedAt, TelemetrySource Source);
