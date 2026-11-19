using System.Text;
using TNWatch.Domain.Battery;
using TNWatch.TelemetryIngestion.Parsing;

namespace TNWatch.TelemetryIngestion.Tests;

public class BatteryEventParserTests
{
    private readonly BatteryEventParser _sut;

    public BatteryEventParserTests()
    {
        var options = new IngestionOptions();
        options.BatteryEventTopicPatterns.Add("traffic-device/{tenant}/battery/{*}");
        _sut = new BatteryEventParser(options);
    }

    [Fact]
    public void Parses_exchange_event_with_local_time()
    {
        var r = _sut.Parse(TestData.Msg(TestData.Exchange));

        Assert.True(r.IsSuccess, r.Error);
        var e = r.Value!;
        Assert.Equal(Guid.Parse("a3d364ef-9e0b-45b7-a6f5-087047fca142"), e.EventId);
        Assert.Equal(BatteryEventType.Exchange, e.Type);
        Assert.Equal("TN0001", e.NodeId);
        Assert.Equal("BAT0001", e.NewBatterySerial);
        Assert.Equal("BAT0002", e.OldBatterySerial);
        Assert.Equal(52.52, e.Latitude);
        Assert.Equal(13.405, e.Longitude);
        Assert.Equal(8, e.AccuracyM);
        // 11:30 Europe/Berlin in November = UTC+1
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 10, 30, 0, TimeSpan.Zero), e.OccurredAt.ToUniversalTime());
    }

    [Fact]
    public void Parses_discard_event()
    {
        var r = _sut.Parse(TestData.Msg(TestData.Discard));

        Assert.True(r.IsSuccess, r.Error);
        Assert.Equal(BatteryEventType.Discard, r.Value!.Type);
        Assert.Equal("BAT0002", r.Value.OldBatterySerial);
        Assert.Null(r.Value.NodeId);
    }

    [Fact]
    public void Missing_date_uses_receive_time()
    {
        var r = _sut.Parse(TestData.Msg("""{ "eventId": "b7f2a83d-76c1-4db5-891e-ae574e42f063", "serial": "BAT9" }"""));
        Assert.Equal(TestData.ReceivedAt, r.Value!.OccurredAt);
    }

    [Theory]
    [InlineData("""{ "eventId": "nope", "serial": "BAT1" }""", "eventId")]
    [InlineData("""{ "eventId": "b7f2a83d-76c1-4db5-891e-ae574e42f063" }""", "Neither")]
    [InlineData("""{ "eventId": "b7f2a83d-76c1-4db5-891e-ae574e42f063", "newBat": "BAT1" }""", "trafficNode")]
    [InlineData("""{ "eventId": "b7f2a83d-76c1-4db5-891e-ae574e42f063", "serial": "B", "date": "05.13.2026" }""", "Invalid date")]
    [InlineData("""{ "eventId": """, "Invalid JSON")]
    [InlineData("""42""", "not a JSON object")]
    public void Rejects_invalid_events(string json, string expected)
    {
        var r = _sut.Parse(TestData.Msg(json));
        Assert.False(r.IsSuccess);
        Assert.Contains(expected, r.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Topic_and_payload_detection()
    {
        Assert.True(_sut.Matches("traffic-device/acme/battery/exchange"));
        Assert.False(_sut.Matches(TestData.RealTopic));
        Assert.Single(_sut.Patterns);
        Assert.True(BatteryEventParser.LooksLikeBatteryEvent(Encoding.UTF8.GetBytes(TestData.Exchange)));
        Assert.False(BatteryEventParser.LooksLikeBatteryEvent(Encoding.UTF8.GetBytes(TestData.SampleMsg)));
    }
}
