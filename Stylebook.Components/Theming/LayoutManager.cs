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

    public const double MaximumGap = 10;

    public const double DefaultGap = 6;

    public static void ApplyRegionGap(double pixels, ResourceDictionary target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var clamped = Math.Clamp(pixels, MinimumGap, MaximumGap);
        target["RegionGapThickness"] = new Thickness(clamped);

        // Alleen boven en onder - voor wat tegen de buitenrand van een blok
        // ligt en daar dus al ruimte van dat blok zelf krijgt.
        // Ruimte alleen aan de kant waar een buur zit. Zo is de ruimte
        // tussen twee onderdelen precies de ingestelde waarde, in plaats
        // van het dubbele omdat ze allebei een marge meebrengen.
        target["RegionGapLeftTopThickness"] = new Thickness(clamped, clamped, 0, 0);
        target["RegionGapTopThickness"] = new Thickness(0, clamped, 0, 0);
        target["RegionGapLeftThickness"] = new Thickness(clamped, 0, 0, 0);

        // De onderste en rechterrand van de pagina heeft geen buur die het
        // kan doen - die komt van een opvulling om het geheel heen.
        target["RegionGapEndThickness"] = new Thickness(0, 0, clamped, clamped);

        target["RegionGapSize"] = clamped;
    }

    /// <summary>De grenzen van de BORDER-schuifbalk.</summary>
    public const double MinimumBorder = 0;

    public const double MaximumBorder = 10;

    public const double DefaultBorder = 3;

    /// <summary>
    /// Zet de kaderrand om de instellingenkaarten en de infoblokken.
    /// Werkt net als ApplyRegionGap: de sleutel gaat rechtstreeks in de
    /// doeldictionary en overschaduwt daar de standaard uit
    /// Styles/Typography.xaml.
    /// </summary>
    public static void ApplyRegionBorder(double pixels, ResourceDictionary target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var clamped = Math.Clamp(pixels, MinimumBorder, MaximumBorder);
        target["RegionBorderThickness"] = new Thickness(clamped);
        ApplyInnerRadius(clamped, target);
    }

    /// <summary>
    /// Berekent de binnenstraal opnieuw met de randdikte die er NU staat.
    /// Aanroepen na een themawissel: de buitenstraal komt uit het thema
    /// (RadiusMedium, 0 in Visual Studio) en die is dan veranderd, terwijl
    /// de randdikte hetzelfde bleef.
    /// </summary>
    public static void RefreshInnerRadius(ResourceDictionary target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var current = target["RegionBorderThickness"] is Thickness thickness ? thickness.Left : DefaultBorder;
        ApplyInnerRadius(current, target);
    }

    /// <summary>
    /// Binnenstraal = buitenstraal min de randdikte. Zonder dat loopt de
    /// bocht van de titelbalk in een infoblok niet mee met de bocht van de
    /// rand eromheen, en zie je in beide bovenhoeken een kiertje.
    ///
    /// De buitenstraal komt uit het THEMA (RadiusMedium): in LCARS 6, in
    /// Visual Studio 0 - daar is alles recht en moet deze dus ook 0 worden.
    /// </summary>
    private static void ApplyInnerRadius(double borderThickness, ResourceDictionary target)
    {
        var outer = target["RadiusMedium"] is CornerRadius radius ? radius.TopLeft : 0;
        var inner = Math.Max(0, outer - borderThickness);
        target["RegionBorderInnerRadius"] = new CornerRadius(inner, inner, 0, 0);
    }
}
