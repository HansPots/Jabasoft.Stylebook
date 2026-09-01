using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Telemetry.Configurations;

internal sealed class TokenUsageEntryConfiguration : IEntityTypeConfiguration<TokenUsageEntry>
{
    public void Configure(EntityTypeBuilder<TokenUsageEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Application).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Model).HasMaxLength(200);

        // Charting queries filter/group by app + time range.
        builder.HasIndex(e => new { e.Application, e.Timestamp });
    }
}
