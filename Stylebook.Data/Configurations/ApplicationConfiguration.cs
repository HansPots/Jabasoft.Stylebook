using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stylebook.Data.Entities;

namespace Stylebook.Data.Configurations;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.Name).IsUnique();

        builder.Property(a => a.Id).HasColumnOrder(0);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(100).HasColumnOrder(1);
        builder.Property(a => a.CreatedAtUtc).IsRequired().HasColumnOrder(2);
        builder.Property(a => a.UpdatedAtUtc).IsRequired().HasColumnOrder(3);
    }
}
