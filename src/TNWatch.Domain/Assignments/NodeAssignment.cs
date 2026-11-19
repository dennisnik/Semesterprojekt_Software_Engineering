namespace TNWatch.Domain.Assignments;

/// <summary>A user as known from the identity provider (Keycloak <c>preferred_username</c> + display name).</summary>
public sealed record UserRef(string UserName, string DisplayName)
{
    /// <summary>Up to two initials for the map badge, e.g. "Kevin" → "K", "Anja Müller" → "AM".</summary>
    public string Initials
    {
        get
        {
            var parts = DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                return UserName.Length > 0 ? UserName[..1].ToUpperInvariant() : "?";
            }

            return string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        }
    }

    public bool IsSameUser(UserRef other) =>
        other is not null && string.Equals(UserName, other.UserName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Who is currently responsible for a node (F11). At most one assignee per node.</summary>
public sealed record NodeAssignment(string NodeId, UserRef Assignee, UserRef AssignedBy, DateTimeOffset AssignedAt)
{
    /// <summary>True if the assignee claimed the node themselves (as opposed to being assigned by a manager).</summary>
    public bool IsSelfClaimed => Assignee.IsSameUser(AssignedBy);
}

/// <summary>Why an assignment ended or changed (shown to users and written to the log).</summary>
public enum AssignmentReason
{
    Claimed,
    Assigned,
    Released,
    BatteryExchanged,
}

/// <summary>Live-update event: <paramref name="Assignment"/> is <c>null</c> when the node was released.</summary>
public sealed record AssignmentChange(string NodeId, NodeAssignment? Assignment, AssignmentReason Reason, UserRef? Actor);
