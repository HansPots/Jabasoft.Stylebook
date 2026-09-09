using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stylebook.Data.Entities;

namespace Stylebook.Data.Configurations;

public sealed class DesignTokenConfiguration : IEntityTypeConfiguration<DesignToken>
{
    public void Configure(EntityTypeBuilder<DesignToken> builder)
    {
        builder.ToTable("DesignTokens");
        builder.HasKey(t => t.Id);
        // Unique per theme, not globally - the same token Name now has one row per Theme.
        builder.HasIndex(t => new { t.Theme, t.Name }).IsUnique();

        // Most important fields first, audit timestamps last - see
        // feedback_db_column_order memory.
        builder.Property(t => t.Id).HasColumnOrder(0);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100).HasColumnOrder(1);
        builder.Property(t => t.Theme).HasConversion<string>().HasMaxLength(50).IsRequired().HasColumnOrder(2);
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(50).IsRequired().HasColumnOrder(3);
        builder.Property(t => t.Value).IsRequired().HasMaxLength(200).HasColumnOrder(4);
        builder.Property(t => t.DefaultValue).HasMaxLength(200).HasColumnOrder(5);
        builder.Property(t => t.CreatedAtUtc).IsRequired().HasColumnOrder(6);
        builder.Property(t => t.UpdatedAtUtc).IsRequired().HasColumnOrder(7);
    }
}
