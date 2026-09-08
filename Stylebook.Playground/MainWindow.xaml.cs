using System;
using System.Linq;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
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
    private enum BuilderMode
    {
        /// <summary>Place already-built components into Header/Menu/Inhoud/Actie/Footer - no editing.</summary>
        PageBuilder,

        /// <summary>See and build exactly one component in isolation - editing (add/rename/delete) lives here.</summary>
        ComponentBuilder,

        /// <summary>Reference-only: the fixed palette (colors/radii/typography) for both themes, side by side.</summary>
        Stylebook,
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

    private void StylebookMode_Checked(object sender, RoutedEventArgs e) => SetBuilderMode(BuilderMode.Stylebook);

    /// <summary>
    /// Gates component editing to the Componentenbouwer tab: the
    /// Paginabouwer and Stylebook tabs only ever look, they can never
    /// add/rename/delete a component.
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
        StylebookContent.Visibility = mode == BuilderMode.Stylebook ? Visibility.Visible : Visibility.Collapsed;

        if (mode == BuilderMode.Stylebook)
        {
            StylebookContent.Content ??= BuildStylebookPanel();
        }
        else
        {
            RefreshPreview();
        }
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
            ComponentTitleBox.Text = selected.Title ?? string.Empty;
            ComponentBodyBox.Text = selected.BodyText ?? string.Empty;
            ComponentXamlBox.Text = selected.Xaml ?? string.Empty;
            XamlErrorText.Visibility = Visibility.Collapsed;
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
    /// Built once (cached on StylebookContent.Content, see SetBuilderMode)
    /// from Stylebook.Components.Theming.DesignTokenCatalog - both themes'
    /// actual color VALUES side by side, not {DynamicResource ...}, since
    /// the whole point is comparing them regardless of which one is
    /// active in the ThemePicker.
    /// </summary>
    private static FrameworkElement BuildStylebookPanel()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var lcarsColumn = BuildThemeColumn(Theme.Lcars);
        Grid.SetColumn(lcarsColumn, 0);
        var vsColumn = BuildThemeColumn(Theme.VisualStudio);
        Grid.SetColumn(vsColumn, 1);

        grid.Children.Add(lcarsColumn);
        grid.Children.Add(vsColumn);
        return grid;
    }

    private static FrameworkElement BuildThemeColumn(Theme theme)
    {
        var colors = DesignTokenCatalog.GetColors(theme);
        Color ColorOf(string name) => colors.First(c => c.Name == name).Value;

        var background = ColorOf("BackgroundColor");
        var surface = ColorOf("SurfaceColor");
        var border = ColorOf("BorderColor");
        var accent = ColorOf("AccentColor");
        var textPrimary = ColorOf("TextPrimaryColor");
        var textMuted = ColorOf("TextMutedColor");

        var stack = new StackPanel { Margin = new Thickness(24) };

        stack.Children.Add(new TextBlock
        {
            Text = theme.ToString(),
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(textPrimary),
            Margin = new Thickness(0, 0, 0, 16),
        });

        stack.Children.Add(SectionLabel("Kleuren", textMuted));
        foreach (var (name, value) in colors)
        {
            stack.Children.Add(ColorSwatchRow(name, value, textPrimary, surface, border));
        }

        stack.Children.Add(SectionLabel("Hoekronding", textMuted));
        foreach (var (name, px) in DesignTokenCatalog.RadiusTokens)
        {
            stack.Children.Add(RadiusSample(name, px, surface, border, textPrimary));
        }

        stack.Children.Add(SectionLabel("Afstand", textMuted));
        foreach (var (name, px) in DesignTokenCatalog.SpacingTokens)
        {
            stack.Children.Add(SpacingSample(name, px, accent, textPrimary));
        }

        stack.Children.Add(SectionLabel("Tekstgrootte", textMuted));
        foreach (var (name, px) in DesignTokenCatalog.FontSizeTokens)
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"{name} ({px}px) - Aa Bb Cc",
                FontSize = px,
                FontFamily = new FontFamily(DesignTokenCatalog.FontFamilyValue),
                Foreground = new SolidColorBrush(textPrimary),
                Margin = new Thickness(0, 4, 0, 0),
            });
        }

        return new Border
        {
            Background = new SolidColorBrush(background),
            BorderBrush = new SolidColorBrush(border),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        };
    }

    private static TextBlock SectionLabel(string text, Color mutedColor) => new()
    {
        Text = text.ToUpperInvariant(),
        Foreground = new SolidColorBrush(mutedColor),
        FontSize = 12,
        Margin = new Thickness(0, 20, 0, 8),
    };

    private static FrameworkElement ColorSwatchRow(string name, Color value, Color textColor, Color surfaceColor, Color borderColor)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(new Border
        {
            Width = 28,
            Height = 28,
            Background = new SolidColorBrush(value),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 8, 0),
        });
        row.Children.Add(new TextBlock
        {
            Text = $"{name}  {value}",
            Foreground = new SolidColorBrush(textColor),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
        });
        return row;
    }

    private static FrameworkElement RadiusSample(string name, double px, Color surfaceColor, Color borderColor, Color textColor)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(new Border
        {
            Width = 48,
            Height = 28,
            Background = new SolidColorBrush(surfaceColor),
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(px),
            Margin = new Thickness(0, 0, 8, 0),
        });
        row.Children.Add(new TextBlock
        {
            Text = $"{name} ({px}px)",
            Foreground = new SolidColorBrush(textColor),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
        });
        return row;
    }

    private static FrameworkElement SpacingSample(string name, double px, Color accentColor, Color textColor)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(new Border
        {
            Width = px,
            Height = 14,
            Background = new SolidColorBrush(accentColor),
            Margin = new Thickness(0, 0, 8, 0),
        });
        row.Children.Add(new TextBlock
        {
            Text = $"{name} ({px}px)",
            Foreground = new SolidColorBrush(textColor),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
        });
        return row;
    }

    /// <summary>
    /// What a catalog entry (Stylebook.Data.Entities.StylebookComponent)
    /// looks like when placed somewhere: its stored Xaml, parsed live with
    /// XamlReader. Falls back to a labeled placeholder when there's no
    /// Xaml yet (an older row, or one never edited), and to a visibly
    /// different error box when the stored Xaml fails to parse - this
    /// runs on every keystroke's worth of "Opslaan en toepassen", so it
    /// must never let bad markup take the app down with it.
    /// {DynamicResource} on the fallback/error visuals is applied via
    /// SetResourceReference (not resolved once via FindResource) so they
    /// keep responding live to the preview area's LCARS/Visual Studio
    /// switch, same as the parsed Xaml's own DynamicResource bindings do
    /// once it's part of the live tree.
    /// </summary>
    private FrameworkElement CreateComponentVisual(StylebookComponent? component)
    {
        if (component is null)
        {
            return Placeholder("(leeg)", "TextMutedBrush", "BorderBrush");
        }

        if (string.IsNullOrWhiteSpace(component.Xaml))
        {
            return Placeholder(component.Name, "TextPrimaryBrush", "BorderBrush");
        }

        try
        {
            if (XamlReader.Parse(component.Xaml) is FrameworkElement parsed)
            {
                return parsed;
            }

            return Placeholder($"'{component.Name}' is geen FrameworkElement", "TextMutedBrush", "BorderBrush");
        }
        catch (Exception ex)
        {
            return Placeholder($"XAML-fout in '{component.Name}': {ex.Message}", "TextMutedBrush", "AccentBrush");
        }
    }

    private static FrameworkElement Placeholder(string text, string foregroundKey, string borderKey)
    {
        var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 };
        label.SetResourceReference(TextBlock.ForegroundProperty, foregroundKey);
        label.SetResourceReference(TextBlock.FontFamilyProperty, "AppFontFamily");
        label.SetResourceReference(TextBlock.FontSizeProperty, "FontSizeBody");

        var box = new Border { Child = label, BorderThickness = new Thickness(1) };
        box.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
        box.SetResourceReference(Border.BorderBrushProperty, borderKey);
        box.SetResourceReference(Border.PaddingProperty, "SpaceMedium");
        return box;
    }

    /// <summary>
    /// Quick-edit path: builds a simple card-shaped Xaml from the Titel/
    /// Inhoud fields and saves it as the component's Xaml. Editing Xaml
    /// directly afterwards does not update Titel/Inhoud back - they're a
    /// generator, not a live-synced view of the markup.
    /// </summary>
    private void GenerateFromProperties_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSelectedComponent is not { } component)
        {
            return;
        }

        component.Title = ComponentTitleBox.Text;
        component.BodyText = ComponentBodyBox.Text;
        component.Xaml = GenerateCardXaml(ComponentTitleBox.Text, ComponentBodyBox.Text);
        ComponentXamlBox.Text = component.Xaml;

        App.Db.SaveChanges();
        XamlErrorText.Visibility = Visibility.Collapsed;
        RefreshPreview();
    }

    /// <summary>
    /// Advanced-edit path: saves whatever is currently in the XAML box as
    /// the component's Xaml, verbatim. Saved even if it fails to parse -
    /// CreateComponentVisual shows the parse error instead of crashing,
    /// so an in-progress edit is never lost.
    /// </summary>
    private void SaveXaml_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSelectedComponent is not { } component)
        {
            return;
        }

        component.Xaml = ComponentXamlBox.Text;
        App.Db.SaveChanges();

        try
        {
            XamlReader.Parse(component.Xaml);
            XamlErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            XamlErrorText.Text = ex.Message;
            XamlErrorText.Visibility = Visibility.Visible;
        }

        RefreshPreview();
    }

    /// <summary>
    /// Base instruction for every AI call: the design-token catalog for
    /// whichever theme is currently selected (see DesignTokenCatalog -
    /// this is the "kies daaruit" palette, not a suggestion the model can
    /// ignore) plus the selected component's current Xaml, when there is
    /// one, so a request like "maak 'm ronder" has something to work from.
    /// </summary>
    private string BuildAiSystemPrompt()
    {
        var theme = (ThemePicker.SelectedItem as ThemeOption)?.Value ?? Theme.Lcars;
        var prompt = "Je bent een assistent die WPF-XAML-componenten voor Stylebook bouwt en aanpast.\n" +
                     DesignTokenCatalog.DescribeForAi(theme);

        if (_lastSelectedComponent is { Xaml.Length: > 0 } component)
        {
            prompt += $"\nDit is de huidige XAML van '{component.Name}':\n{component.Xaml}";
        }

        return prompt;
    }

    /// <summary>
    /// Sends the question (plus the selected component's current Xaml as
    /// context, when there is one) to App.Ai and just shows the raw
    /// answer - for questions/explanations. Use "Pas toe op XAML" instead
    /// to have the AI change the component itself.
    /// </summary>
    private async void AskAi_Click(object sender, RoutedEventArgs e)
    {
        var question = AiQuestionBox.Text.Trim();
        if (question.Length == 0)
        {
            return;
        }

        var originalContent = AskAiButton.Content;
        AskAiButton.IsEnabled = false;
        AskAiButton.Content = "Bezig...";
        AiAnswerBox.Text = string.Empty;

        try
        {
            AiAnswerBox.Text = await App.Ai.AskAsync(BuildAiSystemPrompt(), question);
        }
        catch (Exception ex)
        {
            AiAnswerBox.Text = $"Kon geen antwoord krijgen van de AI-server: {ex.Message}";
        }
        finally
        {
            AskAiButton.IsEnabled = true;
            AskAiButton.Content = originalContent;
        }
    }

    /// <summary>
    /// Same request as AskAi_Click, but instructs the model to answer with
    /// ONLY the updated Xaml and applies that answer straight to
    /// ComponentXamlBox - saved and re-rendered exactly like a manual
    /// "Opslaan en toepassen" would, errors included, so a bad AI answer
    /// is visible and recoverable rather than silently discarded.
    /// </summary>
    private async void AskAiApplyXaml_Click(object sender, RoutedEventArgs e)
    {
        var question = AiQuestionBox.Text.Trim();
        if (question.Length == 0 || _lastSelectedComponent is not { } component)
        {
            return;
        }

        var originalContent = ApplyAiXamlButton.Content;
        ApplyAiXamlButton.IsEnabled = false;
        ApplyAiXamlButton.Content = "Bezig...";
        AiAnswerBox.Text = string.Empty;

        try
        {
            var systemPrompt = BuildAiSystemPrompt() +
                "\nAntwoord ALLEEN met de volledige, aangepaste XAML - geen uitleg, geen markdown-codeblokken.";

            var xaml = StripMarkdownFence(await App.Ai.AskAsync(systemPrompt, question));

            ComponentXamlBox.Text = xaml;
            component.Xaml = xaml;
            App.Db.SaveChanges();

            try
            {
                XamlReader.Parse(xaml);
                XamlErrorText.Visibility = Visibility.Collapsed;
                AiAnswerBox.Text = "XAML aangepast en opgeslagen.";
            }
            catch (Exception parseEx)
            {
                XamlErrorText.Text = parseEx.Message;
                XamlErrorText.Visibility = Visibility.Visible;
                AiAnswerBox.Text = "XAML aangepast en opgeslagen, maar bevat een fout - zie de melding bij de preview.";
            }

            RefreshPreview();
        }
        catch (Exception ex)
        {
            AiAnswerBox.Text = $"Kon geen antwoord krijgen van de AI-server: {ex.Message}";
        }
        finally
        {
            ApplyAiXamlButton.IsEnabled = true;
            ApplyAiXamlButton.Content = originalContent;
        }
    }

    /// <summary>Models tend to wrap XAML in ```xml fences even when told not to - strip it if present.</summary>
    private static string StripMarkdownFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var afterOpeningFence = trimmed[(trimmed.IndexOf('\n') + 1)..];
        var closingFenceIndex = afterOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
        return (closingFenceIndex >= 0 ? afterOpeningFence[..closingFenceIndex] : afterOpeningFence).Trim();
    }

    private static string GenerateCardXaml(string title, string bodyText)
    {
        const string template = """
            <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Background="{DynamicResource SurfaceBrush}"
                    BorderBrush="{DynamicResource BorderBrush}"
                    BorderThickness="1"
                    CornerRadius="6"
                    Padding="16"
                    Width="280">
                <StackPanel>
                    <TextBlock Text="__TITLE__" FontSize="18" FontWeight="SemiBold" Foreground="{DynamicResource AccentBrush}" Margin="0,0,0,8" TextWrapping="Wrap" />
                    <TextBlock Text="__BODY__" Foreground="{DynamicResource TextMutedBrush}" TextWrapping="Wrap" />
                </StackPanel>
            </Border>
            """;

        return template
            .Replace("__TITLE__", SecurityElement.Escape(title))
            .Replace("__BODY__", SecurityElement.Escape(bodyText));
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
            // Seed a real, immediately-renderable Xaml so a brand new
            // component never starts out as a bare placeholder.
            App.Db.Components.Add(new StylebookComponent
            {
                Name = name,
                Region = region,
                Title = name,
                BodyText = "Voorbeeldinhoud",
                Xaml = GenerateCardXaml(name, "Voorbeeldinhoud"),
            });
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
