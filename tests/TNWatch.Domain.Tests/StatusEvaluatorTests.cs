using TNWatch.Domain.Status;

namespace TNWatch.Domain.Tests;

public class StatusEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
    private readonly StatusEvaluator _sut = new(new StatusRules());

    [Theory]
    [InlineData(12.4, NodeStatus.Normal)]      // boundary: U >= 12.4 → Normal
    [InlineData(13.1, NodeStatus.Normal)]
    [InlineData(12.399, NodeStatus.Warning)]
    [InlineData(11.8, NodeStatus.Warning)]     // boundary: 11.8 <= U < 12.4 → Warning
    [InlineData(11.799, NodeStatus.Critical)]
    [InlineData(10.4, NodeStatus.Critical)]
    public void Classify_applies_readme_thresholds(double voltage, NodeStatus expected)
        => Assert.Equal(expected, _sut.Classify(voltage));

    [Fact]
    public void Evaluate_is_offline_at_exactly_30_minutes()
        => Assert.Equal(NodeStatus.Offline, _sut.Evaluate([12.9], Now.AddMinutes(-30), Now));

    [Fact]
    public void Evaluate_is_not_offline_just_below_30_minutes()
        => Assert.Equal(NodeStatus.Normal, _sut.Evaluate([12.9], Now.AddMinutes(-29).AddSeconds(-59), Now));

    [Fact]
    public void Evaluate_offline_wins_regardless_of_voltage()
        => Assert.Equal(NodeStatus.Offline, _sut.Evaluate([10.0], Now.AddHours(-2), Now));

    [Fact]
    public void Evaluate_without_measurements_is_unknown()
    {
        Assert.Equal(NodeStatus.Unknown, _sut.Evaluate([], Now, Now));
        Assert.Equal(NodeStatus.Unknown, _sut.Evaluate([12.0], null, Now));
    }

    [Fact]
    public void Evaluate_single_load_spike_does_not_flip_status()
    {
        // Newest first: one short dip to 11.5 V (radio burst) after two healthy values.
        // Mean = (11.5 + 12.6 + 12.6) / 3 = 12.23 → Warning, not Critical.
        Assert.Equal(NodeStatus.Warning, _sut.Evaluate([11.5, 12.6, 12.6], Now, Now));
    }

    [Fact]
    public void Evaluate_uses_only_the_newest_window_values()
    {
        // 4th value (10.0) must be ignored with a window of 3.
        Assert.Equal(NodeStatus.Normal, _sut.Evaluate([12.5, 12.5, 12.5, 10.0], Now, Now));
    }

    [Fact]
    public void SmoothedVoltage_with_fewer_values_than_window_averages_available()
        => Assert.Equal(12.0, _sut.SmoothedVoltage([12.2, 11.8]), 6);

    [Fact]
    public void SmoothedVoltage_rejects_empty_input()
        => Assert.Throws<ArgumentException>(() => _sut.SmoothedVoltage([]));

    [Fact]
    public void Custom_rules_are_respected()
    {
        var sut = new StatusEvaluator(new StatusRules
        {
            NormalMinVoltage = 13.0,
            WarningMinVoltage = 12.0,
            OfflineAfter = TimeSpan.FromMinutes(5),
            SmoothingWindow = 1,
        });

        Assert.Equal(NodeStatus.Warning, sut.Evaluate([12.5, 13.5], Now, Now));
        Assert.Equal(NodeStatus.Offline, sut.Evaluate([13.5], Now.AddMinutes(-5), Now));
    }

    [Fact]
    public void Invalid_rules_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new StatusEvaluator(new StatusRules { NormalMinVoltage = 11, WarningMinVoltage = 12 }));
        Assert.Throws<ArgumentException>(() => new StatusEvaluator(new StatusRules { SmoothingWindow = 0 }));
        Assert.Throws<ArgumentNullException>(() => new StatusEvaluator(null!));
    }
}
