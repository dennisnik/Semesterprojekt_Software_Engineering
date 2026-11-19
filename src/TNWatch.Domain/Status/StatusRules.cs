namespace TNWatch.Domain.Status;

/// <summary>
/// Configurable thresholds for the status evaluation (README §7).
/// Bound from configuration section "StatusRules".
/// </summary>
public sealed class StatusRules
{
    public const string SectionName = "StatusRules";

    /// <summary>U ≥ this value → Normal (default 12.4 V).</summary>
    public double NormalMinVoltage { get; set; } = 12.4;

    /// <summary>U ≥ this value (and below <see cref="NormalMinVoltage"/>) → Warning (default 11.8 V).</summary>
    public double WarningMinVoltage { get; set; } = 11.8;

    /// <summary>Last contact older than or equal to this → Offline (default 30 min).</summary>
    public TimeSpan OfflineAfter { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Number of measurements for the moving average (hysteresis, default 3).</summary>
    public int SmoothingWindow { get; set; } = 3;
}
