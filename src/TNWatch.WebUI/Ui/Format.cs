using System.Globalization;
using TNWatch.Domain.Status;

namespace TNWatch.WebUI.Ui;

/// <summary>German UI formatting (de-DE numbers, Europe/Berlin times) and status presentation (NFA05).</summary>
public static class Format
{
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public static string Volt(double? v) => v is null ? "–" : v.Value.ToString("0.00", De) + " V";

    public static string Ampere(double? a) => a is null ? "–" : a.Value.ToString("0.00", De) + " A";

    public static string Coord(double? c) => c is null ? "–" : c.Value.ToString("0.00000", De);

    public static string Percent(int? p) => p is null ? "–" : p.Value.ToString(De) + " %";

    public static DateTimeOffset Local(DateTimeOffset t) => TimeZoneInfo.ConvertTime(t, Berlin);

    public static string DateTime(DateTimeOffset? t) => t is null ? "–" : Local(t.Value).ToString("dd.MM.yyyy HH:mm", De);

    /// <summary>"vor 3 Min." style relative time.</summary>
    public static string Ago(DateTimeOffset? t, DateTimeOffset now)
    {
        if (t is null)
        {
            return "nie";
        }

        var d = now - t.Value;
        if (d < TimeSpan.FromMinutes(1))
        {
            return "gerade eben";
        }

        if (d < TimeSpan.FromHours(1))
        {
            return $"vor {(int)d.TotalMinutes} Min.";
        }

        if (d < TimeSpan.FromDays(1))
        {
            return $"vor {(int)d.TotalHours} Std.";
        }

        return $"vor {(int)d.TotalDays} Tg.";
    }

    public static string Label(NodeStatus s) => s switch
    {
        NodeStatus.Normal => "Normal",
        NodeStatus.Warning => "Warnung",
        NodeStatus.Critical => "Kritisch",
        NodeStatus.Offline => "Offline",
        _ => "Unbekannt",
    };

    /// <summary>CSS modifier: status-normal | status-warning | status-critical | status-offline | status-unknown.</summary>
    public static string Css(NodeStatus s) => "status-" + s.ToString().ToLowerInvariant();
}
