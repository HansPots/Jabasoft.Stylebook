namespace Stylebook.Data.Entities;

/// <summary>
/// One hand-editable design-system value (a color, a corner-radius, a
/// spacing amount, a font size, or the font family) - this table is the
/// stylebook: editing it and saving is what "met de hand aanpassen" the
/// app's look means. Stylebook.Playground builds its live WPF
/// ResourceDictionary from these rows (see Theming/DbThemeBuilder.cs),
/// not from a compiled Themes/*.xaml file.
///
/// Value/DefaultValue split: Value is the working/draft value shown and
/// edited on the Stylebook tab (with a live, in-page preview as you
/// type). DefaultValue is what the rest of the application actually
/// renders with (DbThemeBuilder.Build reads DefaultValue, falling back
/// to Value only if it's somehow unset). "Opslaan" only ever touches
/// Value; "Maak dit de standaard" copies Value into DefaultValue too -
/// that's the only thing that changes what the wider app looks like.
/// </summary>
public class DesignToken : IAuditableEntity
{
    public int Id { get; set; }

    /// <summary>Matches a {DynamicResource}/{StaticResource} key used throughout the app, e.g. "AccentColor" or "RadiusMedium".</summary>
    public string Name { get; set; } = string.Empty;

    public DesignTokenCategory Category { get; set; }

    /// <summary>Hex for Color, a plain number for Radius/Spacing/FontSize, a font name for FontFamily. The working/draft value - see the class comment.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>What the wider application actually uses - see the class comment. Nullable only so existing rows survive the migration that added this column; DbThemeBuilder falls back to Value when null.</summary>
    public string? DefaultValue { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public override string ToString() => Name;
}
