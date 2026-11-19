using TNWatch.Domain.Battery;

namespace TNWatch.Domain.Tests;

public class SocEstimatorTests
{
    [Theory]
    [InlineData(9.0, 0)]
    [InlineData(10.5, 0)]
    [InlineData(11.51, 10)]
    [InlineData(12.10, 50)]
    [InlineData(12.50, 80)]
    [InlineData(12.73, 100)]
    [InlineData(14.2, 100)]
    [InlineData(double.NaN, 0)]
    public void Estimate_matches_curve_points_and_clamps(double voltage, int expected)
        => Assert.Equal(expected, SocEstimator.Estimate(voltage));

    [Fact]
    public void Estimate_interpolates_between_points()
        => Assert.Equal(85, SocEstimator.Estimate(12.56)); // halfway between 12.50 (80 %) and 12.62 (90 %)

    [Fact]
    public void Estimate_is_monotonic()
    {
        var last = -1;
        for (var v = 10.0; v <= 13.0; v += 0.01)
        {
            var soc = SocEstimator.Estimate(v);
            Assert.True(soc >= last, $"SoC decreased at {v:F2} V");
            last = soc;
        }
    }
}
