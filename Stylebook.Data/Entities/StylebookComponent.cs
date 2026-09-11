namespace Stylebook.Data.Entities;

public class StylebookComponent : IAuditableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ComponentRegion Region { get; set; }

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

    /// <summary>Testcontainer (Componentenbouwer): Vast (component's own size) or Variabel (stretch to fill) - see MainWindow.xaml.cs's ApplyContainerSimulation.</summary>
    public ContainerSizeMode TestContainerWidthMode { get; set; } = ContainerSizeMode.Fixed;

    public ContainerSizeMode TestContainerHeightMode { get; set; } = ContainerSizeMode.Fixed;

    /// <summary>
    /// Expliciete eigen breedte/hoogte (px) van het component zelf, alleen
    /// van toepassing als de bijbehorende Mode hierboven Fixed is - null
    /// betekent "geen expliciete waarde", het component houdt dan zijn
    /// natuurlijke (XAML-eigen) afmeting aan, zoals voorheen altijd het
    /// geval was. Zie ApplySizeConstraints/BakeSizeConstraintsIntoXaml.
    /// </summary>
    public double? FixedWidth { get; set; }

    public double? FixedHeight { get; set; }

    // Audit fields last, by convention - see feedback_db_audit_timestamps
    // memory (most important fields first, date/timestamp fields last).
    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// WPF falls back to this for a ListBoxItem's accessible name when
    /// nothing else is set - without it, screen readers announce the
    /// full type name instead of the component's actual name.
    /// </summary>
    public override string ToString() => Name;
}
