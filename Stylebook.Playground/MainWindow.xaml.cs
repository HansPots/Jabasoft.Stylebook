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
}
