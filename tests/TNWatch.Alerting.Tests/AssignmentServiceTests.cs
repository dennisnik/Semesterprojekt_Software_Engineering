using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TNWatch.Domain.Assignments;

namespace TNWatch.Alerting.Tests;

public sealed class AssignmentServiceTests : IDisposable
{
    private static readonly UserRef Kevin = new("kevin", "Kevin");
    private static readonly UserRef Anja = new("anja", "Anja");
    private static readonly UserRef Markus = new("markus", "Markus");

    private readonly FakeAssignmentStore _store = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
    private readonly AssignmentService _sut;

    public AssignmentServiceTests() =>
        _sut = new AssignmentService(_store, new FakeFleetQueries("TN0001", "SIM-002"), _notifier, _time, NullLogger<AssignmentService>.Instance);

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task Claim_stores_assignment_and_notifies_everyone()
    {
        var result = await _sut.ClaimAsync("TN0001", Kevin, false, CancellationToken.None);

        Assert.Equal(AssignmentOutcome.Ok, result.Outcome);
        var stored = _store.Items["TN0001"];
        Assert.Equal(Kevin, stored.Assignee);
        Assert.Equal(Kevin, stored.AssignedBy);
        Assert.Equal(_time.GetUtcNow(), stored.AssignedAt);
        var change = Assert.Single(_notifier.AssignmentChanges);
        Assert.Equal(AssignmentReason.Claimed, change.Reason);
        Assert.Equal(stored, change.Assignment);
    }

    [Fact]
    public async Task Second_technician_gets_conflict_and_nothing_changes()
    {
        await _sut.ClaimAsync("TN0001", Kevin, false, CancellationToken.None);

        var result = await _sut.ClaimAsync("TN0001", Anja, false, CancellationToken.None);

        Assert.Equal(AssignmentOutcome.AlreadyAssigned, result.Outcome);
        Assert.Equal(Kevin, result.Current!.Assignee);
        Assert.Single(_notifier.AssignmentChanges);
    }

    [Fact]
    public async Task Concurrent_claims_have_exactly_one_winner()
    {
        var users = Enumerable.Range(0, 20).Select(i => new UserRef($"tech{i}", $"Tech {i}")).ToList();

        var results = await Task.WhenAll(users.Select(u => Task.Run(() => _sut.ClaimAsync("TN0001", u, false, CancellationToken.None))));

        Assert.Single(results, r => r.Outcome == AssignmentOutcome.Ok);
        Assert.Equal(19, results.Count(r => r.Outcome == AssignmentOutcome.AlreadyAssigned));
    }

    [Fact]
    public async Task Manager_assigns_and_reassigns()
    {
        await _sut.ClaimAsync("TN0001", Kevin, false, CancellationToken.None);

        var result = await _sut.AssignAsync("TN0001", Anja, Markus, true, CancellationToken.None);

        Assert.Equal(AssignmentOutcome.Ok, result.Outcome);
        Assert.Equal(Anja, _store.Items["TN0001"].Assignee);
        Assert.Equal(Markus, _store.Items["TN0001"].AssignedBy);
        Assert.Equal(AssignmentReason.Assigned, _notifier.AssignmentChanges[^1].Reason);
        Assert.Equal(Markus, _notifier.AssignmentChanges[^1].Actor);
    }

    [Fact]
    public async Task Technician_cannot_assign_others()
    {
        var result = await _sut.AssignAsync("TN0001", Anja, Kevin, false, CancellationToken.None);
        Assert.Equal(AssignmentOutcome.NotAllowed, result.Outcome);
        Assert.Empty(_store.Items);
    }

    [Fact]
    public async Task Assigning_oneself_counts_as_claim()
    {
        await _sut.AssignAsync("TN0001", Kevin, Kevin, false, CancellationToken.None);
        Assert.Equal(AssignmentReason.Claimed, Assert.Single(_notifier.AssignmentChanges).Reason);
    }

    [Fact]
    public async Task Release_by_owner_and_refusal_for_others()
    {
        await _sut.ClaimAsync("TN0001", Kevin, false, CancellationToken.None);

        Assert.Equal(AssignmentOutcome.NotAllowed, (await _sut.ReleaseAsync("TN0001", Anja, false, CancellationToken.None)).Outcome);
        Assert.Equal(AssignmentOutcome.Ok, (await _sut.ReleaseAsync("TN0001", Kevin, false, CancellationToken.None)).Outcome);

        Assert.Empty(_store.Items);
        var change = _notifier.AssignmentChanges[^1];
        Assert.Null(change.Assignment);
        Assert.Equal(AssignmentReason.Released, change.Reason);
        Assert.Equal(AssignmentOutcome.NotFound, (await _sut.ReleaseAsync("TN0001", Kevin, false, CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task Unknown_node_is_not_found()
    {
        Assert.Equal(AssignmentOutcome.NotFound, (await _sut.ClaimAsync("NOPE", Kevin, false, CancellationToken.None)).Outcome);
        Assert.Empty(_store.Items);
    }

    [Fact]
    public async Task Battery_exchange_releases_assignment_once()
    {
        await _sut.ClaimAsync("TN0001", Kevin, false, CancellationToken.None);

        await _sut.ReleaseAfterBatteryExchangeAsync("TN0001", CancellationToken.None);
        await _sut.ReleaseAfterBatteryExchangeAsync("TN0001", CancellationToken.None); // nothing assigned anymore → no-op
        await _sut.ReleaseAfterBatteryExchangeAsync("SIM-002", CancellationToken.None);

        Assert.Empty(_store.Items);
        Assert.Equal([AssignmentReason.Claimed, AssignmentReason.BatteryExchanged], _notifier.AssignmentChanges.Select(c => c.Reason));
    }

    [Fact]
    public async Task GetAll_returns_store_content()
    {
        await _sut.ClaimAsync("TN0001", Kevin, false, CancellationToken.None);
        await _sut.ClaimAsync("SIM-002", Anja, false, CancellationToken.None);
        Assert.Equal(2, (await _sut.GetAllAsync(CancellationToken.None)).Count);
    }
}
