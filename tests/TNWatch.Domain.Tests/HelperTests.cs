using TNWatch.Domain.Export;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Routing;

namespace TNWatch.Domain.Tests;

public class HelperTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Route_with_single_stop_has_only_destination()
    {
        var uri = GoogleMapsRouteBuilder.Build([new RouteStop("A", 48.7758, 9.1829)]);
        Assert.Equal("https://www.google.com/maps/dir/?api=1&travelmode=driving&destination=48.7758%2C9.1829", uri.AbsoluteUri);
    }

    [Fact]
    public void Route_with_multiple_stops_uses_last_as_destination()
    {
        var uri = GoogleMapsRouteBuilder.Build([new RouteStop("A", 48.1, 9.1), new RouteStop("B", 48.2, 9.2), new RouteStop("C", 48.3, 9.3)]);
        Assert.EndsWith("destination=48.3%2C9.3&waypoints=48.1%2C9.1%7C48.2%2C9.2", uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void Route_validates_stop_count()
    {
        Assert.Throws<ArgumentException>(() => GoogleMapsRouteBuilder.Build([]));
        var tooMany = Enumerable.Range(0, GoogleMapsRouteBuilder.MaxStops + 1).Select(i => new RouteStop($"N{i}", 48, 9)).ToList();
        Assert.Throws<ArgumentException>(() => GoogleMapsRouteBuilder.Build(tooMany));
    }

    [Fact]
    public void Location_link_uses_invariant_coordinates()
        => Assert.Equal("https://www.google.com/maps/search/?api=1&query=48.201157%2C8.649726", GoogleMapsRouteBuilder.Location(48.201157, 8.649726).AbsoluteUri);

    [Fact]
    public void Csv_contains_header_and_invariant_rows()
    {
        var csv = MeasurementCsv.Build("TSN-002", [
            new MeasurementPoint(T0, 11.276, 0.551, 48.201157, 8.649726, null),
            new MeasurementPoint(T0.AddMinutes(3), 11.304, -0.1, null, null, 14.5),
        ]);

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(MeasurementCsv.Header, lines[0]);
        Assert.Equal("TSN-002,2026-11-12T08:00:00.000Z,11.276,0.551,48.201157,8.649726,", lines[1]);
        Assert.Equal("TSN-002,2026-11-12T08:03:00.000Z,11.304,-0.1,,,14.5", lines[2]);
    }

    [Fact]
    public void Csv_escapes_special_node_ids()
        => Assert.StartsWith("\"a,\"\"b\"\"\",", MeasurementCsv.Build("a,\"b\"", [new MeasurementPoint(T0, 12, 1, null, null, null)]).Split("\r\n")[1], StringComparison.Ordinal);

    [Fact]
    public void Downsampling_keeps_small_series_and_averages_large_ones()
    {
        var small = Enumerable.Range(0, 10).Select(i => new MeasurementPoint(T0.AddMinutes(i), 12, 1, null, null, null)).ToList();
        Assert.Same(small, MeasurementDownsampler.Downsample(small, 100));

        var large = Enumerable.Range(0, 1000).Select(i => new MeasurementPoint(T0.AddMinutes(i), i % 2 == 0 ? 12.0 : 13.0, 1, null, null, null)).ToList();
        var result = MeasurementDownsampler.Downsample(large, 100);

        Assert.Equal(100, result.Count);
        Assert.All(result, p => Assert.Equal(12.5, p.VoltageV, 3));
        Assert.True(result.Zip(result.Skip(1)).All(pair => pair.First.MeasuredAt < pair.Second.MeasuredAt));
        Assert.Throws<ArgumentOutOfRangeException>(() => MeasurementDownsampler.Downsample(small, 0));
    }
}
