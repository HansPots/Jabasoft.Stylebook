using Microsoft.EntityFrameworkCore;
using Stylebook.Data.Configurations;
using Stylebook.Data.Entities;

namespace Stylebook.Data;

public sealed class StylebookDbContext(DbContextOptions<StylebookDbContext> options) : DbContext(options)
{
    public DbSet<StylebookComponent> Components => Set<StylebookComponent>();

    public DbSet<DesignToken> DesignTokens => Set<DesignToken>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public DbSet<Application> Applications => Set<Application>();

    public DbSet<Page> Pages => Set<Page>();

    public DbSet<PageRegion> PageRegions => Set<PageRegion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new StylebookComponentConfiguration());
        modelBuilder.ApplyConfiguration(new DesignTokenConfiguration());
        modelBuilder.ApplyConfiguration(new AppSettingConfiguration());
        modelBuilder.ApplyConfiguration(new ApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new PageConfiguration());
        modelBuilder.ApplyConfiguration(new PageRegionConfiguration());
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }
    }
}
