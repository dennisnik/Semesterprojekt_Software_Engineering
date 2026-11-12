namespace TNWatch.Domain.Status;

/// <summary>
/// Rule-based status evaluation (F02). The voltage is smoothed with a moving average over the
/// last <see cref="StatusRules.SmoothingWindow"/> measurements to avoid status flutter.
/// </summary>
public sealed class StatusEvaluator
{
    private readonly StatusRules _rules;

    public StatusEvaluator(StatusRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.WarningMinVoltage > rules.NormalMinVoltage)
        {
            throw new ArgumentException("WarningMinVoltage must not exceed NormalMinVoltage.", nameof(rules));
        }

        if (rules.SmoothingWindow < 1)
        {
            throw new ArgumentException("SmoothingWindow must be at least 1.", nameof(rules));
        }

        _rules = rules;
    }

    public StatusRules Rules => _rules;

    /// <summary>Evaluates the status of a node.</summary>
    /// <param name="recentVoltages">Most recent voltages, newest first. Only the first <c>SmoothingWindow</c> values are used.</param>
    /// <param name="lastContact">Time of the last received measurement, or <c>null</c> if none.</param>
    /// <param name="now">Current time.</param>
    public NodeStatus Evaluate(IReadOnlyList<double> recentVoltages, DateTimeOffset? lastContact, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(recentVoltages);

        if (lastContact is null || recentVoltages.Count == 0)
        {
            return NodeStatus.Unknown;
        }

        if (IsOffline(lastContact.Value, now))
        {
            return NodeStatus.Offline;
        }

        return Classify(SmoothedVoltage(recentVoltages));
    }

    /// <summary>True if the last contact is at least <see cref="StatusRules.OfflineAfter"/> ago.</summary>
    public bool IsOffline(DateTimeOffset lastContact, DateTimeOffset now) => now - lastContact >= _rules.OfflineAfter;

    /// <summary>Moving average of the newest <c>SmoothingWindow</c> voltages (newest first).</summary>
    public double SmoothedVoltage(IReadOnlyList<double> recentVoltages)
    {
        ArgumentNullException.ThrowIfNull(recentVoltages);
        if (recentVoltages.Count == 0)
        {
            throw new ArgumentException("At least one voltage is required.", nameof(recentVoltages));
        }

        var n = Math.Min(_rules.SmoothingWindow, recentVoltages.Count);
        var sum = 0.0;
        for (var i = 0; i < n; i++)
        {
            sum += recentVoltages[i];
        }

        return sum / n;
    }

    /// <summary>Maps a (smoothed) voltage to Normal / Warning / Critical.</summary>
    public NodeStatus Classify(double voltage)
    {
        if (voltage >= _rules.NormalMinVoltage)
        {
            return NodeStatus.Normal;
        }

        return voltage >= _rules.WarningMinVoltage ? NodeStatus.Warning : NodeStatus.Critical;
    }
}
