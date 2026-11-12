namespace TNWatch.Domain.Diagnostics;

public sealed record LatencySnapshot(long Processed, long Rejected, double? P50Ms, double? P95Ms, double? MaxMs);

/// <summary>
/// Rolling latency statistics (broker receipt → DB commit) for NFA01 verification.
/// Keeps the most recent <see cref="Capacity"/> samples.
/// </summary>
public sealed class IngestionStats
{
    public const int Capacity = 2048;

    private readonly double[] _samples = new double[Capacity];
    private readonly object _lock = new();
    private int _next;
    private int _count;
    private long _processed;
    private long _rejected;

    public void RecordProcessed(TimeSpan latency)
    {
        lock (_lock)
        {
            _samples[_next] = latency.TotalMilliseconds;
            _next = (_next + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);
            _processed++;
        }
    }

    public void RecordRejected()
    {
        lock (_lock)
        {
            _rejected++;
        }
    }

    public LatencySnapshot Snapshot()
    {
        double[] copy;
        long processed, rejected;
        lock (_lock)
        {
            copy = _samples.Take(_count).ToArray();
            processed = _processed;
            rejected = _rejected;
        }

        if (copy.Length == 0)
        {
            return new LatencySnapshot(processed, rejected, null, null, null);
        }

        Array.Sort(copy);
        return new LatencySnapshot(processed, rejected, Percentile(copy, 0.50), Percentile(copy, 0.95), copy[^1]);
    }

    private static double Percentile(double[] sorted, double p)
    {
        var index = (int)Math.Ceiling(p * sorted.Length) - 1;
        return Math.Round(sorted[Math.Clamp(index, 0, sorted.Length - 1)], 1);
    }
}
