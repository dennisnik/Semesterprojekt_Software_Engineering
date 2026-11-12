using TNWatch.Domain.Battery;
using TNWatch.Domain.Telemetry;

namespace TNWatch.Domain.Fleet;

/// <summary>Consumer of validated telemetry (ADR-07: TelemetryReceived).</summary>
public interface ITelemetryHandler
{
    Task HandleAsync(TelemetryReading reading, CancellationToken ct);
}

/// <summary>Consumer of validated battery lifecycle events.</summary>
public interface IBatteryLifecycleHandler
{
    Task HandleAsync(BatteryEvent batteryEvent, CancellationToken ct);
}

/// <summary>Controls the in-process simulator (dashboard toggle "Simulation | Aus").</summary>
public interface ISimulatorControl
{
    bool IsRunning { get; }

    event Func<bool, Task>? RunningChanged;

    Task SetRunningAsync(bool running);
}
