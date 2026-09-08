using System;
using System.Globalization;
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
using Stylebook.Data;
using Stylebook.Data.Entities;
using Stylebook.Playground.Theming;

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

        /// <summary>The hand-editable palette (colors/radii/typography) - see Theming/DbThemeBuilder.cs.</summary>
        Stylebook,
    }

    private sealed record ThemePresetOption(Theme Value, string Label);

    private static readonly ThemePresetOption[] ThemePresetOptions =
    [
        new ThemePresetOption(Theme.Lcars, "LCARS"),
        new ThemePresetOption(Theme.VisualStudio, "Visual Studio"),
    ];

    private BuilderMode _builderMode = BuilderMode.PageBuilder;
    private StylebookComponent? _lastSelectedComponent;
    private bool _initializing = true;

    /// <summary>An AI-proposed Xaml awaiting Overnemen/Negeren - see ShowProposal. Null when there's nothing to compare.</summary>
    private string? _proposedXaml;

    /// <summary>
    /// The AI conversation so far for the selected component (oldest
    /// first), so a follow-up question ("maak 'm nog scherper") builds on
    /// what the AI just answered instead of starting over each time.
    /// Cleared whenever the "current XAML" it was talking about goes
    /// away from under it - a different component gets selected, or the
    /// Componentenbouwer tab is left.
    /// </summary>
    private readonly List<(string Role, string Content)> _aiConversation = [];

    public MainWindow()
    {
        InitializeComponent();

        ThemePresetPicker.ItemsSource = ThemePresetOptions;
        ThemePresetPicker.DisplayMemberPath = nameof(ThemePresetOption.Label);
        ThemePresetPicker.SelectedIndex = DetectActiveThemeIndex(App.Db);
        _initializing = false;

        LoadComponentsByRegion();
        PageBuilderModeButton.IsChecked = true;
    }

    /// <summary>
    /// Which preset's AccentColor matches what's actually stored right
    /// now - so the picker reflects reality instead of always defaulting
    /// to "Visual Studio" regardless of the database. Falls back to
    /// Visual Studio (index 1) when nothing matches exactly (hand-edited
    /// tokens, or a value that doesn't correspond to any known preset).
    /// </summary>
    private static int DetectActiveThemeIndex(StylebookDbContext db)
    {
        var storedAccent = db.DesignTokens.FirstOrDefault(t => t.Name == "AccentColor")?.Value;
        if (storedAccent is not null)
        {
            for (var i = 0; i < ThemePresetOptions.Length; i++)
            {
                var presetAccent = DesignTokenCatalog.GetColors(ThemePresetOptions[i].Value)
                    .FirstOrDefault(c => c.Name == "AccentColor").Value
                    .ToString(CultureInfo.InvariantCulture);
                if (string.Equals(storedAccent, presetAccent, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return 1; // Visual Studio - the app's only intended style; also the seed default.
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

    /// <summary>
    /// Loads a known theme's values into the DesignTokens table (overwrites
    /// whatever is there, including hand-edits - a preset is a starting
    /// point, not a merge) and reapplies the live theme app-wide. Guarded
    /// by _initializing so setting the ComboBox's initial selection in the
    /// constructor doesn't clobber whatever was last saved to the database.
    /// </summary>
    private void ThemePreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || ThemePresetPicker.SelectedItem is not ThemePresetOption option)
        {
            return;
        }

        DbThemeBuilder.ApplyPreset(App.Db, option.Value);
        App.ReapplyLiveTheme();

        if (_builderMode == BuilderMode.Stylebook)
        {
            StylebookContent.Content = BuildStylebookPanel();
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

        if (mode != BuilderMode.ComponentBuilder)
        {
            ClearProposal();
            _aiConversation.Clear();
        }
        else
        {
            // Altijd opnieuw opbouwen (niet gecached) zodat een hand-edit op
            // het Stylebook-tabblad hier meteen klopt zodra je terugkomt.
            StyleReferenceBar.Content = BuildStyleReferenceBar();
        }

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
            ClearProposal(); // a pending AI proposal belongs to whichever component was selected when it was asked for.
            _aiConversation.Clear(); // same for the conversation itself - it was about that component's XAML.
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
            var element = CreateComponentVisual(_lastSelectedComponent);
            ApplyContainerSimulation(element);
            ComponentBuilderContent.Content = element;
        }
    }

    private void ContainerSimulation_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        RefreshPreview();
        RefreshProposalPreview();
    }

    private void ContainerSize_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing)
        {
            return;
        }

        TestContainerBorder.Width = ContainerWidthSlider.Value;
        TestContainerBorder.Height = ContainerHeightSlider.Value;
        ProposedTestContainerBorder.Width = ContainerWidthSlider.Value;
        ProposedTestContainerBorder.Height = ContainerHeightSlider.Value;
        ContainerSizeLabel.Text = $"Containerformaat: {ContainerWidthSlider.Value:0} × {ContainerHeightSlider.Value:0} px";
    }

    /// <summary>Re-renders the pending AI proposal (if there is one) so it picks up a Vast/Variabel or container-size change made while it's on screen - ORIGINEEL and AI-VOORSTEL always compare at the same settings.</summary>
    private void RefreshProposalPreview()
    {
        if (_proposedXaml is not { } xaml || _lastSelectedComponent is not { } component)
        {
            return;
        }

        var proposedElement = RenderXamlPreview(component.Name, xaml);
        ApplyContainerSimulation(proposedElement);
        ProposedPreviewContent.Content = proposedElement;
    }

    /// <summary>
    /// Simuleert dat dit component in een container staat die niet per se
    /// zijn eigen (XAML-)afmeting heeft: "Variabel" wist de eigen Width/
    /// Height van dit gerenderde exemplaar en rekt 'm uit tot
    /// TestContainerBorder's afmeting; "Vast" laat de eigen afmeting
    /// staan, gecentreerd. Raakt alleen dit preview-exemplaar aan - de
    /// opgeslagen Xaml verandert nooit.
    /// </summary>
    private void ApplyContainerSimulation(FrameworkElement element)
    {
        if (WidthModeCombo.SelectedIndex == 1)
        {
            element.Width = double.NaN;
            element.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            element.HorizontalAlignment = HorizontalAlignment.Center;
        }

        if (HeightModeCombo.SelectedIndex == 1)
        {
            element.Height = double.NaN;
            element.VerticalAlignment = VerticalAlignment.Stretch;
        }
        else
        {
            element.VerticalAlignment = VerticalAlignment.Center;
        }
    }

    /// <summary>
    /// Built from Stylebook.Data's live DesignTokens rows - not a static
    /// reference, an editor. Each row is a TextBox on the token's working
    /// Value, next to a small preview that updates as you type (before
    /// any save - see CreatePreview). "Opslaan" validates and writes
    /// Value only, so the wider app (which renders from DefaultValue, see
    /// DbThemeBuilder.Build) is unaffected; "Maak dit de standaard" does
    /// the same save AND copies Value into DefaultValue, which is the
    /// only thing that changes what the rest of the app looks like.
    /// Rebuilt after either action so previews reflect what's persisted.
    /// </summary>
    private FrameworkElement BuildStylebookPanel(string? statusMessage = null)
    {
        var tokens = App.Db.DesignTokens.AsEnumerable()
            .OrderBy(t => (int)t.Category)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var editors = new List<(DesignToken Token, TextBox Box)>();
        var stack = new StackPanel { Margin = new Thickness(24), MaxWidth = 560 };
        var selectableStyle = (Style)FindResource("SelectableTextStyle");

        var title = new TextBox { Text = "Stylebook", Style = selectableStyle, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) };
        stack.Children.Add(title);

        var hint = new TextBox
        {
            Text = "Pas een waarde aan - het voorbeeld ernaast volgt meteen. Kleuren als hex (#RRGGBB), overige " +
                   "als getal. 'Opslaan' bewaart je concept; 'Maak dit de standaard' laat de rest van de app het " +
                   "ook echt gebruiken.",
            Style = selectableStyle,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        };
        hint.SetResourceReference(TextBox.ForegroundProperty, "TextMutedBrush");
        stack.Children.Add(hint);

        var fieldStyle = (Style)FindResource("EditorFieldStyle");
        DesignTokenCategory? currentCategory = null;

        foreach (var token in tokens)
        {
            if (token.Category != currentCategory)
            {
                currentCategory = token.Category;
                stack.Children.Add(SectionLabel(CategoryLabel(token.Category)));
            }

            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var (preview, applyPreview) = CreatePreview(token.Category, token.Value);
            Grid.SetColumn(preview, 0);
            row.Children.Add(preview);

            var nameLabel = new TextBox { Text = token.Name, Style = selectableStyle, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(nameLabel, 1);
            row.Children.Add(nameLabel);

            var box = new TextBox { Text = token.Value, Style = fieldStyle, AcceptsReturn = false, Height = 28 };
            box.TextChanged += (_, _) => applyPreview(box.Text.Trim());
            Grid.SetColumn(box, 2);
            row.Children.Add(box);
            editors.Add((token, box));

            stack.Children.Add(row);
        }

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var saveButton = new Button { Content = "Opslaan", Style = (Style)FindResource("EditorActionButtonStyle") };
        saveButton.Click += (_, _) => SaveStylebookEdits(editors, promoteToDefault: false);
        buttonRow.Children.Add(saveButton);

        var promoteButton = new Button { Content = "Maak dit de standaard", Margin = new Thickness(8, 0, 0, 0), Style = (Style)FindResource("EditorActionButtonStyle") };
        promoteButton.Click += (_, _) => SaveStylebookEdits(editors, promoteToDefault: true);
        buttonRow.Children.Add(promoteButton);
        stack.Children.Add(buttonRow);

        if (!string.IsNullOrEmpty(statusMessage))
        {
            var status = new TextBox { Text = statusMessage, Style = selectableStyle, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
            status.SetResourceReference(TextBox.ForegroundProperty, "TextMutedBrush");
            stack.Children.Add(status);
        }

        var background = new Border { Child = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        background.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
        return background;
    }

    /// <summary>
    /// Builds a small live-preview visual for one token category, plus a
    /// closure that updates it in place from a new (possibly still
    /// mid-typing, possibly invalid) text value - invalid/unparseable
    /// input is silently ignored here, the preview just keeps showing the
    /// last good value until the input parses again. Validation/errors
    /// belong to SaveStylebookEdits, not to every keystroke.
    /// </summary>
    private static (FrameworkElement Element, Action<string> Apply) CreatePreview(DesignTokenCategory category, string initialValue)
    {
        switch (category)
        {
            case DesignTokenCategory.Color:
            {
                var swatch = new Border { Width = 24, Height = 24, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left };
                swatch.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                void Apply(string value)
                {
                    if (TryParseColor(value, out var color))
                    {
                        swatch.Background = new SolidColorBrush(color);
                    }
                }
                Apply(initialValue);
                return (swatch, Apply);
            }

            case DesignTokenCategory.Radius:
            {
                var box = new Border { Width = 40, Height = 24, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left };
                box.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
                box.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                void Apply(string value)
                {
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var px))
                    {
                        box.CornerRadius = new CornerRadius(px);
                    }
                }
                Apply(initialValue);
                return (box, Apply);
            }

            case DesignTokenCategory.Spacing:
            {
                var bar = new Border { Height = 14, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                bar.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
                void Apply(string value)
                {
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var px) && px >= 0)
                    {
                        bar.Width = Math.Max(px, 2);
                    }
                }
                Apply(initialValue);
                return (bar, Apply);
            }

            case DesignTokenCategory.FontSize:
            {
                var text = new TextBlock { Text = "Aa", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                text.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                void Apply(string value)
                {
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var px) && px > 0)
                    {
                        text.FontSize = px;
                    }
                }
                Apply(initialValue);
                return (text, Apply);
            }

            case DesignTokenCategory.FontFamily:
            {
                var text = new TextBlock { Text = "Abc", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                text.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                void Apply(string value)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        try
                        {
                            text.FontFamily = new FontFamily(value);
                        }
                        catch (Exception ex) when (ex is FormatException or ArgumentException)
                        {
                            // Keep showing the last good font while the user types an incomplete/invalid name.
                        }
                    }
                }
                Apply(initialValue);
                return (text, Apply);
            }

            default:
                return (new TextBlock(), _ => { });
        }
    }

    /// <summary>
    /// Alleen-lezen naslag van elk stijl-token (icoon/swatch + naam, geen
    /// waarde, geen bewerken) voor de balk links van de preview in
    /// Componentenbouwer - hergebruikt dezelfde icoon-generatie als het
    /// Stylebook-tabblad (CreatePreview), alleen zonder de Apply-kant.
    /// </summary>
    private FrameworkElement BuildStyleReferenceBar()
    {
        var tokens = App.Db.DesignTokens.AsEnumerable()
            .OrderBy(t => (int)t.Category)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var stack = new StackPanel();
        DesignTokenCategory? currentCategory = null;

        foreach (var token in tokens)
        {
            if (token.Category != currentCategory)
            {
                currentCategory = token.Category;
                stack.Children.Add(SectionLabel(CategoryLabel(token.Category)));
            }

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

            var (preview, _) = CreatePreview(token.Category, token.Value);
            preview.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(preview);

            var nameLabel = new TextBox
            {
                Text = token.Name,
                Style = (Style)FindResource("SelectableTextStyle"),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
            };
            row.Children.Add(nameLabel);

            stack.Children.Add(row);
        }

        return stack;
    }

    private static string CategoryLabel(DesignTokenCategory category) => category switch
    {
        DesignTokenCategory.Color => "Kleuren",
        DesignTokenCategory.Radius => "Hoekronding",
        DesignTokenCategory.Spacing => "Afstand",
        DesignTokenCategory.FontSize => "Tekstgrootte",
        DesignTokenCategory.FontFamily => "Lettertype",
        _ => category.ToString(),
    };

    private TextBox SectionLabel(string text)
    {
        var label = new TextBox { Text = text.ToUpperInvariant(), Style = (Style)FindResource("SelectableTextStyle"), FontSize = 12, Margin = new Thickness(0, 20, 0, 8) };
        label.SetResourceReference(TextBox.ForegroundProperty, "TextMutedBrush");
        return label;
    }

    /// <summary>
    /// Validates each row (hex for Color, a parseable number for the
    /// rest) before writing anything - an invalid row is reported and
    /// skipped rather than silently discarded or corrupting the theme.
    /// Always writes Value; only promoteToDefault (the "Maak dit de
    /// standaard" button) also copies it into DefaultValue and reapplies
    /// the app-wide live theme - see DesignToken's class comment.
    /// </summary>
    private void SaveStylebookEdits(List<(DesignToken Token, TextBox Box)> editors, bool promoteToDefault)
    {
        var invalid = new List<string>();

        foreach (var (token, box) in editors)
        {
            var value = box.Text.Trim();
            var isValid = token.Category switch
            {
                DesignTokenCategory.Color => TryParseColor(value, out _),
                DesignTokenCategory.FontFamily => value.Length > 0,
                _ => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
            };

            if (isValid)
            {
                token.Value = value;
                if (promoteToDefault)
                {
                    token.DefaultValue = value;
                }
            }
            else
            {
                invalid.Add(token.Name);
            }
        }

        App.Db.SaveChanges();

        if (promoteToDefault)
        {
            App.ReapplyLiveTheme();
        }

        var action = promoteToDefault ? "Opgeslagen en als standaard ingesteld." : "Opgeslagen.";
        var message = invalid.Count == 0
            ? action
            : $"{action} Behalve: {string.Join(", ", invalid)} (ongeldige waarde, oude waarde behouden).";

        StylebookContent.Content = BuildStylebookPanel(message);
    }

    private static bool TryParseColor(string value, out Color color)
    {
        try
        {
            if (ColorConverter.ConvertFromString(value) is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch (FormatException)
        {
        }

        color = default;
        return false;
    }

    /// <summary>
    /// What a catalog entry (Stylebook.Data.Entities.StylebookComponent)
    /// looks like when placed somewhere: its stored Xaml, parsed live via
    /// RenderXamlPreview - never lets bad markup take the app down with
    /// it, see that method.
    /// </summary>
    private static FrameworkElement CreateComponentVisual(StylebookComponent? component)
    {
        if (component is null)
        {
            return Placeholder("(leeg)", "TextMutedBrush", "BorderBrush");
        }

        return RenderXamlPreview(component.Name, component.Xaml);
    }

    /// <summary>Same rendering CreateComponentVisual uses, but for a raw Xaml string not (yet) attached to a saved component - see ShowProposal.</summary>
    private static FrameworkElement RenderXamlPreview(string name, string? xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
        {
            return Placeholder(name, "TextPrimaryBrush", "BorderBrush");
        }

        try
        {
            if (XamlReader.Parse(xaml) is FrameworkElement parsed)
            {
                return parsed;
            }

            return Placeholder($"'{name}' is geen FrameworkElement", "TextMutedBrush", "BorderBrush");
        }
        catch (Exception ex)
        {
            return Placeholder($"XAML-fout in '{name}': {ex.Message}", "TextMutedBrush", "AccentBrush");
        }
    }

    private static FrameworkElement Placeholder(string text, string foregroundKey, string borderKey)
    {
        var label = new TextBox { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 };
        label.SetResourceReference(FrameworkElement.StyleProperty, "SelectableTextStyle");
        label.SetResourceReference(TextBox.ForegroundProperty, foregroundKey);
        label.SetResourceReference(TextBox.FontFamilyProperty, "AppFontFamily");
        label.SetResourceReference(TextBox.FontSizeProperty, "FontSizeBody");

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
    /// Advanced-edit path: a hand-typed XAML change goes through the same
    /// voorstel-vergelijken-Overnemen flow as an AI answer (ShowProposal)
    /// instead of saving straight away - a typo never lands in the
    /// database or destabilizes the live preview; you see it next to the
    /// last-known-good version first and explicitly accept it.
    /// </summary>
    private void SaveXaml_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSelectedComponent is not { } component)
        {
            return;
        }

        XamlErrorText.Visibility = Visibility.Collapsed;
        ShowProposal(component.Name, component.Xaml ?? string.Empty, ComponentXamlBox.Text);
        AiAnswerBox.Text = "Vergelijk hiernaast met het origineel, en klik Overnemen om te bewaren.";
    }

    /// <summary>
    /// Base instruction for every AI call: the Stylebook's current,
    /// possibly hand-edited token values (see DbThemeBuilder.DescribeForAi
    /// - this is the "kies daaruit" palette, not a suggestion the model
    /// can ignore). Deliberately does NOT embed "the current XAML" here -
    /// that goes into the outgoing user question instead (see
    /// AskAi_Click), explicitly tied to "wat nu op het scherm staat" for
    /// this specific turn, rather than living in the system prompt where
    /// it'd compete with what the conversation history already shows.
    /// </summary>
    private string BuildAiSystemPrompt()
    {
        return "Je bent een assistent die WPF-XAML-componenten voor Stylebook bouwt en aanpast.\n" +
               DbThemeBuilder.DescribeForAi(App.Db);
    }

    /// <summary>
    /// Asks the AI for the updated Xaml reflecting the question/request,
    /// plus a short summary of what it changed. When the Xaml part
    /// actually parses, shows it as a proposal next to the original (see
    /// ShowProposal) instead of applying it - "Overnemen" is the only
    /// thing that ever saves it - and puts the summary (not the raw
    /// markup) in the answer box, so you can read what changed without
    /// having to diff the Xaml yourself. When the answer isn't in the
    /// requested Xaml format (the model explained instead of producing
    /// markup, or a genuinely informational question was asked), falls
    /// back to just showing the raw text - one button handles both, no
    /// need to guess which of two buttons a given question needs.
    /// </summary>
    private async void AskAi_Click(object sender, RoutedEventArgs e)
    {
        var question = AiQuestionBox.Text.Trim();
        if (question.Length == 0 || _lastSelectedComponent is not { } component)
        {
            return;
        }

        // Het antwoord (XAML) zoals nu op het scherm staat gaat expliciet
        // MEE met de vraag zelf, niet (alleen) in de systeemprompt - het
        // nog-niet-geaccepteerde voorstel als er een is (zodat een
        // vervolgvraag daarop doorbouwt), anders de opgeslagen versie.
        // Vastleggen VOORDAT ClearProposal() zo dadelijk _proposedXaml wist.
        var currentXaml = _proposedXaml ?? component.Xaml ?? string.Empty;

        var originalContent = AskAiButton.Content;
        AskAiButton.IsEnabled = false;
        AskAiButton.Content = "Bezig...";
        AiAnswerBox.Text = string.Empty;
        ClearProposal();

        try
        {
            var systemPrompt = BuildAiSystemPrompt() +
                "\nAntwoord in exact dit formaat, zonder markdown-codeblokken:\n" +
                "SAMENVATTING: <een korte zin die samenvat wat je hebt aangepast>\n" +
                "XAML:\n<de volledige, aangepaste XAML>";

            var questionWithScreenXaml = currentXaml.Length > 0
                ? $"Dit is de huidige XAML van '{component.Name}', zoals nu op het scherm staat:\n{currentXaml}\n\nVraag: {question}"
                : question;

            var history = new List<(string Role, string Content)>(_aiConversation) { ("user", questionWithScreenXaml) };
            var raw = await App.Ai.AskAsync(systemPrompt, history);
            var hasSummaryFormat = TryExtractSummaryAndXaml(raw, out var summary, out var xamlPart);
            var xaml = StripMarkdownFence(hasSummaryFormat ? xamlPart : raw);

            // In de bewaarde geschiedenis blijft de vraag kort (zonder de
            // meegestuurde XAML) - die stond toch al in het vorige
            // assistant-antwoord, dus dat zou de geschiedenis nodeloos
            // opblazen bij elke vervolgvraag.
            _aiConversation.Add(("user", question));
            _aiConversation.Add(("assistant", raw));

            if (TryParseXaml(xaml, out _))
            {
                ShowProposal(component.Name, component.Xaml ?? string.Empty, xaml);
                AiAnswerBox.Text = hasSummaryFormat && summary.Length > 0 ? summary : xaml;
            }
            else
            {
                AiAnswerBox.Text = raw;
            }
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

    /// <summary>Splits a "SAMENVATTING: ...\nXAML:\n..." formatted answer apart. Returns false (xaml = the whole answer) when the model didn't follow the format, so the caller can still try to use it as-is.</summary>
    private static bool TryExtractSummaryAndXaml(string answer, out string summary, out string xaml)
    {
        const string xamlMarker = "XAML:";
        var xamlIndex = answer.IndexOf(xamlMarker, StringComparison.OrdinalIgnoreCase);
        if (xamlIndex < 0)
        {
            summary = string.Empty;
            xaml = answer;
            return false;
        }

        const string summaryMarker = "SAMENVATTING:";
        var beforeXaml = answer[..xamlIndex];
        var summaryIndex = beforeXaml.IndexOf(summaryMarker, StringComparison.OrdinalIgnoreCase);
        summary = (summaryIndex >= 0 ? beforeXaml[(summaryIndex + summaryMarker.Length)..] : beforeXaml).Trim();
        xaml = answer[(xamlIndex + xamlMarker.Length)..].Trim();
        return true;
    }

    private static bool TryParseXaml(string xaml, out FrameworkElement? element)
    {
        try
        {
            element = XamlReader.Parse(xaml) as FrameworkElement;
            return element is not null;
        }
        catch (Exception ex) when (ex is XamlParseException or System.Xml.XmlException)
        {
            element = null;
            return false;
        }
    }

    /// <summary>Renders the AI's proposed Xaml next to the current component (same Vast/Variabel + testcontainer-afmeting as ORIGINEEL, zie ApplyContainerSimulation), shows both versions' source underneath, and reveals Overnemen/Negeren.</summary>
    private void ShowProposal(string componentName, string originalXaml, string proposedXaml)
    {
        _proposedXaml = proposedXaml;
        var proposedElement = RenderXamlPreview(componentName, proposedXaml);
        ApplyContainerSimulation(proposedElement);
        ProposedPreviewContent.Content = proposedElement;
        ProposedColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
        ProposalLabelsRow.Visibility = Visibility.Visible;
        ProposedTestContainerBorder.Visibility = Visibility.Visible;
        OriginalXamlText.Text = originalXaml;
        ProposedXamlText.Text = proposedXaml;
        XamlComparisonRow.Visibility = Visibility.Visible;
        XamlComparisonSplitter.Visibility = Visibility.Visible;
        ProposalActionsRow.Visibility = Visibility.Visible;
    }

    private void ClearProposal()
    {
        _proposedXaml = null;
        ProposedColumnDefinition.Width = new GridLength(0);
        ProposalLabelsRow.Visibility = Visibility.Collapsed;
        ProposedTestContainerBorder.Visibility = Visibility.Collapsed;
        ProposedPreviewContent.Content = null;
        OriginalXamlText.Text = string.Empty;
        ProposedXamlText.Text = string.Empty;
        XamlComparisonRow.Visibility = Visibility.Collapsed;
        XamlComparisonSplitter.Visibility = Visibility.Collapsed;
        ProposalActionsRow.Visibility = Visibility.Collapsed;
    }

    /// <summary>Commits the pending AI proposal exactly like a manual "Opslaan en toepassen" would - errors included, so a bad answer is visible and recoverable rather than silently discarded.</summary>
    private void AcceptProposal_Click(object sender, RoutedEventArgs e)
    {
        if (_proposedXaml is not { } xaml || _lastSelectedComponent is not { } component)
        {
            return;
        }

        ComponentXamlBox.Text = xaml;
        component.Xaml = xaml;
        App.Db.SaveChanges();

        try
        {
            XamlReader.Parse(xaml);
            XamlErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception parseEx)
        {
            XamlErrorText.Text = parseEx.Message;
            XamlErrorText.Visibility = Visibility.Visible;
        }

        ClearProposal();
        AiAnswerBox.Text = "Voorstel overgenomen en opgeslagen.";
        RefreshPreview();
    }

    private void DiscardProposal_Click(object sender, RoutedEventArgs e)
    {
        ClearProposal();
        // Zet de XAML-editor terug naar de opgeslagen versie - relevant
        // bij een handmatige wijziging via SaveXaml_Click; een AI-voorstel
        // raakte de box toch al nooit aan, dus daar is dit een no-op.
        ComponentXamlBox.Text = _lastSelectedComponent?.Xaml ?? string.Empty;
        AiAnswerBox.Text = "Voorstel genegeerd.";
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
