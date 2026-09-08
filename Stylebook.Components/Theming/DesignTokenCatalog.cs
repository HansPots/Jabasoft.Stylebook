using System.Windows;
using System.Windows.Media;

namespace Stylebook.Components.Theming;

/// <summary>
/// The single, authoritative list of design tokens every component is
/// expected to build from - colors (per theme), corner-radius scale, and
/// typography. Two consumers: a visual "Stylebook" reference page (so a
/// person can see what's available) and an AI system prompt (so a
/// generated design is built from these exact tokens instead of
/// inventing new ones). Reads color VALUES directly from each theme's
/// own Themes/*.xaml at runtime rather than duplicating hex values here,
/// so this can never drift out of sync with the actual theme files.
/// </summary>
public static class DesignTokenCatalog
{
    // Every Themes/*.xaml declares exactly these Color keys - see the
    // class comment on any one of them (e.g. Lcars.xaml).
    public static readonly string[] ColorTokenNames =
    [
        "BackgroundColor",
        "SurfaceColor",
        "BorderColor",
        "AccentColor",
        "AccentColorDark",
        "AccentForegroundColor",
        "TextPrimaryColor",
        "TextMutedColor",
    ];

    public static readonly (string Name, double Pixels)[] RadiusTokens =
    [
        ("RadiusSmall", 3),
        ("RadiusMedium", 6),
        ("RadiusLarge", 12),
    ];

    public static readonly (string Name, double Pixels)[] SpacingTokens =
    [
        ("SpaceXSmall", 4),
        ("SpaceSmall", 8),
        ("SpaceMedium", 16),
        ("SpaceLarge", 24),
    ];

    public static readonly (string Name, double Pixels)[] FontSizeTokens =
    [
        ("FontSizeSmall", 12),
        ("FontSizeBody", 14),
        ("FontSizeTitle", 18),
    ];

    public const string FontFamilyTokenName = "AppFontFamily";
    public const string FontFamilyValue = "Segoe UI";

    public static ResourceDictionary LoadTheme(Theme theme)
    {
        var uri = new Uri(
            $"pack://application:,,,/Stylebook.Components;component/Styles/Themes/{theme}.xaml", UriKind.Absolute);
        return new ResourceDictionary { Source = uri };
    }

    /// <summary>Each color token's actual value for the given theme, in declared order.</summary>
    public static IReadOnlyList<(string Name, Color Value)> GetColors(Theme theme)
    {
        var dictionary = LoadTheme(theme);
        return ColorTokenNames
            .Where(dictionary.Contains)
            .Select(name => (name, (Color)dictionary[name]!))
            .ToList();
    }

    /// <summary>
    /// Plain-text summary for an AI system prompt: the exact tokens it
    /// must build a design from, brush names (not the raw Color keys, so
    /// the AI writes {DynamicResource XyzBrush} the way every component
    /// already does) plus their current hex value so it can reason about
    /// contrast without guessing.
    /// </summary>
    public static string DescribeForAi(Theme theme)
    {
        var colorLines = GetColors(theme)
            .Select(c => $"  {c.Name.Replace("Color", "Brush", StringComparison.Ordinal)} = {c.Value}");
        var radiusLines = RadiusTokens.Select(r => $"  {r.Name} = {r.Pixels}");
        var spacingLines = SpacingTokens.Select(s => $"  {s.Name} = {s.Pixels}");
        var fontLines = FontSizeTokens.Select(f => $"  {f.Name} = {f.Pixels}");

        return $"""
            Beschikbare stijl-tokens voor thema {theme} - bouw het ontwerp UITSLUITEND met deze tokens,
            verzin geen eigen kleur, ronding of maat. Refereer ALLE tokens (kleur, hoekronding, afstand,
            lettertype, tekstgrootte) via DynamicResource met de TokenNaam - nooit StaticResource, want deze
            XAML wordt at runtime geparsed zonder ambient resource-context, waardoor StaticResource niet oplost.
            Voor Margin/Padding/Thickness mag een token-referentie alleen de VOLLEDIGE attribuutwaarde zijn -
            nooit combineren met losse cijfers en komma's in dezelfde waarde (dus niet eerst 0,0,0, en dan pas
            de referentie); gebruik voor zulke eigenschappen ofwel uitsluitend letterlijke getallen, ofwel
            uitsluitend een token-referentie als hele waarde. Voeg geen XML-commentaar toe in de XAML.
            Kleuren:
            {string.Join('\n', colorLines)}
            Hoekronding:
            {string.Join('\n', radiusLines)}
            Afstand:
            {string.Join('\n', spacingLines)}
            Lettertype: {FontFamilyTokenName} = "{FontFamilyValue}"
            Tekstgrootte:
            {string.Join('\n', fontLines)}
            """;
    }
}
