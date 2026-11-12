using System.Globalization;
using System.Text;

namespace TNWatch.Domain.Routing;

public sealed record RouteStop(string NodeId, double Latitude, double Longitude);

/// <summary>
/// Builds a Google Maps directions link (Maps URLs API, no API key) for the route planner (F07).
/// The route starts at the device's current location; the last stop is the destination, the others are waypoints.
/// </summary>
public static class GoogleMapsRouteBuilder
{
    /// <summary>Google Maps URLs support up to 9 waypoints + 1 destination.</summary>
    public const int MaxStops = 10;

    public static Uri Build(IReadOnlyList<RouteStop> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count == 0)
        {
            throw new ArgumentException("At least one stop is required.", nameof(stops));
        }

        if (stops.Count > MaxStops)
        {
            throw new ArgumentException($"Google Maps supports at most {MaxStops} stops.", nameof(stops));
        }

        var sb = new StringBuilder("https://www.google.com/maps/dir/?api=1&travelmode=driving&destination=");
        sb.Append(Coordinate(stops[^1]));
        if (stops.Count > 1)
        {
            sb.Append("&waypoints=");
            sb.Append(string.Join("%7C", stops.Take(stops.Count - 1).Select(Coordinate)));
        }

        return new Uri(sb.ToString());
    }

    /// <summary>Link that shows a single node location in Google Maps (for technicians, e.g. persona Anja).</summary>
    public static Uri Location(double latitude, double longitude) =>
        new(string.Create(CultureInfo.InvariantCulture, $"https://www.google.com/maps/search/?api=1&query={latitude:0.######}%2C{longitude:0.######}"));

    private static string Coordinate(RouteStop s) =>
        string.Create(CultureInfo.InvariantCulture, $"{s.Latitude:0.######}%2C{s.Longitude:0.######}");
}
