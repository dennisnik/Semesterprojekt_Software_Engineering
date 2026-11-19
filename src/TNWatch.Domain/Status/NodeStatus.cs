namespace TNWatch.Domain.Status;

/// <summary>Status classes according to README §7 (Fachliche Statusregeln).</summary>
public enum NodeStatus
{
    /// <summary>No measurement received yet.</summary>
    Unknown = 0,
    Normal = 1,
    Warning = 2,
    Critical = 3,
    Offline = 4,
}
