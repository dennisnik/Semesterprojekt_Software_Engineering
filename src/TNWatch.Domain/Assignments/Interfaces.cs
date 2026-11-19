namespace TNWatch.Domain.Assignments;

/// <summary>Persistence of the current assignments (implemented in TNWatch.Persistence).</summary>
public interface IAssignmentStore
{
    Task<IReadOnlyList<NodeAssignment>> GetAllAsync(CancellationToken ct);

    Task<NodeAssignment?> GetAsync(string nodeId, CancellationToken ct);

    /// <summary>Inserts or replaces the assignment of a node.</summary>
    Task SaveAsync(NodeAssignment assignment, CancellationToken ct);

    Task RemoveAsync(string nodeId, CancellationToken ct);
}

/// <summary>Result of an assignment command; <see cref="Current"/> is the assignment after the command.</summary>
public sealed record AssignmentResult(AssignmentOutcome Outcome, NodeAssignment? Current)
{
    public bool Succeeded => Outcome is AssignmentOutcome.Ok or AssignmentOutcome.Unchanged;
}

/// <summary>Use cases of F11, called by UI, REST API and the battery event handler.</summary>
public interface IAssignmentService
{
    Task<IReadOnlyList<NodeAssignment>> GetAllAsync(CancellationToken ct);

    /// <summary>The actor takes over the node.</summary>
    Task<AssignmentResult> ClaimAsync(string nodeId, UserRef actor, bool actorIsManager, CancellationToken ct);

    /// <summary>The actor (normally a Betriebsleiter) assigns the node to <paramref name="target"/>.</summary>
    Task<AssignmentResult> AssignAsync(string nodeId, UserRef target, UserRef actor, bool actorIsManager, CancellationToken ct);

    Task<AssignmentResult> ReleaseAsync(string nodeId, UserRef actor, bool actorIsManager, CancellationToken ct);

    /// <summary>Automatic release after a battery exchange (no permission check, system action).</summary>
    Task ReleaseAfterBatteryExchangeAsync(string nodeId, CancellationToken ct);
}

/// <summary>Users that can be assigned to nodes (Keycloak in production, configuration in development).</summary>
public interface IUserDirectory
{
    Task<IReadOnlyList<UserRef>> GetAssignableUsersAsync(CancellationToken ct);
}
