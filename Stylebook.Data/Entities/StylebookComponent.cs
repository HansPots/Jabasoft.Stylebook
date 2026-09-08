namespace Stylebook.Data.Entities;

public class StylebookComponent : IAuditableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ComponentRegion Region { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Quick-edit field: what the properties panel writes into the generated Xaml's title text.</summary>
    public string? Title { get; set; }

    /// <summary>Quick-edit field: what the properties panel writes into the generated Xaml's body text.</summary>
    public string? BodyText { get; set; }

    /// <summary>
    /// The component's actual visual definition - parsed with XamlReader
    /// at render time. Source of truth for what's on screen; Title/
    /// BodyText are only a convenience for regenerating a simple version
    /// of it, editing Xaml directly does not update them.
    /// </summary>
    public string? Xaml { get; set; }

    /// <summary>
    /// WPF falls back to this for a ListBoxItem's accessible name when
    /// nothing else is set - without it, screen readers announce the
    /// full type name instead of the component's actual name.
    /// </summary>
    public override string ToString() => Name;
}
