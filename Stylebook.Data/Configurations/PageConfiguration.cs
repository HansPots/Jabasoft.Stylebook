using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stylebook.Data.Entities;

namespace Stylebook.Data.Configurations;

public sealed class PageConfiguration : IEntityTypeConfiguration<Page>
{
    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.ToTable("Pages");
        builder.HasKey(p => p.Id);
        builder.HasIndex(p => new { p.ApplicationId, p.Name }).IsUnique();
        builder.HasOne<Application>().WithMany().HasForeignKey(p => p.ApplicationId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.Id).HasColumnOrder(0);
        builder.Property(p => p.ApplicationId).IsRequired().HasColumnOrder(1);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100).HasColumnOrder(2);
        builder.Property(p => p.CreatedAtUtc).IsRequired().HasColumnOrder(3);
        builder.Property(p => p.UpdatedAtUtc).IsRequired().HasColumnOrder(4);
    }
}
