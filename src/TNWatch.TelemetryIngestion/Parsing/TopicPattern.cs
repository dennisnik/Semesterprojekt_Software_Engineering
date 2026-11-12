using System.Text.RegularExpressions;

namespace TNWatch.TelemetryIngestion.Parsing;

/// <summary>
/// A topic template like <c>traffic-device/{tenant}/trafficnode/{nodeId}/status</c>.
/// Matches concrete topics, extracts placeholders and yields the MQTT subscription filter.
/// </summary>
public sealed partial class TopicPattern
{
    private readonly Regex _regex;

    public TopicPattern(string template)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        Template = template;

        var levels = template.Split('/');
        var regexParts = new List<string>(levels.Length);
        var filterParts = new List<string>(levels.Length);
        foreach (var level in levels)
        {
            var m = PlaceholderRegex().Match(level);
            if (m.Success)
            {
                var name = m.Groups["name"].Value;
                regexParts.Add(name == "*" ? "[^/]+" : $"(?<{name}>[^/]+)");
                filterParts.Add("+");
            }
            else
            {
                regexParts.Add(Regex.Escape(level));
                filterParts.Add(level);
            }
        }

        _regex = new Regex("^" + string.Join('/', regexParts) + "$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        SubscriptionFilter = string.Join('/', filterParts);
    }

    public string Template { get; }

    /// <summary>MQTT filter, e.g. <c>traffic-device/+/trafficnode/+/status</c>.</summary>
    public string SubscriptionFilter { get; }

    /// <summary>Tries to match a topic; returns the extracted placeholder values.</summary>
    public bool TryMatch(string topic, out IReadOnlyDictionary<string, string> values)
    {
        var m = _regex.Match(topic ?? string.Empty);
        if (!m.Success)
        {
            values = new Dictionary<string, string>();
            return false;
        }

        values = _regex.GetGroupNames()
            .Where(n => !int.TryParse(n, out _))
            .ToDictionary(n => n, n => m.Groups[n].Value, StringComparer.Ordinal);
        return true;
    }

    [GeneratedRegex(@"^\{(?<name>[A-Za-z]+|\*)\}$")]
    private static partial Regex PlaceholderRegex();
}
