namespace Stylebook.Data.Entities;

public class StylebookComponent : IAuditableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ComponentRegion Region { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// WPF falls back to this for a ListBoxItem's accessible name when
    /// nothing else is set - without it, screen readers announce the
    /// full type name instead of the component's actual name.
    /// </summary>
    public override string ToString() => Name;
}
