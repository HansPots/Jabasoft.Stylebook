using System;
using System.Windows;

namespace Stylebook.Components.Theming;

public enum Theme
{
    Lcars,
    VisualStudio,
}

/// <summary>
/// Swaps the active color theme at runtime by replacing the
/// Themes/{Lcars,VisualStudio}.xaml entry directly in the given
/// ResourceDictionary's own MergedDictionaries - deliberately NOT always
/// Application.Resources. The Stylebook tool's own chrome (menu strip,
/// dropdown, ...) has one fixed Visual Studio dark look and stays out of
/// theme switching entirely - only a preview surface (e.g. the Grid that
/// hosts a Basis demo) merges a swappable Themes/*.xaml and passes its
/// own Resources here. Application.Resources is a valid target too, for
/// an app that wants its whole UI themed - just pass it explicitly.
///
/// The swap has to happen at that dictionary's own top level - replacing
/// one nested further down (e.g. inside Theme.xaml itself) does not
/// reliably repaint controls already on screen, even though the swap
/// itself succeeds with no error.
/// </summary>
public static class ThemeManager
{
    private const string ThemeFilesPath = "Themes/";
    private const string ThemePackPrefix = "pack://application:,,,/Stylebook.Components;component/Styles/Themes/";

    public static void Apply(Theme theme, ResourceDictionary target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var uri = new Uri($"{ThemePackPrefix}{theme}.xaml", UriKind.Absolute);
        var replacement = new ResourceDictionary { Source = uri };

        var merged = target.MergedDictionaries;
        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].Source?.OriginalString.Contains(ThemeFilesPath, StringComparison.OrdinalIgnoreCase) == true)
            {
                merged[i] = replacement;
                return;
            }
        }

        throw new InvalidOperationException(
            "No Styles/Themes/*.xaml entry found directly in the target ResourceDictionary's " +
            "MergedDictionaries - merge one (see Styles/Theme.xaml) before calling ThemeManager.Apply.");
    }
}
