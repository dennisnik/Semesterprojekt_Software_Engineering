using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TNWatch.Domain.Fleet;
using TNWatch.Persistence.Entities;

namespace TNWatch.Persistence;

/// <summary>EF Core context. One database, logical schemas per module (ADR-09): core, telemetry, alerting.</summary>
public sealed class TnWatchDbContext(DbContextOptions<TnWatchDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<TrafficNode> Nodes => Set<TrafficNode>();

    public DbSet<MeasurementEntity> Measurements => Set<MeasurementEntity>();

    public DbSet<StatusChangeEntity> StatusChanges => Set<StatusChangeEntity>();

    public DbSet<BatteryEventEntity> BatteryEvents => Set<BatteryEventEntity>();

    /// <summary>ASP.NET Core Data Protection keys (auth cookies survive container restarts).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Npgsql only accepts UTC offsets for timestamptz → normalize every DateTimeOffset.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<TrafficNode>(e =>
        {
            e.ToTable("traffic_nodes", "core");
            e.HasKey(n => n.Id);
            e.Property(n => n.Id).HasMaxLength(64);
            e.Property(n => n.Tenant).HasMaxLength(128);
            e.Property(n => n.BatterySerial).HasMaxLength(64);
            e.Property(n => n.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(n => n.Status);
        });

        modelBuilder.Entity<MeasurementEntity>(e =>
        {
            e.ToTable("measurements", "telemetry");
            e.HasKey(m => m.Id);
            e.Property(m => m.NodeId).HasMaxLength(64);
            e.Property(m => m.Source).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(m => new { m.NodeId, m.MeasuredAt }).IsDescending(false, true);
            e.HasOne<TrafficNode>().WithMany().HasForeignKey(m => m.NodeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StatusChangeEntity>(e =>
        {
            e.ToTable("status_changes", "alerting");
            e.HasKey(c => c.Id);
            e.Property(c => c.NodeId).HasMaxLength(64);
            e.Property(c => c.FromStatus).HasConversion<string>().HasMaxLength(16);
            e.Property(c => c.ToStatus).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(c => new { c.NodeId, c.OccurredAt }).IsDescending(false, true);
            e.HasOne<TrafficNode>().WithMany().HasForeignKey(c => c.NodeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DataProtectionKey>(e => e.ToTable("data_protection_keys", "core"));

        modelBuilder.Entity<BatteryEventEntity>(e =>
        {
            e.ToTable("battery_events", "core");
            e.HasKey(b => b.EventId);
            e.Property(b => b.Type).HasConversion<string>().HasMaxLength(16);
            e.Property(b => b.NodeId).HasMaxLength(64);
            e.Property(b => b.NewBatterySerial).HasMaxLength(64);
            e.Property(b => b.OldBatterySerial).HasMaxLength(64);
            e.HasIndex(b => new { b.NodeId, b.OccurredAt });
        });
    }

    private sealed class UtcConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(v => v.ToUniversalTime(), v => v);
}
