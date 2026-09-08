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
/// Seeds the table once (from DesignTokenCatalog's VisualStudio values -
/// see [[project_jabasoft_stylebook_architecture]]) if it's empty, so the
/// app always has something correct to render even before anyone edits
/// anything.
/// </summary>
public static class DbThemeBuilder
{
    public static void EnsureSeeded(StylebookDbContext db)
    {
        if (db.DesignTokens.Any())
        {
            return;
        }

        foreach (var (name, value) in DesignTokenCatalog.GetColors(Theme.VisualStudio))
        {
            db.DesignTokens.Add(new DesignToken
            {
                Name = name,
                Category = DesignTokenCategory.Color,
                Value = value.ToString(CultureInfo.InvariantCulture),
            });
        }

        foreach (var (name, px) in DesignTokenCatalog.RadiusTokens)
        {
            db.DesignTokens.Add(new DesignToken { Name = name, Category = DesignTokenCategory.Radius, Value = Format(px) });
        }

        foreach (var (name, px) in DesignTokenCatalog.SpacingTokens)
        {
            db.DesignTokens.Add(new DesignToken { Name = name, Category = DesignTokenCategory.Spacing, Value = Format(px) });
        }

        foreach (var (name, px) in DesignTokenCatalog.FontSizeTokens)
        {
            db.DesignTokens.Add(new DesignToken { Name = name, Category = DesignTokenCategory.FontSize, Value = Format(px) });
        }

        db.DesignTokens.Add(new DesignToken
        {
            Name = DesignTokenCatalog.FontFamilyTokenName,
            Category = DesignTokenCategory.FontFamily,
            Value = DesignTokenCatalog.FontFamilyValue,
        });

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
