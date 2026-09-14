namespace Stylebook.Data.Entities;

/// <summary>
/// Which slot of the Basis app-shell (Stylebook.Components/Controls/Basis.xaml)
/// a component is meant for. Components not tied to one specific region
/// (e.g. Card) live in Stylebook.Components/Controls and are tagged by
/// namespace instead - see MainWindow.DiscoverLibraryControls, where a
/// null ComponentRegion means "Controls", not one of these five.
/// </summary>
public enum ComponentRegion
{
    Header,
    Menu,
    Inhoud,
    Actie,
    Footer,
}
