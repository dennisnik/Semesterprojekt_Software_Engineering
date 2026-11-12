namespace TNWatch.TelemetryIngestion.Simulation;

/// <summary>Configuration section "Simulator".</summary>
public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    /// <summary>Start the simulator automatically on application start.</summary>
    public bool StartEnabled { get; set; }

    /// <summary>Number of simulated nodes. The first 7 use the fixed demo profiles; more are generated randomly (load tests).</summary>
    public int NodeCount { get; set; } = 7;

    /// <summary>Send interval per node.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Tenant used in the simulated topic.</summary>
    public string Tenant { get; set; } = "simulation";

    public int Seed { get; set; } = 4711;
}
