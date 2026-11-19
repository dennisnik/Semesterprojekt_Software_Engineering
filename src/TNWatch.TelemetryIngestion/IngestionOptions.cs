namespace TNWatch.TelemetryIngestion;

/// <summary>Configuration section "Ingestion": topic patterns and parsing rules (MQTT contract, docs/interfaces/mqtt-contract.md).</summary>
public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>
    /// Telemetry topic patterns. Placeholders: <c>{tenant}</c>, <c>{nodeId}</c>, and <c>{*}</c> (any single level).
    /// Each pattern is also used to derive the MQTT subscription filter (placeholders → <c>+</c>).
    /// </summary>
    /// <remarks>
    /// Live format on the course broker (seen 19.11.2026): <c>traffic-nodes/TN0001/measurements</c>.
    /// The second pattern is the format of the older export in <c>data/traffic_devices_signals.csv</c>.
    /// The first pattern is also used by the simulator for its topics.
    /// </remarks>
    public IList<string> TelemetryTopicPatterns { get; } =
    [
        "traffic-nodes/{nodeId}/measurements",
        "traffic-device/{tenant}/trafficnode/{nodeId}/status",
    ];

    /// <summary>Battery event topic patterns (exchange/discard). Same placeholder syntax.</summary>
    public IList<string> BatteryEventTopicPatterns { get; } = [];

    /// <summary>Time zone for battery events that carry a local date/time without offset.</summary>
    public string EventTimeZone { get; set; } = "Europe/Berlin";

    /// <summary>Device timestamps further in the future than this are replaced by the receive time.</summary>
    public TimeSpan MaxClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Capacity of the bounded ingestion channel (back-pressure).</summary>
    public int ChannelCapacity { get; set; } = 10_000;
}
