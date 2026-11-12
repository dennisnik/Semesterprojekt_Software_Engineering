using System.Globalization;
using System.Text;
using TNWatch.Domain.Fleet;

namespace TNWatch.Domain.Export;

/// <summary>
/// CSV export of a node's measurements (F08). RFC 4180, comma separator, invariant number format (decimal point)
/// and ISO-8601 UTC timestamps, so the file can be read by statistics tools without locale issues.
/// </summary>
public static class MeasurementCsv
{
    public const string Header = "node_id,measured_at_utc,voltage_v,current_a,latitude,longitude,temperature_c";

    public static string Build(string nodeId, IEnumerable<MeasurementPoint> points)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(points);

        var sb = new StringBuilder();
        sb.Append(Header).Append("\r\n");
        foreach (var p in points)
        {
            sb.Append(Escape(nodeId)).Append(',')
              .Append(p.MeasuredAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)).Append(',')
              .Append(Num(p.VoltageV)).Append(',')
              .Append(Num(p.CurrentA)).Append(',')
              .Append(Num(p.Latitude)).Append(',')
              .Append(Num(p.Longitude)).Append(',')
              .Append(Num(p.TemperatureC)).Append("\r\n");
        }

        return sb.ToString();
    }

    private static string Num(double? value) => value?.ToString("0.######", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
}
