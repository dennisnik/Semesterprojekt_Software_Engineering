namespace TNWatch.TelemetryIngestion.Simulation;

/// <summary>Behaviour of a simulated TrafficNode.</summary>
public enum SimulationProfile
{
    /// <summary>Stable around the start voltage.</summary>
    Stable,

    /// <summary>Discharges continuously (Normal → Warning → Critical).</summary>
    Discharging,

    /// <summary>Sends one old message on start, then stays silent → Offline.</summary>
    Silent,
}

/// <summary>State of one simulated node. Voltage evolves per tick; load spikes demonstrate the smoothing (hysteresis).</summary>
public sealed class SimulatedNode(string id, double latitude, double longitude, double startVoltage, SimulationProfile profile)
{
    public string Id { get; } = id;

    public double Latitude { get; } = latitude;

    public double Longitude { get; } = longitude;

    public SimulationProfile Profile { get; } = profile;

    public double BaseVoltage { get; private set; } = startVoltage;

    public bool HasSentInitial { get; set; }

    /// <summary>Voltage at which a discharging node gets a new battery (simulated service visit).</summary>
    public const double ExchangeVoltage = 11.0;

    /// <summary>Voltage of a freshly installed battery.</summary>
    public const double FreshBatteryVoltage = 12.85;

    /// <summary>Serial number of the installed (simulated) battery, e.g. <c>SBAT-004-1</c>.</summary>
    public string BatterySerial => $"SBAT-{Id[^3..]}-{BatteryGeneration}";

    /// <summary>1 for the initially installed battery, incremented with every exchange.</summary>
    public int BatteryGeneration { get; private set; } = 1;

    /// <summary>True once the initial installation event was emitted (in this process).</summary>
    public bool HasAnnouncedBattery { get; set; }

    /// <summary>
    /// A discharging node whose battery reached <see cref="ExchangeVoltage"/> gets a new one.
    /// Returns the replaced serial, or <c>null</c> if no exchange happened.
    /// </summary>
    public string? ExchangeBatteryIfEmpty()
    {
        if (Profile != SimulationProfile.Discharging || BaseVoltage > ExchangeVoltage)
        {
            return null;
        }

        var old = BatterySerial;
        BatteryGeneration++;
        BaseVoltage = FreshBatteryVoltage;
        return old;
    }

    /// <summary>Advances one tick and returns (voltage, current) for this measurement.</summary>
    public (double Voltage, double Current) NextSample(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (Profile == SimulationProfile.Discharging)
        {
            BaseVoltage = Math.Max(ExchangeVoltage, BaseVoltage - 0.004);
        }

#pragma warning disable CA5394 // Random is fine for simulated sensor noise.
        var noise = (random.NextDouble() - 0.5) * 0.06;
        var current = 0.45 + (random.NextDouble() * 0.35);
        var spike = random.NextDouble() < 0.05;
#pragma warning restore CA5394
        if (spike)
        {
            // Radio burst: short voltage drop + current peak, smoothed away by the 3-value moving average.
            return (Math.Round(BaseVoltage - 0.45 + noise, 3), Math.Round(current + 0.6, 3));
        }

        return (Math.Round(BaseVoltage + noise, 3), Math.Round(current, 3));
    }

    /// <summary>Fixed demo fleet around Stuttgart, matching the mockup pin locations.</summary>
    public static IReadOnlyList<SimulatedNode> DemoFleet() =>
    [
        new("SIM-001", 48.8781, 9.0717, 11.45, SimulationProfile.Stable),       // Schwieberdingen – Kritisch
        new("SIM-002", 48.8756, 9.3980, 12.80, SimulationProfile.Stable),       // Winnenden – Normal
        new("SIM-003", 48.7758, 9.1829, 12.10, SimulationProfile.Stable),       // Stuttgart – Warnung
        new("SIM-004", 48.8020, 9.5270, 12.55, SimulationProfile.Discharging),  // Schorndorf – entlädt sich
        new("SIM-005", 48.6857, 9.0115, 11.30, SimulationProfile.Stable),       // Böblingen – Kritisch
        new("SIM-006", 48.6483, 9.4510, 12.90, SimulationProfile.Stable),       // Kirchheim unter Teck – Normal
        new("SIM-007", 48.7406, 9.3108, 12.70, SimulationProfile.Silent),       // Esslingen – Offline
    ];

    /// <summary>Additional random nodes in the Stuttgart region (load tests, NFA01/NFA02).</summary>
    public static SimulatedNode Random(int index, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
#pragma warning disable CA5394
        var lat = 48.55 + (random.NextDouble() * 0.45);
        var lon = 8.85 + (random.NextDouble() * 0.85);
        var voltage = 11.2 + (random.NextDouble() * 1.8);
        var profile = random.NextDouble() < 0.2 ? SimulationProfile.Discharging : SimulationProfile.Stable;
#pragma warning restore CA5394
        return new SimulatedNode($"SIM-{index:000}", Math.Round(lat, 5), Math.Round(lon, 5), voltage, profile);
    }
}
