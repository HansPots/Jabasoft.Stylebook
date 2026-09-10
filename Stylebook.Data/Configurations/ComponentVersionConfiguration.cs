using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stylebook.Data.Entities;

namespace Stylebook.Data.Configurations;

public sealed class ComponentVersionConfiguration : IEntityTypeConfiguration<ComponentVersion>
{
    public void Configure(EntityTypeBuilder<ComponentVersion> builder)
    {
        builder.ToTable("ComponentVersions");
        builder.HasKey(v => v.Id);
        builder.HasIndex(v => new { v.ComponentId, v.CreatedAtUtc });
        builder.HasOne<StylebookComponent>().WithMany().HasForeignKey(v => v.ComponentId).OnDelete(DeleteBehavior.Cascade);

        // Most important fields first, audit timestamps last - see
        // feedback_db_audit_timestamps memory.
        builder.Property(v => v.Id).HasColumnOrder(0);
        builder.Property(v => v.ComponentId).IsRequired().HasColumnOrder(1);
        builder.Property(v => v.Name).IsRequired().HasMaxLength(200).HasColumnOrder(2);
        builder.Property(v => v.Title).HasMaxLength(200).HasColumnOrder(3);
        builder.Property(v => v.BodyText).HasMaxLength(2000).HasColumnOrder(4);
        builder.Property(v => v.Xaml).HasColumnOrder(5);
        builder.Property(v => v.CreatedAtUtc).IsRequired().HasColumnOrder(6);
        builder.Property(v => v.UpdatedAtUtc).IsRequired().HasColumnOrder(7);
    }
}
