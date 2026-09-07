using System;
using System.Windows;

namespace Stylebook.Components.Theming;

public enum Theme
{
    Dark,
    Light,
}

/// <summary>
/// Swaps the active color theme at runtime by replacing the
/// Themes/{Dark,Light}.xaml entry directly in
/// Application.Resources.MergedDictionaries. This has to be a top-level
/// entry (see the app-wiring note in Styles/Theme.xaml) - replacing a
/// dictionary nested further down (e.g. inside Theme.xaml itself) does
/// not reliably repaint controls already on screen, even though the
/// swap itself succeeds with no error.
/// Every component reads colors via {DynamicResource ...}, so this one
/// swap is enough - no control needs to be touched or re-created.
/// </summary>
public static class ThemeManager
{
    private const string ThemeFilesPath = "Themes/";
    private const string ThemePackPrefix = "pack://application:,,,/Stylebook.Components;component/Styles/Themes/";

    public static void Apply(Theme theme)
    {
        var app = Application.Current
            ?? throw new InvalidOperationException("No running Application to theme.");

        var uri = new Uri($"{ThemePackPrefix}{theme}.xaml", UriKind.Absolute);
        var replacement = new ResourceDictionary { Source = uri };

        var merged = app.Resources.MergedDictionaries;
        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].Source?.OriginalString.Contains(ThemeFilesPath, StringComparison.OrdinalIgnoreCase) == true)
            {
                merged[i] = replacement;
                return;
            }
        }

        throw new InvalidOperationException(
            "No Styles/Themes/*.xaml entry found directly in Application.Resources.MergedDictionaries - " +
            "merge one (see Styles/Theme.xaml) before calling ThemeManager.Apply.");
    }
}
