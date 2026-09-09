namespace Stylebook.Data.Entities;

/// <summary>
/// Which theme a DesignToken row (or the app's current AppSetting) belongs
/// to. Deliberately its own type here, not a reference to
/// Stylebook.Components.Theming.Theme - Stylebook.Data has no WPF/
/// Components dependency (it's a plain net10.0 library). DbThemeBuilder,
/// which already depends on both projects, converts between the two by
/// name where they meet.
/// </summary>
public enum Theme
{
    Lcars,
    VisualStudio,
}
