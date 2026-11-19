using TNWatch.TelemetryIngestion.Parsing;

namespace TNWatch.TelemetryIngestion.Tests;

public class TopicPatternTests
{
    [Fact]
    public void Extracts_placeholders_and_builds_filter()
    {
        var p = new TopicPattern("traffic-device/{tenant}/trafficnode/{nodeId}/status");

        Assert.Equal("traffic-device/+/trafficnode/+/status", p.SubscriptionFilter);
        Assert.True(p.TryMatch(TestData.RealTopic, out var v));
        Assert.Equal("dieringer-stark", v["tenant"]);
        Assert.Equal("TSN-002", v["nodeId"]);
    }

    [Theory]
    [InlineData("traffic-device/x/trafficnode/TSN-002/telemetry")]
    [InlineData("traffic-device/x/trafficnode/a/b/status")]
    [InlineData("")]
    public void Does_not_match_other_topics(string topic)
        => Assert.False(new TopicPattern("traffic-device/{tenant}/trafficnode/{nodeId}/status").TryMatch(topic, out _));

    [Fact]
    public void Wildcard_level_matches_without_capturing()
    {
        var p = new TopicPattern("tn-watch/{*}/{nodeId}");
        Assert.Equal("tn-watch/+/+", p.SubscriptionFilter);
        Assert.True(p.TryMatch("tn-watch/any/TN-1", out var v));
        Assert.Equal("TN-1", v["nodeId"]);
        Assert.Single(v);
    }

    [Fact]
    public void Special_characters_in_literal_levels_are_escaped()
        => Assert.False(new TopicPattern("a.b/{nodeId}").TryMatch("axb/1", out _));
}
