using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stylebook.Data;

namespace Stylebook.Playground;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static StylebookDbContext Db { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("JabasoftStylebook")
            ?? throw new InvalidOperationException("ConnectionStrings:JabasoftStylebook ontbreekt in appsettings.json.");

        var options = new DbContextOptionsBuilder<StylebookDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        Db = new StylebookDbContext(options);
        Db.Database.Migrate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Db.Dispose();
        base.OnExit(e);
    }
}
