using Microsoft.EntityFrameworkCore;
using Shared.Telemetry.Configurations;

namespace Shared.Telemetry;

/// <summary>
/// The shared "JabaSoftTelemetry" database - deliberately its own database,
/// separate from any single app's own data (TabStudio's song library,
/// LocalAiStudio's analysis results, etc.), so every JabaSoft app can point
/// at the same connection string and log into the same
/// <see cref="TokenUsageEntry"/> table.
/// </summary>
public sealed class TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : DbContext(options)
{
    public DbSet<TokenUsageEntry> TokenUsageEntries => Set<TokenUsageEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TokenUsageEntryConfiguration());
    }
}
