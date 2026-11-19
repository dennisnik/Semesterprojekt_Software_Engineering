using Microsoft.Extensions.Logging;
using TNWatch.Domain.Assignments;
using TNWatch.Domain.Fleet;

namespace TNWatch.Alerting;

/// <summary>
/// F11 use cases: claim, assign, release, automatic release after a battery exchange.
/// Commands are serialized with a lock, so two people claiming the same node at the same moment cannot
/// both win. This is sufficient because TN-Watch runs as one process (modular monolith, ADR-01).
/// Every change is pushed live to all users via <see cref="IFleetNotifier"/>.
/// </summary>
public sealed class AssignmentService(
    IAssignmentStore store,
    IFleetQueries queries,
    IFleetNotifier notifier,
    TimeProvider time,
    ILogger<AssignmentService> logger) : IAssignmentService, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public Task<IReadOnlyList<NodeAssignment>> GetAllAsync(CancellationToken ct) => store.GetAllAsync(ct);

    public Task<AssignmentResult> ClaimAsync(string nodeId, UserRef actor, bool actorIsManager, CancellationToken ct) =>
        ChangeAsync(nodeId, actor, current => AssignmentRules.CheckClaim(current, actor, actorIsManager), actor, AssignmentReason.Claimed, ct);

    public Task<AssignmentResult> AssignAsync(string nodeId, UserRef target, UserRef actor, bool actorIsManager, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(target);
        var reason = target.IsSameUser(actor) ? AssignmentReason.Claimed : AssignmentReason.Assigned;
        return ChangeAsync(nodeId, actor, current => AssignmentRules.CheckAssign(current, actor, actorIsManager, target), target, reason, ct);
    }

    public Task<AssignmentResult> ReleaseAsync(string nodeId, UserRef actor, bool actorIsManager, CancellationToken ct) =>
        ChangeAsync(nodeId, actor, current => AssignmentRules.CheckRelease(current, actor, actorIsManager), newAssignee: null, AssignmentReason.Released, ct);

    public async Task ReleaseAfterBatteryExchangeAsync(string nodeId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var current = await store.GetAsync(nodeId, ct);
            if (current is null)
            {
                return;
            }

            await store.RemoveAsync(nodeId, ct);
            logger.LogInformation("Assignment of {NodeId} ({User}) released after battery exchange", nodeId, current.Assignee.UserName);
            await notifier.PublishAssignmentChangedAsync(new AssignmentChange(nodeId, null, AssignmentReason.BatteryExchanged, null));
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private async Task<AssignmentResult> ChangeAsync(
        string nodeId,
        UserRef actor,
        Func<NodeAssignment?, AssignmentOutcome> check,
        UserRef? newAssignee,
        AssignmentReason reason,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        ArgumentNullException.ThrowIfNull(actor);

        await _lock.WaitAsync(ct);
        try
        {
            if (await queries.GetNodeAsync(nodeId, ct) is null)
            {
                return new AssignmentResult(AssignmentOutcome.NotFound, null);
            }

            var current = await store.GetAsync(nodeId, ct);
            var outcome = check(current);
            if (outcome != AssignmentOutcome.Ok)
            {
                return new AssignmentResult(outcome, current);
            }

            NodeAssignment? updated = null;
            if (newAssignee is null)
            {
                await store.RemoveAsync(nodeId, ct);
            }
            else
            {
                updated = new NodeAssignment(nodeId, newAssignee, actor, time.GetUtcNow());
                await store.SaveAsync(updated, ct);
            }

            logger.LogInformation("Assignment {NodeId}: {Reason} → {Assignee} (by {Actor})", nodeId, reason, newAssignee?.UserName ?? "–", actor.UserName);
            await notifier.PublishAssignmentChangedAsync(new AssignmentChange(nodeId, updated, reason, actor));
            return new AssignmentResult(AssignmentOutcome.Ok, updated);
        }
        finally
        {
            _lock.Release();
        }
    }
}
