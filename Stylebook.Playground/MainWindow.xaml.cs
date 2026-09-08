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
using Stylebook.Components.Controls;
using Stylebook.Components.Theming;
using Stylebook.Data.Entities;

namespace Stylebook.Playground;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private enum BuilderMode
    {
        /// <summary>Place already-built components into Header/Menu/Inhoud/Actie/Footer - no editing.</summary>
        PageBuilder,

        /// <summary>See and build exactly one component in isolation - editing (add/rename/delete) lives here.</summary>
        ComponentBuilder,
    }

    private sealed record ThemeOption(Theme Value, string Label);

    private static readonly ThemeOption[] ThemeOptions =
    [
        new ThemeOption(Theme.Lcars, "LCARS"),
        new ThemeOption(Theme.VisualStudio, "Visual Studio"),
    ];

    private BuilderMode _builderMode = BuilderMode.PageBuilder;
    private StylebookComponent? _lastSelectedComponent;

    public MainWindow()
    {
        InitializeComponent();

        ThemePicker.ItemsSource = ThemeOptions;
        ThemePicker.DisplayMemberPath = nameof(ThemeOption.Label);
        ThemePicker.SelectedIndex = 0;

        LoadComponentsByRegion();
        PageBuilderModeButton.IsChecked = true;
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

        RefreshPreview();
    }

    private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemePicker.SelectedItem is ThemeOption option)
        {
            ThemeManager.Apply(option.Value, StylePreviewArea.Resources);
        }
    }

    private void PageBuilderMode_Checked(object sender, RoutedEventArgs e) => SetBuilderMode(BuilderMode.PageBuilder);

    private void ComponentBuilderMode_Checked(object sender, RoutedEventArgs e) => SetBuilderMode(BuilderMode.ComponentBuilder);

    /// <summary>
    /// Gates component editing to the Componentenbouwer tab: the
    /// Paginabouwer only places already-built components, it can never
    /// add/rename/delete one.
    /// </summary>
    private void SetBuilderMode(BuilderMode mode)
    {
        _builderMode = mode;

        var editingAllowed = mode == BuilderMode.ComponentBuilder;
        HeaderEditRow.IsEnabled = editingAllowed;
        MenuEditRow.IsEnabled = editingAllowed;
        InhoudEditRow.IsEnabled = editingAllowed;
        ActieEditRow.IsEnabled = editingAllowed;
        FooterEditRow.IsEnabled = editingAllowed;
        AlgemeenEditRow.IsEnabled = editingAllowed;

        PageBuilderBasis.Visibility = mode == BuilderMode.PageBuilder ? Visibility.Visible : Visibility.Collapsed;
        ComponentBuilderCanvas.Visibility = mode == BuilderMode.ComponentBuilder ? Visibility.Visible : Visibility.Collapsed;

        RefreshPreview();
    }

    private void Component_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var listBox = (ListBox)sender;
        var region = Enum.Parse<ComponentRegion>((string)listBox.Tag);
        var selected = listBox.SelectedItem as StylebookComponent;
        NewComponentNameBox(region).Text = selected?.Name ?? string.Empty;

        if (selected is not null)
        {
            _lastSelectedComponent = selected;
        }

        RefreshPreview();
    }

    /// <summary>
    /// Paginabouwer: fills every Basis slot from whatever is selected in
    /// that region's list (Algemeen has no slot, so it's not placeable).
    /// Componentenbouwer: shows just the most recently selected component,
    /// with no Basis chrome around it.
    /// </summary>
    private void RefreshPreview()
    {
        if (_builderMode == BuilderMode.PageBuilder)
        {
            PageBuilderBasis.HeaderContent = CreateComponentVisual(ComponentsListBox(ComponentRegion.Header).SelectedItem as StylebookComponent);
            PageBuilderBasis.MenuContent = CreateComponentVisual(ComponentsListBox(ComponentRegion.Menu).SelectedItem as StylebookComponent);
            PageBuilderBasis.MainContent = CreateComponentVisual(ComponentsListBox(ComponentRegion.Inhoud).SelectedItem as StylebookComponent);
            PageBuilderBasis.ActionContent = CreateComponentVisual(ComponentsListBox(ComponentRegion.Actie).SelectedItem as StylebookComponent);
            PageBuilderBasis.FooterContent = CreateComponentVisual(ComponentsListBox(ComponentRegion.Footer).SelectedItem as StylebookComponent);
        }
        else
        {
            ComponentBuilderContent.Content = CreateComponentVisual(_lastSelectedComponent);
        }
    }

    /// <summary>
    /// What a catalog entry (Stylebook.Data.Entities.StylebookComponent)
    /// looks like when placed somewhere. Only "Card" has a real compiled
    /// control to show yet - everything else is a labeled placeholder
    /// until components store actual, renderable content.
    /// {DynamicResource} is applied via SetResourceReference (not
    /// resolved once via FindResource) so these visuals keep responding
    /// live to the preview area's LCARS/Visual Studio switch.
    /// </summary>
    private FrameworkElement CreateComponentVisual(StylebookComponent? component)
    {
        if (component is null)
        {
            var empty = new TextBlock { Text = "(leeg)", HorizontalAlignment = HorizontalAlignment.Center };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            empty.SetResourceReference(TextBlock.FontFamilyProperty, "AppFontFamily");
            empty.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeSmall");
            return empty;
        }

        if (string.Equals(component.Name, "Card", StringComparison.OrdinalIgnoreCase))
        {
            var body = new TextBlock { Text = "Voorbeeldinhoud", TextWrapping = TextWrapping.Wrap };
            body.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            body.SetResourceReference(TextBlock.FontFamilyProperty, "AppFontFamily");
            body.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeBody");

            return new Card { Title = component.Name, Body = body, Width = 280, HorizontalAlignment = HorizontalAlignment.Center };
        }

        var label = new TextBlock { Text = component.Name };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        label.SetResourceReference(TextBlock.FontFamilyProperty, "AppFontFamily");
        label.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeBody");

        var placeholder = new Border { Child = label, BorderThickness = new Thickness(1) };
        placeholder.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        placeholder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        placeholder.SetResourceReference(Border.PaddingProperty, "SpaceMedium");
        return placeholder;
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
