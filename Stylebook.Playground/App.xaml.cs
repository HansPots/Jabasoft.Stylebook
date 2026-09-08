using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stylebook.Data;
using Stylebook.Playground.Ai;
using Stylebook.Playground.Theming;

namespace Stylebook.Playground;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static StylebookDbContext Db { get; private set; } = null!;

    public static AiClient Ai { get; private set; } = null!;

    /// <summary>
    /// The live, DB-backed theme layer merged last into Application.Resources
    /// - later entries win for a shared key, so this overrides the static
    /// Themes/VisualStudio.xaml/Typography.xaml defaults underneath it. Kept
    /// so ReapplyLiveTheme can replace it in place after a Stylebook edit.
    /// </summary>
    private static ResourceDictionary _liveThemeDictionary = null!;

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

        DbThemeBuilder.EnsureSeeded(Db);
        _liveThemeDictionary = DbThemeBuilder.Build(Db);
        Resources.MergedDictionaries.Add(_liveThemeDictionary);

        var aiServerUrl = configuration["AiConnector:ServerUrl"]
            ?? throw new InvalidOperationException("AiConnector:ServerUrl ontbreekt in appsettings.json.");
        var aiModel = configuration["AiConnector:Model"]
            ?? throw new InvalidOperationException("AiConnector:Model ontbreekt in appsettings.json.");

        Ai = new AiClient(aiServerUrl, aiModel);
    }

    /// <summary>Call after saving DesignTokens changes so the edit is visible immediately, everywhere.</summary>
    public static void ReapplyLiveTheme()
    {
        var index = Current.Resources.MergedDictionaries.IndexOf(_liveThemeDictionary);
        _liveThemeDictionary = DbThemeBuilder.Build(Db);

        if (index >= 0)
        {
            Current.Resources.MergedDictionaries[index] = _liveThemeDictionary;
        }
        else
        {
            Current.Resources.MergedDictionaries.Add(_liveThemeDictionary);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Db.Dispose();
        base.OnExit(e);
    }
}
