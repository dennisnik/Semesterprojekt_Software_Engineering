using TNWatch.Domain.Telemetry;
using TNWatch.TelemetryIngestion.Parsing;

namespace TNWatch.TelemetryIngestion.Tests;

public class TelemetryParserTests
{
    private readonly TelemetryParser _sut = new(new IngestionOptions());

    [Fact]
    public void Parses_sample_msg_format_and_converts_units()
    {
        var r = _sut.Parse(TestData.Msg(TestData.SampleMsg));

        Assert.True(r.IsSuccess, r.Error);
        var t = r.Value!;
        Assert.Equal("TSN-002", t.NodeId);
        Assert.Equal("dieringer-stark", t.Tenant);
        Assert.Equal(11.276, t.VoltageV, 3);
        Assert.Equal(0.551, t.CurrentA, 4);
        Assert.Equal(48.201157, t.Latitude);
        Assert.Equal(8.649726, t.Longitude);
        Assert.Equal(609.6, t.AltitudeM);
        Assert.Equal(7, t.Satellites);
        Assert.Equal(14.5, t.TemperatureC);
        Assert.Equal(new DateTimeOffset(2026, 11, 12, 8, 59, 30, TimeSpan.Zero), t.MeasuredAt);
        Assert.Equal(TelemetrySource.Mqtt, t.Source);
    }

    [Fact]
    public void Parses_csv_field_names_with_node_id_from_payload()
    {
        var r = _sut.Parse(TestData.Msg(TestData.CsvStyle, topic: "some/other/topic"));

        Assert.True(r.IsSuccess, r.Error);
        Assert.Equal("TSN-002", r.Value!.NodeId);
        Assert.Null(r.Value.Tenant);
        Assert.Equal(11.276, r.Value.VoltageV, 3);
        Assert.Equal(0.551, r.Value.CurrentA, 4);
    }

    [Fact]
    public void Null_time_and_gps_fall_back_gracefully()
    {
        var r = _sut.Parse(TestData.Msg("""{ "current_mA": 100, "voltage_mV": 12600, "lat": null, "lon": null, "time": null }"""));

        Assert.True(r.IsSuccess, r.Error);
        Assert.Null(r.Value!.Latitude);
        Assert.Null(r.Value.Longitude);
        Assert.Equal(TestData.ReceivedAt, r.Value.MeasuredAt);
    }

    [Theory]
    [InlineData("""{ "voltage_V": 12.5, "current_A": 0.5, "lat": 0, "lon": 0 }""")]
    [InlineData("""{ "voltage_V": 12.5, "current_A": 0.5, "lat": 91, "lon": 8 }""")]
    [InlineData("""{ "voltage_V": 12.5, "current_A": 0.5, "lat": 48 }""")]
    public void Implausible_or_incomplete_positions_become_unknown(string json)
    {
        var r = _sut.Parse(TestData.Msg(json));
        Assert.True(r.IsSuccess, r.Error);
        Assert.Null(r.Value!.Latitude);
    }

    [Theory]
    [InlineData("""{ "current_mA": 0.00, "voltage_mV": 0.00 }""", "Voltage")]          // literal data/sample_msg.json
    [InlineData("""{ "current_A": 0.5, "voltage_V": 25 }""", "Voltage")]
    [InlineData("""{ "current_A": 60, "voltage_V": 12 }""", "Current")]
    [InlineData("""{ "current_A": 0.5 }""", "Voltage missing")]
    [InlineData("""{ "voltage_V": 12.5 }""", "Current missing")]
    [InlineData("""{ "voltage_V": "abc", "current_A": 1 }""", "not a number")]
    [InlineData("""{ "voltage_V": 12.5, "current_A": 0.5""", "Invalid JSON")]
    [InlineData("""[1, 2, 3]""", "not a JSON object")]
    public void Rejects_invalid_payloads(string json, string expectedError)
    {
        var r = _sut.Parse(TestData.Msg(json));
        Assert.False(r.IsSuccess);
        Assert.Contains(expectedError, r.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_message_without_node_id()
    {
        var r = _sut.Parse(TestData.Msg("""{ "voltage_V": 12.5, "current_A": 0.5 }""", topic: "unknown/topic"));
        Assert.False(r.IsSuccess);
        Assert.Contains("Node ID", r.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Numeric_strings_and_unix_timestamps_are_accepted()
    {
        var unix = TestData.ReceivedAt.AddMinutes(-1).ToUnixTimeSeconds();
        var r = _sut.Parse(TestData.Msg($$"""{ "voltage_V": "12.5", "current_A": "-0.2", "timestamp": {{unix}} }"""));

        Assert.True(r.IsSuccess, r.Error);
        Assert.Equal(12.5, r.Value!.VoltageV);
        Assert.Equal(-0.2, r.Value.CurrentA); // negative = charging (glossary)
        Assert.Equal(TestData.ReceivedAt.AddMinutes(-1), r.Value.MeasuredAt);
    }

    [Fact]
    public void Unix_milliseconds_are_accepted()
    {
        var ms = TestData.ReceivedAt.AddSeconds(-30).ToUnixTimeMilliseconds();
        var r = _sut.Parse(TestData.Msg($$"""{ "voltage_V": 12.5, "current_A": 0.2, "time": {{ms}} }"""));
        Assert.Equal(TestData.ReceivedAt.AddSeconds(-30), r.Value!.MeasuredAt);
    }

    [Fact]
    public void Future_device_time_beyond_skew_is_replaced_by_receive_time()
    {
        var r = _sut.Parse(TestData.Msg("""{ "voltage_V": 12.5, "current_A": 0.5, "time": "2030-01-01T00:00:00Z" }"""));
        Assert.Equal(TestData.ReceivedAt, r.Value!.MeasuredAt);
    }

    [Fact]
    public void Unparsable_time_falls_back_to_receive_time()
    {
        var r = _sut.Parse(TestData.Msg("""{ "voltage_V": 12.5, "current_A": 0.5, "time": "yesterday" }"""));
        Assert.Equal(TestData.ReceivedAt, r.Value!.MeasuredAt);
    }

    [Fact]
    public void Matches_uses_configured_patterns()
    {
        Assert.True(_sut.Matches(TestData.RealTopic));
        Assert.False(_sut.Matches("traffic-device/x/battery/exchange"));
        Assert.Single(_sut.Patterns);
    }
}
