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

        // Laatste vangnet: RenderXamlPreview vangt een parse-fout al af,
        // maar XAML die WEL parseert kan nog steeds pas tijdens layout
        // (Measure/Arrange, na het parsen) een uitzondering gooien - die
        // zou zonder dit de hele app meenemen. Nooit stil negeren: de
        // gebruiker moet zien dat er iets misging, alleen niet de app
        // erdoor kwijtraken.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

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

    /// <summary>Guards against showing more than one crash dialog at once - see the class comment on OnDispatcherUnhandledException for why that matters here.</summary>
    private static bool _unhandledExceptionDialogPending;

    /// <summary>
    /// NOOIT hier rechtstreeks (synchroon) MessageBox.Show aanroepen: dat
    /// pompt de dispatcher-message-loop opnieuw open TERWIJL de kapotte
    /// operatie nog op de stack staat. Bij een herbruikbare/uitgestelde
    /// WPF-operatie (bv. VirtualizingStackPanel's viewport-initialisatie,
    /// die zichzelf via Dispatcher.BeginInvoke herplant) laat die geneste
    /// message-pump de KAPOTTE operatie meteen opnieuw uitvoeren - die
    /// gooit dezelfde fout, triggert deze handler opnieuw, toont een
    /// NIEUWE geneste MessageBox, enzovoort: een oneindige, geneste lus
    /// die de stack alsnog laat overlopen (dit was letterlijk de oorzaak
    /// van een eerder gerapporteerde crash - de stacktrace toonde
    /// tientallen geneste MessageBox.Show-aanroepen vlak vóór de
    /// StackOverflowException). Dispatcher.BeginInvoke stelt de dialoog
    /// uit tot een vers dispatcher-frame, ná de kapotte operatie, dus
    /// zonder die geneste pomp. _unhandledExceptionDialogPending voorkomt
    /// bovendien een stortvloed dialogen als hetzelfde kapotte element
    /// bij een volgende layout-pas opnieuw faalt.
    /// </summary>
    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        if (_unhandledExceptionDialogPending)
        {
            return;
        }

        _unhandledExceptionDialogPending = true;
        var message = e.Exception.Message;

        Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
        {
            try
            {
                // Vóór de dialoog: het onderbroken layout-pas laat de kapotte
                // subtree (bijna altijd het voorstel - dat is de enige plek
                // waar van-buitenaf-aangeleverde XAML gerenderd wordt) op
                // grootte 0 achter, wat aanvoelt als "de editor is weg"
                // terwijl _proposedXaml en alle Visibility-vlaggen nog gewoon
                // kloppen. Het voorstel verwerpen (zie
                // MainWindow.RecoverFromUnhandledException) haalt de kapotte
                // inhoud weg vóór de gebruiker de melding wegklikt, zodat de
                // app al hersteld is zodra ze "OK" zien.
                (Current.MainWindow as MainWindow)?.RecoverFromUnhandledException();

                MessageBox.Show(
                    $"Er ging iets onverwacht mis, maar de app blijft draaien:\n\n{message}",
                    "Onverwachte fout",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                _unhandledExceptionDialogPending = false;
            }
        }));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Db.Dispose();
        base.OnExit(e);
    }
}
