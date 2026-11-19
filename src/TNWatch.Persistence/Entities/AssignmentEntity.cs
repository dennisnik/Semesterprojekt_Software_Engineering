using TNWatch.Domain.Assignments;

namespace TNWatch.Persistence.Entities;

/// <summary>Row in <c>core.node_assignments</c> (F11). The node ID is the primary key → one assignee per node.</summary>
public sealed class AssignmentEntity
{
    public required string NodeId { get; set; }

    public required string AssigneeUserName { get; set; }

    public required string AssigneeDisplayName { get; set; }

    public required string AssignedByUserName { get; set; }

    public required string AssignedByDisplayName { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    public NodeAssignment ToDomain() => new(
        NodeId,
        new UserRef(AssigneeUserName, AssigneeDisplayName),
        new UserRef(AssignedByUserName, AssignedByDisplayName),
        AssignedAt);

    public void Apply(NodeAssignment a)
    {
        ArgumentNullException.ThrowIfNull(a);
        AssigneeUserName = a.Assignee.UserName;
        AssigneeDisplayName = a.Assignee.DisplayName;
        AssignedByUserName = a.AssignedBy.UserName;
        AssignedByDisplayName = a.AssignedBy.DisplayName;
        AssignedAt = a.AssignedAt;
    }

    public static AssignmentEntity From(NodeAssignment a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new AssignmentEntity
        {
            NodeId = a.NodeId,
            AssigneeUserName = a.Assignee.UserName,
            AssigneeDisplayName = a.Assignee.DisplayName,
            AssignedByUserName = a.AssignedBy.UserName,
            AssignedByDisplayName = a.AssignedBy.DisplayName,
            AssignedAt = a.AssignedAt,
        };
    }
}
