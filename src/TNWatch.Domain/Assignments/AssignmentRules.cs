namespace TNWatch.Domain.Assignments;

public enum AssignmentOutcome
{
    /// <summary>The change is allowed and will be applied.</summary>
    Ok,

    /// <summary>Nothing to do (e.g. claiming a node one already has).</summary>
    Unchanged,

    /// <summary>Someone else is already responsible; only a Betriebsleiter may take it over.</summary>
    AlreadyAssigned,

    /// <summary>The user lacks the right (e.g. a Techniker assigning someone else).</summary>
    NotAllowed,

    /// <summary>The node (or, for release, the assignment) does not exist.</summary>
    NotFound,
}

/// <summary>
/// Who may claim, assign and release nodes (F11). Pure functions, so the rules are unit-testable:
/// <list type="bullet">
/// <item>Everyone may claim a free node and release their own assignment.</item>
/// <item>A node held by someone else can only be taken over or released by a Betriebsleiter.</item>
/// <item>Only a Betriebsleiter may assign a node to another person.</item>
/// </list>
/// </summary>
public static class AssignmentRules
{
    public static AssignmentOutcome CheckClaim(NodeAssignment? current, UserRef actor, bool actorIsManager)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (current is null)
        {
            return AssignmentOutcome.Ok;
        }

        if (current.Assignee.IsSameUser(actor))
        {
            return AssignmentOutcome.Unchanged;
        }

        return actorIsManager ? AssignmentOutcome.Ok : AssignmentOutcome.AlreadyAssigned;
    }

    public static AssignmentOutcome CheckAssign(NodeAssignment? current, UserRef actor, bool actorIsManager, UserRef target)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(target);

        if (target.IsSameUser(actor))
        {
            return CheckClaim(current, actor, actorIsManager);
        }

        if (!actorIsManager)
        {
            return AssignmentOutcome.NotAllowed;
        }

        return current is not null && current.Assignee.IsSameUser(target) ? AssignmentOutcome.Unchanged : AssignmentOutcome.Ok;
    }

    public static AssignmentOutcome CheckRelease(NodeAssignment? current, UserRef actor, bool actorIsManager)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (current is null)
        {
            return AssignmentOutcome.NotFound;
        }

        return current.Assignee.IsSameUser(actor) || actorIsManager ? AssignmentOutcome.Ok : AssignmentOutcome.NotAllowed;
    }
}
