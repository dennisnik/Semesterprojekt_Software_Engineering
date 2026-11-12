using System.Text;
using TNWatch.Domain.Telemetry;

namespace TNWatch.TelemetryIngestion.Tests;

internal static class TestData
{
    public const string RealTopic = "traffic-device/dieringer-stark/trafficnode/TSN-002/status";
    public static readonly DateTimeOffset ReceivedAt = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    public static RawMessage Msg(string json, string topic = RealTopic) =>
        new(topic, Encoding.UTF8.GetBytes(json), ReceivedAt, TelemetrySource.Mqtt);

    /// <summary>Shape of data/sample_msg.json with realistic values.</summary>
    public const string SampleMsg = """
        { "current_mA": 551.0, "voltage_mV": 11276.0, "lat": 48.201157, "lon": 8.649726,
          "altitude_m": 609.6, "satellites": 7, "time": "2026-11-12T08:59:30Z", "temperature_C": 14.5 }
        """;

    /// <summary>Field names as in data/traffic_devices_signals.csv.</summary>
    public const string CsvStyle = """
        { "_time": "2026-03-19T08:50:50.836520127Z", "device_id": "TSN-002", "current_A": 0.551,
          "latitude": 48.201157, "longitude": 8.649726, "voltage_V": 11.276 }
        """;

    public const string Exchange = """
        { "eventId": "a3d364ef-9e0b-45b7-a6f5-087047fca142", "date": "2026-11-05", "time": "11:30:00.000",
          "newBat": "BAT0001", "oldBat": "BAT0002", "trafficNode": "TN0001",
          "position": { "latitude": 52.52, "longitude": 13.405, "accuracy": 8 } }
        """;

    public const string Discard = """
        { "eventId": "b7f2a83d-76c1-4db5-891e-ae574e42f063", "date": "2026-11-05", "time": "11:35:00.000", "serial": "BAT0002" }
        """;
}
