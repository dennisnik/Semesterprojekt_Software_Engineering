using TNWatch.Domain.Diagnostics;

namespace TNWatch.Alerting.Tests;

public class IngestionStatsTests
{
    [Fact]
    public void Empty_stats_have_no_percentiles()
    {
        var s = new IngestionStats().Snapshot();
        Assert.Equal(0, s.Processed);
        Assert.Null(s.P95Ms);
    }

    [Fact]
    public void Computes_percentiles_and_counts()
    {
        var stats = new IngestionStats();
        for (var i = 1; i <= 100; i++)
        {
            stats.RecordProcessed(TimeSpan.FromMilliseconds(i));
        }

        stats.RecordRejected();
        var s = stats.Snapshot();

        Assert.Equal(100, s.Processed);
        Assert.Equal(1, s.Rejected);
        Assert.Equal(50, s.P50Ms);
        Assert.Equal(95, s.P95Ms);
        Assert.Equal(100, s.MaxMs);
    }

    [Fact]
    public void Keeps_only_most_recent_samples()
    {
        var stats = new IngestionStats();
        for (var i = 0; i < IngestionStats.Capacity; i++)
        {
            stats.RecordProcessed(TimeSpan.FromMilliseconds(1000));
        }

        for (var i = 0; i < IngestionStats.Capacity; i++)
        {
            stats.RecordProcessed(TimeSpan.FromMilliseconds(1));
        }

        Assert.Equal(1, stats.Snapshot().MaxMs);
    }
}
