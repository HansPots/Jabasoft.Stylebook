using Microsoft.EntityFrameworkCore;
using Shared.Telemetry.Configurations;

namespace Shared.Telemetry;

/// <summary>
/// The shared "JabasoftBase" database - deliberately its own database,
/// separate from any single app's own data (Stylebook's components,
/// TabStudio's song library, ...), so every JabaSoft app and the Broker
/// can point at the same connection string and log into the same
/// <see cref="TokenUsageEntry"/> table.
/// </summary>
public sealed class TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : DbContext(options)
{
    public DbSet<TokenUsageEntry> TokenUsageEntries => Set<TokenUsageEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TokenUsageEntryConfiguration());
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }
}
