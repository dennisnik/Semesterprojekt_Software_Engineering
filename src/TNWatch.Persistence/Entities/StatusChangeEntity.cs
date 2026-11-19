using TNWatch.Domain.Fleet;
using TNWatch.Domain.Status;

namespace TNWatch.Persistence.Entities;

/// <summary>Row in <c>alerting.status_changes</c> (F06).</summary>
public sealed class StatusChangeEntity
{
    public long Id { get; set; }

    public required string NodeId { get; set; }

    public NodeStatus FromStatus { get; set; }

    public NodeStatus ToStatus { get; set; }

    public double? SmoothedVoltageV { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public static StatusChangeEntity From(StatusChange c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return new StatusChangeEntity
        {
            NodeId = c.NodeId,
            FromStatus = c.From,
            ToStatus = c.To,
            SmoothedVoltageV = c.SmoothedVoltageV,
            OccurredAt = c.OccurredAt,
        };
    }

    public StatusChange ToDomain() => new(NodeId, FromStatus, ToStatus, SmoothedVoltageV, OccurredAt);
}
