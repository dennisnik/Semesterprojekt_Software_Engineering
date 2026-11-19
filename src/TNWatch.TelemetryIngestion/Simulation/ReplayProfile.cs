using System.Globalization;

namespace TNWatch.TelemetryIngestion.Simulation;

/// <summary>One real measurement of a replay profile.</summary>
public readonly record struct ReplaySample(double VoltageV, double CurrentA);

/// <summary>
/// Real measurement series embedded in the assembly (Simulation/Profiles/*.csv), generated from
/// data/traffic_devices_signals.csv by tools/replay-profile/make_replay_profile.py.
/// </summary>
public static class ReplayProfile
{
    private static readonly Lazy<IReadOnlyList<ReplaySample>> Tsn002Lazy = new(() => Load("tsn-002.csv"));

    /// <summary>Node TSN-002, 19.–24.03.2026: 2,229 samples every 3 min, slow discharge 11.3 V → 10.7 V.</summary>
    public static IReadOnlyList<ReplaySample> Tsn002 => Tsn002Lazy.Value;

    public static IReadOnlyList<ReplaySample> Load(string fileName)
    {
        var resource = $"TNWatch.Simulation.Profiles.{fileName}";
        using var stream = typeof(ReplayProfile).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded replay profile '{resource}' not found.");
        using var reader = new StreamReader(stream);
        return Parse(reader);
    }

    /// <summary>Parses "voltage_v,current_a" lines; '#' comments and the header are skipped.</summary>
    public static IReadOnlyList<ReplaySample> Parse(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var samples = new List<ReplaySample>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("voltage", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split(',');
            samples.Add(new ReplaySample(
                double.Parse(parts[0], CultureInfo.InvariantCulture),
                double.Parse(parts[1], CultureInfo.InvariantCulture)));
        }

        return samples.Count > 0 ? samples : throw new InvalidOperationException("Replay profile is empty.");
    }
}
