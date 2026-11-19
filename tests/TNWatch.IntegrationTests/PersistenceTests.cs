using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TNWatch.Alerting;
using TNWatch.Domain.Assignments;
using TNWatch.Domain.Battery;
using TNWatch.Domain.Diagnostics;
using TNWatch.Domain.Fleet;
using TNWatch.Domain.Status;
using TNWatch.Domain.Telemetry;
using TNWatch.Persistence;

namespace TNWatch.IntegrationTests;

[Collection(PostgresGroup.Name)]
public sealed class PersistenceTests(PostgresFixture db) : IAsyncLifetime
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));
    private readonly StatusEvaluator _evaluator = new(new StatusRules());

    public Task InitializeAsync() => db.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private TelemetryReading Reading(string id, double v, TelemetrySource source = TelemetrySource.Mqtt, DateTimeOffset? at = null)
    {
        var t = at ?? _time.GetUtcNow();
        return new TelemetryReading(id, "acme", t, t, v, 0.5, 48.7, 9.1, 280, 12, 8, source);
    }

    private async Task ProcessAsync(TelemetryReading reading)
    {
        await using var provider = db.CreateServices();
        await using var scope = provider.CreateAsyncScope();
        var processor = new TelemetryProcessor(
            scope.ServiceProvider.GetRequiredService<ITelemetryRepository>(), _evaluator, new FleetNotifier(),
            new IngestionStats(), _time, NullLogger<TelemetryProcessor>.Instance);
        await processor.HandleAsync(reading, CancellationToken.None);
    }

    private static IFleetQueries Queries(ServiceProvider provider) => provider.GetRequiredService<IFleetQueries>();

    private AssignmentService Assignments(ServiceProvider provider) =>
        new(provider.GetRequiredService<IAssignmentStore>(), Queries(provider), new FleetNotifier(), _time, NullLogger<AssignmentService>.Instance);

    [Fact]
    public async Task Telemetry_is_persisted_with_snapshot_and_status_history()
    {
        await ProcessAsync(Reading("TSN-002", 12.6));
        _time.Advance(TimeSpan.FromMinutes(3));
        await ProcessAsync(Reading("TSN-002", 11.0));
        _time.Advance(TimeSpan.FromMinutes(3));
        await ProcessAsync(Reading("TSN-002", 11.0)); // Ø(11.0, 11.0, 12.6) = 11.53 → Critical

        await using var provider = db.CreateServices();
        var node = await Queries(provider).GetNodeAsync("TSN-002", CancellationToken.None);
        Assert.NotNull(node);
        Assert.Equal(NodeStatus.Critical, node.Status);
        Assert.Equal(11.533, node.SmoothedVoltageV!.Value, 3);
        Assert.Equal(_time.GetUtcNow(), node.LastSeenAt);

        var measurements = await Queries(provider).GetMeasurementsAsync("TSN-002", _time.GetUtcNow().AddHours(-1), _time.GetUtcNow(), CancellationToken.None);
        Assert.Equal([12.6, 11.0, 11.0], measurements.Select(m => m.VoltageV));

        var changes = await Queries(provider).GetStatusChangesAsync("TSN-002", 10, CancellationToken.None);
        Assert.Equal([NodeStatus.Critical, NodeStatus.Warning, NodeStatus.Normal], changes.Select(c => c.To)); // newest first
    }

    [Fact]
    public async Task Simulated_nodes_are_filtered_and_summarized()
    {
        await ProcessAsync(Reading("TN-REAL", 12.6));
        await ProcessAsync(Reading("SIM-001", 11.2, TelemetrySource.Simulator));

        await using var provider = db.CreateServices();
        var withoutSim = await Queries(provider).GetNodesAsync(includeSimulated: false, CancellationToken.None);
        var withSim = await Queries(provider).GetSummaryAsync(includeSimulated: true, CancellationToken.None);

        Assert.Equal("TN-REAL", Assert.Single(withoutSim).Id);
        Assert.Equal(new FleetSummary(2, 1, 0, 1, 0, 0), withSim);
    }

    [Fact]
    public async Task Battery_events_with_local_offset_are_stored_as_utc_and_idempotent()
    {
        await ProcessAsync(Reading("TN0001", 12.6));
        await using (var setup = db.CreateServices())
        {
            await Assignments(setup).ClaimAsync("TN0001", new UserRef("kevin", "Kevin"), false, CancellationToken.None);
        }

        var occurred = new DateTimeOffset(2026, 11, 5, 11, 30, 0, TimeSpan.FromHours(1));
        var exchange = new BatteryEvent(Guid.NewGuid(), BatteryEventType.Exchange, occurred, "TN0001", "BAT0001", "BAT0002", 52.52, 13.405, 8);

        for (var i = 0; i < 2; i++)
        {
            await using var provider = db.CreateServices();
            await using var scope = provider.CreateAsyncScope();
            await new BatteryEventProcessor(scope.ServiceProvider.GetRequiredService<ITelemetryRepository>(), new FleetNotifier(), Assignments(provider), NullLogger<BatteryEventProcessor>.Instance)
                .HandleAsync(exchange, CancellationToken.None);
        }

        await using var p = db.CreateServices();
        var events = await Queries(p).GetBatteryEventsAsync("TN0001", 10, CancellationToken.None);
        var stored = Assert.Single(events);
        Assert.Equal(occurred.ToUniversalTime(), stored.OccurredAt);
        Assert.Equal("BAT0001", (await Queries(p).GetNodeAsync("TN0001", CancellationToken.None))!.BatterySerial);
        Assert.Empty(await Assignments(p).GetAllAsync(CancellationToken.None)); // F11: released after the exchange
    }

    [Fact]
    public async Task Assignments_are_persisted_updated_and_removed_with_their_node()
    {
        await ProcessAsync(Reading("TN0001", 12.6));
        await ProcessAsync(Reading("TN0002", 12.6));
        var kevin = new UserRef("kevin", "Kevin");
        var markus = new UserRef("markus", "Markus");

        await using (var provider = db.CreateServices())
        {
            var service = Assignments(provider);
            Assert.Equal(AssignmentOutcome.Ok, (await service.ClaimAsync("TN0001", kevin, false, CancellationToken.None)).Outcome);
            Assert.Equal(AssignmentOutcome.Ok, (await service.AssignAsync("TN0002", kevin, markus, true, CancellationToken.None)).Outcome);
            Assert.Equal(AssignmentOutcome.Ok, (await service.AssignAsync("TN0002", new UserRef("anja", "Anja"), markus, true, CancellationToken.None)).Outcome);
        }

        await using var p = db.CreateServices();
        var store = p.GetRequiredService<IAssignmentStore>();
        var all = await store.GetAllAsync(CancellationToken.None);
        Assert.Equal(["TN0001", "TN0002"], all.Select(a => a.NodeId));
        var second = await store.GetAsync("TN0002", CancellationToken.None);
        Assert.Equal("anja", second!.Assignee.UserName);
        Assert.Equal("Markus", second.AssignedBy.DisplayName);
        Assert.Equal(_time.GetUtcNow(), second.AssignedAt);

        // Deleting a node (e.g. clean-up of simulated nodes) removes its assignment (FK cascade).
        var factory = p.GetRequiredService<IDbContextFactory<TnWatchDbContext>>();
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            await ctx.Nodes.Where(n => n.Id == "TN0002").ExecuteDeleteAsync();
        }

        Assert.Equal(["TN0001"], (await store.GetAllAsync(CancellationToken.None)).Select(a => a.NodeId));
        await store.RemoveAsync("TN0001", CancellationToken.None);
        Assert.Null(await store.GetAsync("TN0001", CancellationToken.None));
    }

    [Fact]
    public async Task Repository_returns_recent_voltages_and_stale_nodes()
    {
        await ProcessAsync(Reading("OLD", 12.6)); // fresh at first …
        _time.Advance(TimeSpan.FromMinutes(40));   // … then silent for 40 min
        for (var i = 0; i < 5; i++)
        {
            await ProcessAsync(Reading("FRESH", 12.0 + (i / 10.0), at: _time.GetUtcNow().AddMinutes(i)));
        }

        await using var provider = db.CreateServices();
        await using var scope = provider.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITelemetryRepository>();

        Assert.Equal([12.4, 12.3], await repo.GetRecentVoltagesAsync("FRESH", 2, CancellationToken.None));
        Assert.Empty(await repo.GetRecentVoltagesAsync("FRESH", 0, CancellationToken.None));
        var stale = await repo.GetStaleNodesAsync(_time.GetUtcNow().AddMinutes(-30), CancellationToken.None);
        Assert.Equal("OLD", Assert.Single(stale).Id);
        Assert.True(await repo.BatteryEventExistsAsync(Guid.NewGuid(), CancellationToken.None) is false);
    }

    [Fact]
    public async Task Schemas_follow_adr_09()
    {
        await using var provider = db.CreateServices();
        var factory = provider.GetRequiredService<IDbContextFactory<TnWatchDbContext>>();
        await using var ctx = await factory.CreateDbContextAsync();
        var tables = await ctx.Database
            .SqlQueryRaw<string>("SELECT table_schema || '.' || table_name AS \"Value\" FROM information_schema.tables WHERE table_schema IN ('core','telemetry','alerting') ORDER BY 1")
            .ToListAsync();

        Assert.Equal(["alerting.status_changes", "core.battery_events", "core.data_protection_keys", "core.node_assignments", "core.traffic_nodes", "telemetry.measurements"], tables);
    }
}
