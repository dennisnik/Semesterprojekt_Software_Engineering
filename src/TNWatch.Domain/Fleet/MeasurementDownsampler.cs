namespace TNWatch.Domain.Fleet;

/// <summary>
/// Reduces a measurement series for charting (e.g. 30 days × 1/min = 43,200 points) by averaging
/// fixed-size buckets. Preserves the time order; the CSV export always uses the raw data.
/// </summary>
public static class MeasurementDownsampler
{
    public static IReadOnlyList<MeasurementPoint> Downsample(IReadOnlyList<MeasurementPoint> points, int maxPoints)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPoints, 1);

        if (points.Count <= maxPoints)
        {
            return points;
        }

        var bucketSize = (int)Math.Ceiling(points.Count / (double)maxPoints);
        var result = new List<MeasurementPoint>(maxPoints);
        for (var start = 0; start < points.Count; start += bucketSize)
        {
            var count = Math.Min(bucketSize, points.Count - start);
            double v = 0, c = 0;
            for (var i = start; i < start + count; i++)
            {
                v += points[i].VoltageV;
                c += points[i].CurrentA;
            }

            var middle = points[start + (count / 2)];
            result.Add(middle with { VoltageV = Math.Round(v / count, 3), CurrentA = Math.Round(c / count, 4) });
        }

        return result;
    }
}
