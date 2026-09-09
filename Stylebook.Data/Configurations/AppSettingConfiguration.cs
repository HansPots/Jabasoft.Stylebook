using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Stylebook.Data.Entities;

namespace Stylebook.Data.Configurations;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnOrder(0);
        builder.Property(s => s.CurrentTheme).HasConversion<string>().HasMaxLength(50).IsRequired().HasColumnOrder(1);
        builder.Property(s => s.CreatedAtUtc).IsRequired().HasColumnOrder(2);
        builder.Property(s => s.UpdatedAtUtc).IsRequired().HasColumnOrder(3);
    }
}
