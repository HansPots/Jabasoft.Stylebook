using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stylebook.Data.Entities;

namespace Stylebook.Data.Configurations;

public sealed class StylebookComponentConfiguration : IEntityTypeConfiguration<StylebookComponent>
{
    public void Configure(EntityTypeBuilder<StylebookComponent> builder)
    {
        builder.ToTable("Components");
        builder.HasKey(c => c.Id);

        // Most important fields first, audit timestamps last - see
        // feedback_db_audit_timestamps memory. HasColumnOrder makes this
        // an actual physical column order, not just C# declaration order.
        builder.Property(c => c.Id).HasColumnOrder(0);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200).HasColumnOrder(1);
        builder.Property(c => c.Region).HasConversion<string>().HasMaxLength(50).IsRequired().HasColumnOrder(2);
        builder.Property(c => c.Title).HasMaxLength(200).HasColumnOrder(3);
        builder.Property(c => c.BodyText).HasMaxLength(2000).HasColumnOrder(4);
        builder.Property(c => c.Xaml).HasColumnOrder(5);
        builder.Property(c => c.TestContainerWidthMode).HasConversion<string>().HasMaxLength(20).IsRequired().HasColumnOrder(6);
        builder.Property(c => c.TestContainerHeightMode).HasConversion<string>().HasMaxLength(20).IsRequired().HasColumnOrder(7);
        builder.Property(c => c.TestContainerWidth).IsRequired().HasColumnOrder(8);
        builder.Property(c => c.TestContainerHeight).IsRequired().HasColumnOrder(9);
        builder.Property(c => c.FixedWidth).HasColumnOrder(10);
        builder.Property(c => c.FixedHeight).HasColumnOrder(11);
        builder.Property(c => c.CreatedAtUtc).IsRequired().HasColumnOrder(12);
        builder.Property(c => c.UpdatedAtUtc).IsRequired().HasColumnOrder(13);
    }
}
