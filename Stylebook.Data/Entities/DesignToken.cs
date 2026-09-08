namespace Stylebook.Data.Entities;

/// <summary>
/// One hand-editable design-system value (a color, a corner-radius, a
/// spacing amount, a font size, or the font family) - this table is the
/// stylebook: editing it and saving is what "met de hand aanpassen" the
/// app's look means. Stylebook.Playground builds its live WPF
/// ResourceDictionary from these rows (see Theming/DbThemeBuilder.cs),
/// not from a compiled Themes/*.xaml file.
/// </summary>
public class DesignToken : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Matches a {DynamicResource}/{StaticResource} key used throughout the app, e.g. "AccentColor" or "RadiusMedium".</summary>
    public string Name { get; set; } = string.Empty;

    public DesignTokenCategory Category { get; set; }

    /// <summary>Hex for Color, a plain number for Radius/Spacing/FontSize, a font name for FontFamily.</summary>
    public string Value { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public override string ToString() => Name;
}
