using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Stylebook.Components.Theming;
using Stylebook.Data;
using Stylebook.Data.Entities;
using ComponentsTheme = Stylebook.Components.Theming.Theme;
using DataTheme = Stylebook.Data.Entities.Theme;

namespace Stylebook.Playground.Theming;

/// <summary>
/// Builds the app's live theme ResourceDictionary from Stylebook.Data's
/// DesignTokens table instead of a compiled Themes/*.xaml file - this is
/// what makes hand-edits on the Stylebook page actually take effect. Every
/// theme has its own row per token Name (unique on Theme+Name), so
/// switching which theme is active (see App.CurrentTheme) never touches
/// another theme's hand-edits - only ApplyPreset (an explicit "reset to
/// preset" action) overwrites a theme's own rows. See DesignToken's class
/// comment for the Value/DefaultValue split, and Stylebook.Data.Entities.
/// Theme's comment for why it's a separate type from
/// Stylebook.Components.Theming.Theme (used here to convert between them).
/// </summary>
public static class DbThemeBuilder
{
    /// <summary>
    /// Seeds any theme that has no rows yet, from its own known preset,
    /// and creates the singleton AppSettings row if missing (defaulting
    /// to VisualStudio) - so there's always something correct to render,
    /// for a brand-new database (both themes get seeded) and for one
    /// migrated from the old single-row-per-name shape (only the theme(s)
    /// never backfilled by that migration get seeded - see
    /// AddThemeScopedDesignTokens's own comment).
    /// </summary>
    public static void EnsureSeeded(StylebookDbContext db)
    {
        foreach (var theme in Enum.GetValues<DataTheme>())
        {
            if (!db.DesignTokens.Any(t => t.Theme == theme))
            {
                ApplyPreset(db, theme);
            }
        }

        if (!db.AppSettings.Any())
        {
            db.AppSettings.Add(new AppSetting { CurrentTheme = DataTheme.VisualStudio });
            db.SaveChanges();
        }
    }

    /// <summary>Overwrites this theme's existing tokens' Value AND DefaultValue to match its known preset, adding any that don't exist yet - never touches another theme's rows.</summary>
    public static void ApplyPreset(StylebookDbContext db, DataTheme theme)
    {
        var componentsTheme = Enum.Parse<ComponentsTheme>(theme.ToString());
        var tokensByName = db.DesignTokens.Where(t => t.Theme == theme).ToDictionary(t => t.Name);

        void Upsert(string name, DesignTokenCategory category, string value)
        {
            if (tokensByName.TryGetValue(name, out var token))
            {
                token.Value = value;
                token.DefaultValue = value;
            }
            else
            {
                db.DesignTokens.Add(new DesignToken { Name = name, Theme = theme, Category = category, Value = value, DefaultValue = value });
            }
        }

        foreach (var (name, value) in DesignTokenCatalog.GetColors(componentsTheme))
        {
            Upsert(name, DesignTokenCategory.Color, value.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var (name, px) in DesignTokenCatalog.RadiusTokens)
        {
            Upsert(name, DesignTokenCategory.Radius, Format(px));
        }

        foreach (var (name, px) in DesignTokenCatalog.SpacingTokens)
        {
            Upsert(name, DesignTokenCategory.Spacing, Format(px));
        }

        foreach (var (name, px) in DesignTokenCatalog.FontSizeTokens)
        {
            Upsert(name, DesignTokenCategory.FontSize, Format(px));
        }

        Upsert(DesignTokenCatalog.FontFamilyTokenName, DesignTokenCategory.FontFamily, DesignTokenCatalog.FontFamilyValue);

        db.SaveChanges();
    }

    /// <summary>
    /// Builds the app-wide live theme for the given theme from its tokens'
    /// DefaultValue (falling back to Value only if DefaultValue is somehow
    /// unset) - "in de applicatie de default waardes gebruikt worden". A
    /// row's in-progress Value is only ever visible on the Stylebook tab's
    /// own live preview until "Maak dit de standaard" promotes it here.
    /// Every token becomes both its raw key (e.g. "AccentColor") and,
    /// for colors, a matching "...Brush" SolidColorBrush - the same two
    /// forms every Themes/*.xaml file provides, so existing
    /// {DynamicResource}/{StaticResource} usages need no changes.
    /// </summary>
    public static ResourceDictionary Build(StylebookDbContext db, DataTheme theme)
    {
        var dictionary = new ResourceDictionary();

        foreach (var token in db.DesignTokens.Where(t => t.Theme == theme).AsEnumerable())
        {
            var value = token.DefaultValue ?? token.Value;

            switch (token.Category)
            {
                case DesignTokenCategory.Color:
                    var color = (Color)ColorConverter.ConvertFromString(value)!;
                    dictionary[token.Name] = color;
                    dictionary[token.Name.Replace("Color", "Brush", StringComparison.Ordinal)] = new SolidColorBrush(color);
                    break;
                case DesignTokenCategory.Radius:
                    dictionary[token.Name] = new CornerRadius(double.Parse(value, CultureInfo.InvariantCulture));
                    break;
                case DesignTokenCategory.Spacing:
                    dictionary[token.Name] = new Thickness(double.Parse(value, CultureInfo.InvariantCulture));
                    break;
                case DesignTokenCategory.FontSize:
                    dictionary[token.Name] = double.Parse(value, CultureInfo.InvariantCulture);
                    break;
                case DesignTokenCategory.FontFamily:
                    dictionary[token.Name] = new FontFamily(value);
                    break;
            }
        }

        return dictionary;
    }

    /// <summary>
    /// Plain-text summary of the CURRENT (possibly hand-edited) token
    /// values for the given theme, for an AI system prompt - see
    /// DesignTokenCatalog.DescribeForAi for the static-file equivalent
    /// this replaces for Stylebook.Playground.
    /// </summary>
    public static string DescribeForAi(StylebookDbContext db, DataTheme theme)
    {
        var tokens = db.DesignTokens.Where(t => t.Theme == theme).AsEnumerable().ToList();

        string Section(DesignTokenCategory category) => string.Join(
            '\n', tokens.Where(t => t.Category == category).Select(t => $"  {DisplayName(t)} = {t.Value}"));

        var fontFamily = tokens.FirstOrDefault(t => t.Category == DesignTokenCategory.FontFamily);

        return $"""
            Beschikbare stijl-tokens voor thema {theme} (het handmatig aangepaste Stylebook) - bouw het ontwerp
            UITSLUITEND met deze tokens, verzin geen eigen kleur, ronding of maat. Refereer ALLE tokens (kleur,
            hoekronding, afstand, lettertype, tekstgrootte) via DynamicResource met de TokenNaam - nooit
            StaticResource, want deze XAML wordt at runtime geparsed zonder ambient resource-context, waardoor
            StaticResource niet oplost. Voor Margin/Padding/Thickness/CornerRadius mag een token-referentie alleen
            de VOLLEDIGE attribuutwaarde zijn - nooit combineren met losse cijfers of meerdere tokens in dezelfde
            komma-waarde (dus niet eerst 0,0,0, en dan pas de referentie, en ook niet twee tokens samen in een
            waarde); gebruik voor zulke eigenschappen ofwel uitsluitend letterlijke getallen, ofwel uitsluitend een
            token-referentie als hele waarde. Moet een Border per hoek een andere hoekronding hebben (dus niet alle
            vier gelijk) EN moet dat via tokens - dan kan het CornerRadius-attribuut zelf niet gebruikt worden. Zet
            in plaats daarvan op de Border het namespace-voorvoegsel xmlns:theming gelijk aan clr-namespace:
            Stylebook.Components.Theming;assembly=Stylebook.Components, en gebruik per hoek een eigen apart
            attribuut - theming:CornerRadiusParts.TopLeft, theming:CornerRadiusParts.TopRight,
            theming:CornerRadiusParts.BottomRight en theming:CornerRadiusParts.BottomLeft - elk met DynamicResource
            verwijzend naar de gewenste token, precies zoals bij elk ander attribuut. Elk van die vier attributen
            mag zelf weer ofwel een token ofwel een letterlijk getal zijn (bv. 0 voor een rechte hoek), nooit
            gemixed binnen dat ene attribuut. Een hoek die je weglaat wordt 0. Hetzelfde probleem, dezelfde
            oplossing geldt voor Margin (theming:MarginParts.Left/Top/Right/Bottom) en voor de Padding van een
            Border of Control (theming:PaddingParts.Left/Top/Right/Bottom) zodra niet alle zijden gelijk hoeven te
            zijn maar er wel ergens een token gebruikt moet worden - ook daar wordt een weggelaten zijde 0, en mag
            elke zijde apart ofwel een token ofwel een letterlijk getal zijn. Zet nooit ALSNOG een gewoon
            Margin- of Padding-attribuut op hetzelfde element als je deze Parts-attributen gebruikt - die vervangen
            het volledig, een los Margin/Padding-attribuut ernaast wordt genegeerd. Elke Spacing-token is altijd een
            positieve grootte, maar een naar-buiten-getrokken Margin/Padding-zijde (bv. een kop die net buiten de
            rand van zijn kaart uitsteekt) is vaak negatief - daar bestaat geen apart negatief token voor. Zet in
            dat geval naast de gewone theming:MarginParts.Zijde of theming:PaddingParts.Zijde ook het bijbehorende
            theming:MarginParts.ZijdeNegative="True" (of PaddingParts.ZijdeNegative) - dat maakt precies die ene
            zijde negatief, ook als de waarde zelf van een token komt. Alleen relevant voor Margin/Padding, niet
            voor CornerRadius (een negatieve hoekronding bestaat niet). Voeg geen XML-commentaar toe in
            de XAML.
            Kleuren:
            {Section(DesignTokenCategory.Color)}
            Hoekronding:
            {Section(DesignTokenCategory.Radius)}
            Afstand:
            {Section(DesignTokenCategory.Spacing)}
            Lettertype: {fontFamily?.Name} = "{fontFamily?.Value}"
            Tekstgrootte:
            {Section(DesignTokenCategory.FontSize)}
            """;
    }

    private static string DisplayName(DesignToken token) =>
        token.Category == DesignTokenCategory.Color ? token.Name.Replace("Color", "Brush", StringComparison.Ordinal) : token.Name;

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);
}
