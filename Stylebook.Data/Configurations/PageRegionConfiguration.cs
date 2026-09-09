using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stylebook.Data.Entities;

namespace Stylebook.Data.Configurations;

public sealed class PageRegionConfiguration : IEntityTypeConfiguration<PageRegion>
{
    public void Configure(EntityTypeBuilder<PageRegion> builder)
    {
        builder.ToTable("PageRegions");
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => new { r.PageId, r.Region }).IsUnique();
        builder.HasOne<Page>().WithMany().HasForeignKey(r => r.PageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<StylebookComponent>().WithMany().HasForeignKey(r => r.ComponentId).OnDelete(DeleteBehavior.SetNull);

        builder.Property(r => r.Id).HasColumnOrder(0);
        builder.Property(r => r.PageId).IsRequired().HasColumnOrder(1);
        builder.Property(r => r.Region).HasConversion<string>().HasMaxLength(50).IsRequired().HasColumnOrder(2);
        builder.Property(r => r.ComponentId).HasColumnOrder(3);
        builder.Property(r => r.CreatedAtUtc).IsRequired().HasColumnOrder(4);
        builder.Property(r => r.UpdatedAtUtc).IsRequired().HasColumnOrder(5);
    }
}
