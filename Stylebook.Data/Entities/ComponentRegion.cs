namespace Stylebook.Data.Entities;

/// <summary>
/// Which slot of the Basis app-shell (Stylebook.Components/Controls/Basis.xaml)
/// a component is meant for - Algemeen is for anything not tied to one
/// specific region (e.g. Card).
/// </summary>
public enum ComponentRegion
{
    Algemeen,
    Header,
    Menu,
    Inhoud,
    Actie,
    Footer,
}
