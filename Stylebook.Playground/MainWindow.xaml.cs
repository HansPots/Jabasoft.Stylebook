using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Stylebook.Components.Theming;
using Stylebook.Data.Entities;

namespace Stylebook.Playground;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private sealed record ThemeOption(Theme Value, string Label);

    private static readonly ThemeOption[] ThemeOptions =
    [
        new ThemeOption(Theme.Lcars, "LCARS"),
        new ThemeOption(Theme.VisualStudio, "Visual Studio"),
    ];

    public MainWindow()
    {
        InitializeComponent();

        ThemePicker.ItemsSource = ThemeOptions;
        ThemePicker.DisplayMemberPath = nameof(ThemeOption.Label);
        ThemePicker.SelectedIndex = 0;

        LoadComponentsByRegion();
    }

    private void LoadComponentsByRegion()
    {
        var componentsByRegion = App.Db.Components.AsEnumerable().ToLookup(c => c.Region);

        HeaderComponents.ItemsSource = componentsByRegion[ComponentRegion.Header].ToList();
        MenuComponents.ItemsSource = componentsByRegion[ComponentRegion.Menu].ToList();
        InhoudComponents.ItemsSource = componentsByRegion[ComponentRegion.Inhoud].ToList();
        ActieComponents.ItemsSource = componentsByRegion[ComponentRegion.Actie].ToList();
        FooterComponents.ItemsSource = componentsByRegion[ComponentRegion.Footer].ToList();
        AlgemeenComponents.ItemsSource = componentsByRegion[ComponentRegion.Algemeen].ToList();
    }

    private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemePicker.SelectedItem is ThemeOption option)
        {
            ThemeManager.Apply(option.Value, StylePreviewArea.Resources);
        }
    }

    private void Component_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var listBox = (ListBox)sender;
        var region = Enum.Parse<ComponentRegion>((string)listBox.Tag);
        NewComponentNameBox(region).Text = (listBox.SelectedItem as StylebookComponent)?.Name ?? string.Empty;
    }

    private void AddComponent_Click(object sender, RoutedEventArgs e)
    {
        SaveComponent(Enum.Parse<ComponentRegion>((string)((Button)sender).Tag));
    }

    private void NewComponentName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        SaveComponent(Enum.Parse<ComponentRegion>((string)((TextBox)sender).Tag));
    }

    /// <summary>
    /// Adds a new component, or - when one is selected in the region's
    /// ListBox - renames it instead. CreatedAtUtc/UpdatedAtUtc are stamped
    /// by StylebookDbContext.SaveChanges, never set here.
    /// </summary>
    private void SaveComponent(ComponentRegion region)
    {
        var nameBox = NewComponentNameBox(region);
        var name = nameBox.Text.Trim();
        if (name.Length == 0)
        {
            return;
        }

        if (ComponentsListBox(region).SelectedItem is StylebookComponent existing)
        {
            existing.Name = name;
        }
        else
        {
            App.Db.Components.Add(new StylebookComponent { Name = name, Region = region });
        }

        App.Db.SaveChanges();
        nameBox.Clear();
        LoadComponentsByRegion();
    }

    private void DeleteComponent_Click(object sender, RoutedEventArgs e)
    {
        var region = Enum.Parse<ComponentRegion>((string)((Button)sender).Tag);
        if (ComponentsListBox(region).SelectedItem is not StylebookComponent selected)
        {
            return;
        }

        App.Db.Components.Remove(selected);
        App.Db.SaveChanges();

        NewComponentNameBox(region).Clear();
        LoadComponentsByRegion();
    }

    private TextBox NewComponentNameBox(ComponentRegion region) => region switch
    {
        ComponentRegion.Header => HeaderNewComponentName,
        ComponentRegion.Menu => MenuNewComponentName,
        ComponentRegion.Inhoud => InhoudNewComponentName,
        ComponentRegion.Actie => ActieNewComponentName,
        ComponentRegion.Footer => FooterNewComponentName,
        ComponentRegion.Algemeen => AlgemeenNewComponentName,
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
    };

    private ListBox ComponentsListBox(ComponentRegion region) => region switch
    {
        ComponentRegion.Header => HeaderComponents,
        ComponentRegion.Menu => MenuComponents,
        ComponentRegion.Inhoud => InhoudComponents,
        ComponentRegion.Actie => ActieComponents,
        ComponentRegion.Footer => FooterComponents,
        ComponentRegion.Algemeen => AlgemeenComponents,
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
    };
}
