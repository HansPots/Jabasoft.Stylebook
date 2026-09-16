using System.Windows;
using System.Windows.Media;

namespace Stylebook.Components.Theming;

/// <summary>
/// Zoekt de ResourceDictionary waar een instelling van het
/// instellingenscherm in gezet moet worden.
///
/// Waarom niet altijd Application.Resources: de Stylebook-tool zelf houdt
/// bewust één vaste look (Visual Studio donker) en alleen zijn
/// PREVIEW-oppervlak mag meewisselen - zie de toelichting boven
/// Styles/Theme.xaml. Zou een instellingenkaart hier rechtstreeks
/// Application.Resources aanpassen, dan zou het bekijken van die kaart in
/// het Stylebook de hele tool omkleuren.
///
/// Daarom zoeken we van de kaart naar boven het DICHTSTBIJZIJNDE element
/// dat zelf een Styles/Themes/*.xaml in zijn MergedDictionaries heeft. In
/// een echte app (Jabasoft.App) is dat Application.Resources en kleurt dus
/// alles mee; in het Stylebook is dat het previewvlak, en blijft de tool
/// eromheen staan zoals hij staat.
/// </summary>
public static class AppSettingsScope
{
    private const string ThemeFilesPath = "Themes/";

    /// <summary>
    /// De dichtstbijzijnde dictionary boven <paramref name="from"/> met een
    /// eigen thema erin; Application.Resources als er geen enkele is.
    /// </summary>
    public static ResourceDictionary NearestThemed(DependencyObject? from)
    {
        for (var node = from; node is not null; node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement { Resources: { } resources } && HasTheme(resources))
            {
                return resources;
            }
        }

        return Application.Current?.Resources ?? new ResourceDictionary();
    }

    /// <summary>
    /// Het venster waar dit element in zit - het aangrijpingspunt voor
    /// instellingen die via OVERERVING werken in plaats van via een
    /// resource, zoals het lettertype. Een Window en niet zomaar een
    /// FrameworkElement, want FontFamily en FontSize zitten op Control.
    /// </summary>
    public static Window? NearestRoot(DependencyObject? from) =>
        Window.GetWindow(from) ?? Application.Current?.MainWindow;

    private static bool HasTheme(ResourceDictionary dictionary)
    {
        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (merged.Source?.OriginalString.Contains(ThemeFilesPath, StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
        }

        return false;
    }
}
