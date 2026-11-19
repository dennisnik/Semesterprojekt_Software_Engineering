using TNWatch.Domain.Battery;
using TNWatch.Domain.Status;
using TNWatch.Domain.Telemetry;

namespace TNWatch.Domain.Fleet;

/// <summary>
/// Aggregate for one TrafficNode (master data + current snapshot). Persisted in <c>core.traffic_nodes</c>.
/// </summary>
public sealed class TrafficNode
{
    public TrafficNode(string id, string? tenant, bool isSimulated, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        Tenant = tenant;
        IsSimulated = isSimulated;
        CreatedAt = createdAt;
        Status = NodeStatus.Unknown;
    }

    public string Id { get; private set; }

    public string? Tenant { get; private set; }

    public bool IsSimulated { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>"Letzter Kontakt": time of the newest measurement.</summary>
    public DateTimeOffset? LastSeenAt { get; private set; }

    public double? LastVoltageV { get; private set; }

    public double? SmoothedVoltageV { get; private set; }

    public double? LastCurrentA { get; private set; }

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public NodeStatus Status { get; private set; }

    public DateTimeOffset? StatusSince { get; private set; }

    public string? BatterySerial { get; private set; }

    /// <summary>
    /// Applies a new reading to the snapshot and re-evaluates the status.
    /// Out-of-order readings (older than <see cref="LastSeenAt"/>) leave the snapshot untouched.
    /// </summary>
    /// <param name="reading">The new reading.</param>
    /// <param name="recentVoltages">Voltages of the newest measurements including <paramref name="reading"/>, newest first.</param>
    /// <param name="evaluator">Status rules.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The status change, if any.</returns>
    public StatusChange? ApplyReading(TelemetryReading reading, IReadOnlyList<double> recentVoltages, StatusEvaluator evaluator, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(evaluator);

        if (LastSeenAt is { } last && reading.MeasuredAt < last)
        {
            return null;
        }

        LastSeenAt = reading.MeasuredAt;
        LastVoltageV = reading.VoltageV;
        LastCurrentA = reading.CurrentA;
        Tenant ??= reading.Tenant;
        if (reading.Latitude is not null && reading.Longitude is not null)
        {
            Latitude = reading.Latitude;
            Longitude = reading.Longitude;
        }

        SmoothedVoltageV = Math.Round(evaluator.SmoothedVoltage(recentVoltages), 3);
        return SetStatus(evaluator.Evaluate(recentVoltages, LastSeenAt, now), now);
    }

    /// <summary>Switches the node to Offline if its last contact is too old (offline watchdog).</summary>
    public StatusChange? MarkOfflineIfStale(StatusEvaluator evaluator, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        if (Status == NodeStatus.Offline || LastSeenAt is null || !evaluator.IsOffline(LastSeenAt.Value, now))
        {
            return null;
        }

        return SetStatus(NodeStatus.Offline, now);
    }

    /// <summary>Records the battery currently installed (from an exchange event).</summary>
    public void InstallBattery(string serial)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        BatterySerial = serial;
    }

    public NodeSnapshot ToSnapshot() => new(
        Id,
        Tenant,
        IsSimulated,
        Status,
        LastVoltageV,
        SmoothedVoltageV,
        LastCurrentA,
        SmoothedVoltageV is { } v ? SocEstimator.Estimate(v) : null,
        Latitude,
        Longitude,
        LastSeenAt,
        BatterySerial);

    private StatusChange? SetStatus(NodeStatus newStatus, DateTimeOffset now)
    {
        if (newStatus == Status)
        {
            return null;
        }

        var change = new StatusChange(Id, Status, newStatus, SmoothedVoltageV, now);
        Status = newStatus;
        StatusSince = now;
        return change;
    }
}
