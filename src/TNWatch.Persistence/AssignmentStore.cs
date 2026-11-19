using Microsoft.EntityFrameworkCore;
using TNWatch.Domain.Assignments;
using TNWatch.Persistence.Entities;

namespace TNWatch.Persistence;

/// <summary>EF Core store for node assignments; short-lived context per call (safe as singleton).</summary>
public sealed class AssignmentStore(IDbContextFactory<TnWatchDbContext> factory) : IAssignmentStore
{
    public async Task<IReadOnlyList<NodeAssignment>> GetAllAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.Assignments.AsNoTracking().OrderBy(a => a.NodeId).ToListAsync(ct);
        return rows.Select(r => r.ToDomain()).ToList();
    }

    public async Task<NodeAssignment?> GetAsync(string nodeId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Assignments.AsNoTracking().FirstOrDefaultAsync(a => a.NodeId == nodeId, ct);
        return row?.ToDomain();
    }

    public async Task SaveAsync(NodeAssignment assignment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.Assignments.FirstOrDefaultAsync(a => a.NodeId == assignment.NodeId, ct);
        if (row is null)
        {
            db.Assignments.Add(AssignmentEntity.From(assignment));
        }
        else
        {
            row.Apply(assignment);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(string nodeId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Assignments.Where(a => a.NodeId == nodeId).ExecuteDeleteAsync(ct);
    }
}
