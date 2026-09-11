using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shared.Telemetry.Configurations;

internal sealed class TokenUsageEntryConfiguration : IEntityTypeConfiguration<TokenUsageEntry>
{
    public void Configure(EntityTypeBuilder<TokenUsageEntry> builder)
    {
        builder.ToTable("TokenUsageEntries");
        builder.HasKey(e => e.Id);

        // Most important fields first, audit timestamps last - see
        // feedback_db_column_order memory.
        builder.Property(e => e.Id).HasColumnOrder(0);
        builder.Property(e => e.Application).IsRequired().HasMaxLength(100).HasColumnOrder(1);
        builder.Property(e => e.Model).HasMaxLength(200).HasColumnOrder(2);
        builder.Property(e => e.PromptTokens).HasColumnOrder(3);
        builder.Property(e => e.CompletionTokens).HasColumnOrder(4);
        builder.Property(e => e.TotalTokens).HasColumnOrder(5);
        builder.Property(e => e.CreatedAtUtc).IsRequired().HasColumnOrder(6);
        builder.Property(e => e.UpdatedAtUtc).IsRequired().HasColumnOrder(7);

        // Charting queries filter/group by app + time range.
        builder.HasIndex(e => new { e.Application, e.CreatedAtUtc });
    }
}
