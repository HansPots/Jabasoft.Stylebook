using Microsoft.EntityFrameworkCore;
using Stylebook.Data.Configurations;
using Stylebook.Data.Entities;

namespace Stylebook.Data;

public sealed class StylebookDbContext(DbContextOptions<StylebookDbContext> options) : DbContext(options)
{
    public DbSet<StylebookComponent> Components => Set<StylebookComponent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new StylebookComponentConfiguration());
    }
}
