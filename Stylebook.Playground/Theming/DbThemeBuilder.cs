using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Stylebook.Components.Theming;
using Stylebook.Data;
using Stylebook.Data.Entities;

namespace Stylebook.Playground.Theming;

/// <summary>
/// Builds the app's live theme ResourceDictionary from Stylebook.Data's
/// DesignTokens table instead of a compiled Themes/*.xaml file - this is
/// what makes hand-edits on the Stylebook page actually take effect.
/// ApplyPreset loads a known theme's values INTO that same editable
/// table (upsert by Name) rather than switching to a separate live
/// scope - "wisselen van stijl" is loading a starting point, hand-edits
/// afterwards still work exactly the same way.
/// </summary>
public static class DbThemeBuilder
{
    /// <summary>Seeds the table once, from Visual Studio's values, if it's empty - so there's always something correct to render.</summary>
    public static void EnsureSeeded(StylebookDbContext db)
    {
        if (!db.DesignTokens.Any())
        {
            ApplyPreset(db, Theme.VisualStudio);
        }
    }

    /// <summary>Overwrites every existing token's Value to match the given theme's known preset, adding any that don't exist yet.</summary>
    public static void ApplyPreset(StylebookDbContext db, Theme theme)
    {
        var tokensByName = db.DesignTokens.ToDictionary(t => t.Name);

        void Upsert(string name, DesignTokenCategory category, string value)
        {
            if (tokensByName.TryGetValue(name, out var token))
            {
                token.Value = value;
            }
            else
            {
                db.DesignTokens.Add(new DesignToken { Name = name, Category = category, Value = value });
            }
        }

        foreach (var (name, value) in DesignTokenCatalog.GetColors(theme))
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
    /// Every token becomes both its raw key (e.g. "AccentColor") and,
    /// for colors, a matching "...Brush" SolidColorBrush - the same two
    /// forms every Themes/*.xaml file provides, so existing
    /// {DynamicResource}/{StaticResource} usages need no changes.
    /// </summary>
    public static ResourceDictionary Build(StylebookDbContext db)
    {
        var dictionary = new ResourceDictionary();

        foreach (var token in db.DesignTokens.AsEnumerable())
        {
            switch (token.Category)
            {
                case DesignTokenCategory.Color:
                    var color = (Color)ColorConverter.ConvertFromString(token.Value)!;
                    dictionary[token.Name] = color;
                    dictionary[token.Name.Replace("Color", "Brush", StringComparison.Ordinal)] = new SolidColorBrush(color);
                    break;
                case DesignTokenCategory.Radius:
                    dictionary[token.Name] = new CornerRadius(double.Parse(token.Value, CultureInfo.InvariantCulture));
                    break;
                case DesignTokenCategory.Spacing:
                    dictionary[token.Name] = new Thickness(double.Parse(token.Value, CultureInfo.InvariantCulture));
                    break;
                case DesignTokenCategory.FontSize:
                    dictionary[token.Name] = double.Parse(token.Value, CultureInfo.InvariantCulture);
                    break;
                case DesignTokenCategory.FontFamily:
                    dictionary[token.Name] = new FontFamily(token.Value);
                    break;
            }
        }

        return dictionary;
    }

    /// <summary>
    /// Plain-text summary of the CURRENT (possibly hand-edited) token
    /// values, for an AI system prompt - see DesignTokenCatalog.DescribeForAi
    /// for the static-file equivalent this replaces for Stylebook.Playground.
    /// </summary>
    public static string DescribeForAi(StylebookDbContext db)
    {
        var tokens = db.DesignTokens.AsEnumerable().ToList();

        string Section(DesignTokenCategory category) => string.Join(
            '\n', tokens.Where(t => t.Category == category).Select(t => $"  {DisplayName(t)} = {t.Value}"));

        var fontFamily = tokens.FirstOrDefault(t => t.Category == DesignTokenCategory.FontFamily);

        return $"""
            Beschikbare stijl-tokens (het handmatig aangepaste Stylebook) - bouw het ontwerp UITSLUITEND met deze
            tokens, verzin geen eigen kleur, ronding of maat. Kleuren refereer je via DynamicResource met de
            TokenNaam, overige tokens (hoekronding/afstand/lettertype) via StaticResource met de TokenNaam.
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
