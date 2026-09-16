using System.Windows;

namespace Stylebook.Components.Theming;

/// <summary>
/// Zet de zwarte rand om de vijf blokken van Basis.xaml (header, menu,
/// inhoud, actie, footer) - de SPACING-instelling.
///
/// Werkt door de sleutel RegionGapThickness rechtstreeks in de doeldictionary
/// te zetten, waar hij de standaard uit Styles/Typography.xaml overschaduwt.
/// Basis.xaml verwijst er met DynamicResource naar, dus elk blok dat al op
/// het scherm staat verspringt meteen mee.
/// </summary>
public static class LayoutManager
{
    /// <summary>De grenzen van de SPACING-schuifbalk - ook hier vastgelegd, zodat een app dezelfde grenzen kan aanhouden zonder ze over te typen.</summary>
    public const double MinimumGap = 0;

    public const double MaximumGap = 5;

    public const double DefaultGap = 0;

    public static void ApplyRegionGap(double pixels, ResourceDictionary target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var clamped = Math.Clamp(pixels, MinimumGap, MaximumGap);
        target["RegionGapThickness"] = new Thickness(clamped);
    }
}
