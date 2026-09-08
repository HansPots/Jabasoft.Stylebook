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
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Region).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(c => c.CreatedAtUtc).IsRequired();
    }
}
