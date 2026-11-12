using TNWatch.Domain.Fleet;
using TNWatch.Domain.Status;
using TNWatch.Domain.Telemetry;

namespace TNWatch.Domain.Tests;

public class TrafficNodeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);
    private readonly StatusEvaluator _eval = new(new StatusRules());

    private static TelemetryReading Reading(double v, DateTimeOffset at, double? lat = 48.7, double? lon = 9.1) =>
        new("TN-001", "acme", at, at, v, 0.8, lat, lon, null, null, null, TelemetrySource.Mqtt);

    [Fact]
    public void New_node_is_unknown_and_without_soc()
    {
        var node = new TrafficNode("TN-001", null, false, T0);
        Assert.Equal(NodeStatus.Unknown, node.Status);
        Assert.Null(node.ToSnapshot().StateOfChargePercent);
        Assert.Throws<ArgumentException>(() => new TrafficNode(" ", null, false, T0));
    }

    [Fact]
    public void First_reading_sets_snapshot_and_reports_status_change()
    {
        var node = new TrafficNode("TN-001", null, false, T0);

        var change = node.ApplyReading(Reading(12.6, T0), [12.6], _eval, T0);

        Assert.Equal(new StatusChange("TN-001", NodeStatus.Unknown, NodeStatus.Normal, 12.6, T0), change);
        var s = node.ToSnapshot();
        Assert.Equal(NodeStatus.Normal, s.Status);
        Assert.Equal(12.6, s.VoltageV);
        Assert.Equal(0.8, s.CurrentA);
        Assert.Equal(88, s.StateOfChargePercent); // 80 % + (0.10 / 0.12) * 10 %
        Assert.Equal("acme", s.Tenant);
        Assert.Equal(T0, s.LastSeenAt);
        Assert.Equal(T0, node.StatusSince);
    }

    [Fact]
    public void Same_status_produces_no_change()
    {
        var node = new TrafficNode("TN-001", null, false, T0);
        node.ApplyReading(Reading(12.6, T0), [12.6], _eval, T0);

        Assert.Null(node.ApplyReading(Reading(12.5, T0.AddMinutes(1)), [12.5, 12.6], _eval, T0.AddMinutes(1)));
    }

    [Fact]
    public void Smoothed_voltage_drives_status_transition_to_critical()
    {
        var node = new TrafficNode("TN-001", null, false, T0);
        node.ApplyReading(Reading(12.0, T0), [12.0], _eval, T0);

        var change = node.ApplyReading(Reading(11.5, T0.AddMinutes(1)), [11.5, 11.6, 11.7], _eval, T0.AddMinutes(1));

        Assert.Equal(NodeStatus.Warning, change!.From);
        Assert.Equal(NodeStatus.Critical, change.To);
        Assert.Equal(11.6, change.SmoothedVoltageV!.Value, 3);
    }

    [Fact]
    public void Out_of_order_reading_is_ignored_for_snapshot()
    {
        var node = new TrafficNode("TN-001", null, false, T0);
        node.ApplyReading(Reading(12.6, T0.AddMinutes(5)), [12.6], _eval, T0.AddMinutes(5));

        var change = node.ApplyReading(Reading(10.0, T0), [10.0], _eval, T0.AddMinutes(6));

        Assert.Null(change);
        Assert.Equal(12.6, node.LastVoltageV);
        Assert.Equal(T0.AddMinutes(5), node.LastSeenAt);
    }

    [Fact]
    public void Missing_gps_keeps_last_known_position()
    {
        var node = new TrafficNode("TN-001", null, false, T0);
        node.ApplyReading(Reading(12.6, T0), [12.6], _eval, T0);
        node.ApplyReading(Reading(12.6, T0.AddMinutes(1), lat: null, lon: null), [12.6], _eval, T0.AddMinutes(1));

        Assert.Equal(48.7, node.Latitude);
        Assert.Equal(9.1, node.Longitude);
    }

    [Fact]
    public void Stale_node_goes_offline_once()
    {
        var node = new TrafficNode("TN-001", null, false, T0);
        node.ApplyReading(Reading(12.6, T0), [12.6], _eval, T0);

        Assert.Null(node.MarkOfflineIfStale(_eval, T0.AddMinutes(29)));
        var change = node.MarkOfflineIfStale(_eval, T0.AddMinutes(30));
        Assert.Equal(NodeStatus.Offline, change!.To);
        Assert.Null(node.MarkOfflineIfStale(_eval, T0.AddMinutes(31)));
    }

    [Fact]
    public void Node_without_contact_never_goes_offline_via_watchdog()
        => Assert.Null(new TrafficNode("TN-001", null, false, T0).MarkOfflineIfStale(_eval, T0.AddDays(1)));

    [Fact]
    public void Offline_node_recovers_with_new_reading()
    {
        var node = new TrafficNode("TN-001", null, false, T0);
        node.ApplyReading(Reading(12.6, T0), [12.6], _eval, T0);
        node.MarkOfflineIfStale(_eval, T0.AddHours(1));

        var change = node.ApplyReading(Reading(12.6, T0.AddHours(1)), [12.6], _eval, T0.AddHours(1));

        Assert.Equal(NodeStatus.Offline, change!.From);
        Assert.Equal(NodeStatus.Normal, change.To);
    }

    [Fact]
    public void Installing_battery_sets_serial()
    {
        var node = new TrafficNode("TN-001", null, true, T0);
        node.InstallBattery("BAT0001");
        Assert.Equal("BAT0001", node.ToSnapshot().BatterySerial);
        Assert.True(node.ToSnapshot().IsSimulated);
        Assert.Throws<ArgumentException>(() => node.InstallBattery(""));
    }

    [Fact]
    public void Summary_counts_statuses()
    {
        NodeSnapshot S(NodeStatus st) => new("x", null, false, st, null, null, null, null, null, null, null, null);
        var summary = new[] { S(NodeStatus.Normal), S(NodeStatus.Normal), S(NodeStatus.Warning), S(NodeStatus.Critical), S(NodeStatus.Offline), S(NodeStatus.Unknown) }.Summarize();

        Assert.Equal(new FleetSummary(6, 2, 1, 1, 1, 1), summary);
    }
}
