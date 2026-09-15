using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Stylebook.Components.Theming;
using Stylebook.Data.Entities;
using Stylebook.Playground.Theming;
using ComponentsTheme = Stylebook.Components.Theming.Theme;
using DataTheme = Stylebook.Data.Entities.Theme;

namespace Stylebook.Playground;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private enum BuilderMode
    {
        /// <summary>The hand-editable palette (colors/radii/typography) - see Theming/DbThemeBuilder.cs.</summary>
        Stylebook,

        /// <summary>Alleen de losse bouwstenen uit Stylebook.Components/Controls, één tegelijk geïsoleerd - zie LibraryComponents_SelectionChanged.</summary>
        Controls,

        /// <summary>Meerdere Controls samen op de echte afmeting van één regio, slepend te positioneren - zie RebuildRegionCanvas. Alleen voorbeeld, schrijft niets weg.</summary>
        Regions,

        /// <summary>Per regio een component kiezen en de complete pagina in een echte Basis bekijken - zie LibraryPagePicker_SelectionChanged.</summary>
        Apps,
    }

    /// <summary>One UserControl type found by DiscoverLibraryControls - ToString() drives LibraryComponents' display (dat menu toont alleen Controls, dus daar is de regionaam overbodig). Region is null for a generic Stylebook.Components/Controls building block, set for a region-specific one.</summary>
    private sealed record LibraryEntry(ComponentRegion? Region, Type Type)
    {
        public override string ToString() => Type.Name;
    }

    private sealed record ThemePresetOption(ComponentsTheme Value, string Label);

    private static readonly ThemePresetOption[] ThemePresetOptions =
    [
        new ThemePresetOption(ComponentsTheme.Lcars, "LCARS"),
        new ThemePresetOption(ComponentsTheme.VisualStudio, "Visual Studio"),
    ];

    private BuilderMode _builderMode = BuilderMode.Controls;
    private bool _initializing = true;

    /// <summary>Eén control op het Regions-canvas: waar hij staat, hoe hoog hij in de stapel ligt (hoger = bovenop), en hoeveel pixels er van elke kant afgesneden zijn.</summary>
    private sealed class PlacedControl
    {
        public required Type Type { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public int Z { get; set; }
        public double TrimLeft { get; set; }
        public double TrimTop { get; set; }
        public double TrimRight { get; set; }
        public double TrimBottom { get; set; }
    }

    /// <summary>
    /// De samenstelling waar je in de Regions-stand aan werkt: welke Controls
    /// erop staan (één per soort - de aanvinkvakjes) en hoe ze geplaatst
    /// zijn. Blijft staan als je van regio of menu wisselt; bewaren op schijf
    /// gebeurt alleen via "Opslaan" onder Concepten.
    /// </summary>
    private readonly Dictionary<Type, PlacedControl> _regionComposerPlaced = [];
    private Type? _regionComposerSelectedType;
    private IReadOnlyList<LibraryEntry> _regionComposerControls = [];
    private bool _fillingRegionTrimFields;

    private FrameworkElement? _regionDragElement;
    private Point _regionDragStart;
    private Point _regionDragOrigin;

    public MainWindow()
    {
        InitializeComponent();

        ThemePresetPicker.ItemsSource = ThemePresetOptions;
        ThemePresetPicker.DisplayMemberPath = nameof(ThemePresetOption.Label);
        ThemePresetPicker.SelectedIndex = Array.FindIndex(
            ThemePresetOptions, o => string.Equals(o.Value.ToString(), App.CurrentTheme.ToString(), StringComparison.Ordinal));
        _initializing = false;

        ControlsModeButton.IsChecked = true;
    }

    /// <summary>
    /// Switches which theme is active app-wide (App.SwitchTheme) - each
    /// theme keeps its own stored tokens, hand-edits included, so this is
    /// non-destructive; it does NOT reset anything back to a preset.
    /// Guarded by _initializing so setting the ComboBox's initial
    /// selection in the constructor doesn't re-trigger a switch.
    /// </summary>
    private void ThemePreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || ThemePresetPicker.SelectedItem is not ThemePresetOption option)
        {
            return;
        }

        App.SwitchTheme(Enum.Parse<DataTheme>(option.Value.ToString()));

        if (_builderMode == BuilderMode.Stylebook)
        {
            StylebookContent.Content = BuildStylebookPanel();
        }
    }

    /// <summary>
    /// The one deliberate, destructive counterpart to the (now
    /// non-destructive) theme switcher - overwrites every token of the
    /// CURRENTLY active theme back to its built-in preset values, same as
    /// ApplyPreset always did before switching stopped doing this
    /// automatically. Confirmed first: there's no undo.
    /// </summary>
    private void ResetThemeToPreset_Click(object sender, RoutedEventArgs e)
    {
        var themeLabel = ThemePresetOptions.First(o => string.Equals(o.Value.ToString(), App.CurrentTheme.ToString(), StringComparison.Ordinal)).Label;
        var confirmed = MessageBox.Show(
            $"Alle handmatige aanpassingen aan '{themeLabel}' gaan verloren en worden teruggezet naar de standaardwaarden. Doorgaan?",
            "Herstel naar standaard",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

        if (!confirmed)
        {
            return;
        }

        DbThemeBuilder.ApplyPreset(App.Db, App.CurrentTheme);
        App.ReapplyLiveTheme();

        if (_builderMode == BuilderMode.Stylebook)
        {
            StylebookContent.Content = BuildStylebookPanel($"'{themeLabel}' hersteld naar standaardwaarden.");
        }
    }

    private void StylebookMode_Checked(object sender, RoutedEventArgs e) => SetBuilderMode(BuilderMode.Stylebook);

    private void ControlsMode_Checked(object sender, RoutedEventArgs e) => SetBuilderMode(BuilderMode.Controls);

    private void RegionsMode_Checked(object sender, RoutedEventArgs e) => SetBuilderMode(BuilderMode.Regions);

    private void AppsMode_Checked(object sender, RoutedEventArgs e) => SetBuilderMode(BuilderMode.Apps);

    private void SetBuilderMode(BuilderMode mode)
    {
        _builderMode = mode;

        StylebookContent.Visibility = mode == BuilderMode.Stylebook ? Visibility.Visible : Visibility.Collapsed;
        LibraryCanvasArea.Visibility = mode == BuilderMode.Stylebook ? Visibility.Collapsed : Visibility.Visible;
        LibraryComponentPanel.Visibility = mode == BuilderMode.Controls ? Visibility.Visible : Visibility.Collapsed;
        LibraryRegionPanel.Visibility = mode == BuilderMode.Regions ? Visibility.Visible : Visibility.Collapsed;
        LibraryPagePanel.Visibility = mode == BuilderMode.Apps ? Visibility.Visible : Visibility.Collapsed;

        if (mode == BuilderMode.Stylebook)
        {
            StylebookContent.Content ??= BuildStylebookPanel();
        }
        else
        {
            // Altijd opnieuw opbouwen (niet gecached) - een net toegevoegde
            // UserControl-klasse (herbouwd project) moet meteen verschijnen
            // zonder Stylebook.Playground te hoeven herstarten.
            LoadLibraryControls();
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
        var tokens = App.Db.DesignTokens.Where(t => t.Theme == App.CurrentTheme).AsEnumerable()
            .OrderBy(t => (int)t.Category)
            .ThenBy(TokenSortValue)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var editors = new List<(DesignToken Token, Func<string> GetValue)>();
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

            if (token.Category == DesignTokenCategory.FontFamily)
            {
                var combo = new ComboBox { Height = 28 };
                foreach (var option in DesignTokenCatalog.FontFamilyOptions)
                {
                    combo.Items.Add(option.DisplayName);
                }
                var current = DesignTokenCatalog.FontFamilyOptions.FirstOrDefault(o => o.Source == token.Value);
                combo.SelectedItem = current.DisplayName ?? DesignTokenCatalog.FontFamilyOptions[0].DisplayName;
                combo.SelectionChanged += (_, _) =>
                {
                    var source = DesignTokenCatalog.FontFamilyOptions
                        .FirstOrDefault(o => o.DisplayName == (string)combo.SelectedItem).Source
                        ?? DesignTokenCatalog.FontFamilyOptions[0].Source;
                    applyPreview(source);
                };
                Grid.SetColumn(combo, 2);
                row.Children.Add(combo);
                editors.Add((token, () => DesignTokenCatalog.FontFamilyOptions
                    .FirstOrDefault(o => o.DisplayName == (string)combo.SelectedItem).Source
                    ?? DesignTokenCatalog.FontFamilyOptions[0].Source));
            }
            else
            {
                var box = new TextBox { Text = token.Value, Style = fieldStyle, AcceptsReturn = false, Height = 28 };
                box.TextChanged += (_, _) => applyPreview(box.Text.Trim());
                Grid.SetColumn(box, 2);
                row.Children.Add(box);
                editors.Add((token, () => box.Text.Trim()));
            }

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

    private static double TokenSortValue(DesignToken token) =>
        double.TryParse(token.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

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
    private void SaveStylebookEdits(List<(DesignToken Token, Func<string> GetValue)> editors, bool promoteToDefault)
    {
        var invalid = new List<string>();

        foreach (var (token, getValue) in editors)
        {
            var value = getValue();
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

    /// <summary>Regio-afhankelijke containerafmeting, afgeleid van de ECHTE afmeting in Basis.xaml (Header=210 hoog, Footer=65 hoog, Menu=256 breed, Actie=48 breed, Inhoud volledig flexibel). Een Control (region null) heeft geen Basis-slot en gebruikt een generieke standaard.</summary>
    private static (ContainerSizeMode WidthMode, double Width, ContainerSizeMode HeightMode, double Height) RegionDefaultContainerSize(ComponentRegion? region) => region switch
    {
        ComponentRegion.Header => (ContainerSizeMode.Variable, 1200, ContainerSizeMode.Fixed, 210),
        ComponentRegion.Menu => (ContainerSizeMode.Fixed, 256, ContainerSizeMode.Variable, 700),
        ComponentRegion.Inhoud => (ContainerSizeMode.Variable, 1200, ContainerSizeMode.Variable, 700),
        ComponentRegion.Actie => (ContainerSizeMode.Fixed, 48, ContainerSizeMode.Variable, 700),
        ComponentRegion.Footer => (ContainerSizeMode.Variable, 1200, ContainerSizeMode.Fixed, 65),
        null => (ContainerSizeMode.Fixed, 400, ContainerSizeMode.Fixed, 260),
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
    };

    /// <summary>
    /// "Variabel" wist de eigen Width/Height van dit exemplaar en rekt
    /// 'm uit (Stretch) tot wat de omgeving 'm geeft. "Vast" met een
    /// expliciete fixedWidth/fixedHeight zet die waarde er hard op; "Vast"
    /// zonder waarde (null) laat de natuurlijke (XAML-eigen) afmeting
    /// staan.
    /// </summary>
    private static void ApplySizeConstraints(FrameworkElement element, ContainerSizeMode widthMode, double? fixedWidth, ContainerSizeMode heightMode, double? fixedHeight)
    {
        if (widthMode == ContainerSizeMode.Variable)
        {
            element.Width = double.NaN;
            element.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            element.Width = fixedWidth ?? double.NaN;
            element.HorizontalAlignment = HorizontalAlignment.Center;
        }

        if (heightMode == ContainerSizeMode.Variable)
        {
            element.Height = double.NaN;
            element.VerticalAlignment = VerticalAlignment.Stretch;
        }
        else
        {
            element.Height = fixedHeight ?? double.NaN;
            element.VerticalAlignment = VerticalAlignment.Center;
        }
    }

    /// <summary>
    /// Vindt elke publieke, niet-abstracte UserControl-subklasse in
    /// Stylebook.Components waarvan de namespace eindigt op "Controls"
    /// (bv. Stylebook.Components.Controls.Card -> een generieke
    /// bouwsteen, Region null) of op een ComponentRegion-naam (bv.
    /// Stylebook.Components.Regions.Header -> regio Header) - dat is
    /// meteen de Controls/Regio-tagging, zonder aparte attributen nodig.
    /// Puur reflectie over de al geladen assembly, geen database erbij
    /// betrokken.
    /// Slaat "...Base"-klassen over (bv. HeaderBase) - dat zijn lege
    /// kopieer-startpunten voor een nieuw component (zie Regions/Header/
    /// HeaderBase.xaml), geen afgerond component om te bekijken.
    /// </summary>
    private static IEnumerable<LibraryEntry> DiscoverLibraryControls()
    {
        var assembly = typeof(Stylebook.Components.Controls.Basis).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsPublic || type.IsAbstract || !typeof(UserControl).IsAssignableFrom(type) ||
                type.Name.EndsWith("Base", StringComparison.Ordinal))
            {
                continue;
            }

            var lastNamespaceSegment = type.Namespace?.Split('.').LastOrDefault();
            if (lastNamespaceSegment == "Controls")
            {
                yield return new LibraryEntry(null, type);
            }
            else if (lastNamespaceSegment is not null && Enum.TryParse<ComponentRegion>(lastNamespaceSegment, out var region))
            {
                yield return new LibraryEntry(region, type);
            }
        }
    }

    /// <summary>Vult de drie menus met de op dit moment gevonden echte UserControls - zie DiscoverLibraryControls.</summary>
    private void LoadLibraryControls()
    {
        var discovered = DiscoverLibraryControls().ToList();

        // Het Controls-menu toont bewust alleen de losse bouwstenen
        // (Region null): regiospecifieke componenten horen in Regions/Apps
        // thuis, niet in deze lijst. Alfabetisch, zoals Solution Explorer.
        var controls = discovered
            .Where(entry => entry.Region is null)
            .OrderBy(entry => entry.Type.Name, StringComparer.Ordinal)
            .ToList();

        LibraryComponents.ItemsSource = controls;
        LibraryPreviewContent.Content = null;
        LibraryContainerSizeLabel.Text = "Containerformaat: -";

        LoadRegionComposer(controls);

        LoadLibraryPagePicker(LibraryPageHeaderPicker, ComponentRegion.Header, discovered);
        LoadLibraryPagePicker(LibraryPageMenuPicker, ComponentRegion.Menu, discovered);
        LoadLibraryPagePicker(LibraryPageInhoudPicker, ComponentRegion.Inhoud, discovered);
        LoadLibraryPagePicker(LibraryPageActiePicker, ComponentRegion.Actie, discovered);
        LoadLibraryPagePicker(LibraryPageFooterPicker, ComponentRegion.Footer, discovered);

        LoadPageDraftApps();
    }

    /// <summary>
    /// Map waar Pagina-concepten als los JSON-bestand bewaard worden,
    /// genest als App/Pagina.json - WELK component per regio gekozen is,
    /// geen gegenereerd bestand. Ligt in de brontree (niet de
    /// build-output) zodat concepten meegaan in git, net als
    /// Scratch/ComponentDesigner.xaml - vandaar het 3x "omhoog" vanaf de
    /// build-output.
    /// </summary>
    private static readonly string PageDraftsDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Scratch", "PageDrafts"));

    /// <summary>Wat er per regio gekozen was toen een Pagina-concept werd opgeslagen: de Key van een LibraryPageOption (Type.FullName, "regioconcept:&lt;naam&gt;", of null = "(leeg)").</summary>
    private sealed record PageDraft(string? Header, string? Menu, string? Inhoud, string? Actie, string? Footer);

    /// <summary>Eén echte, al gebouwde pagina in Stylebook.Components/Apps/&lt;App&gt;/&lt;Pagina&gt;.xaml.</summary>
    private sealed record AppPageEntry(string App, string Page);

    /// <summary>
    /// Vindt elke publieke, niet-abstracte UserControl-subklasse waarvan
    /// de namespace op "Apps.&lt;AppNaam&gt;" eindigt (bv.
    /// Stylebook.Components.Apps.Jabasoft.Hoofdscherm -> App "Jabasoft",
    /// Pagina "Hoofdscherm") - zelfde reflectie-aanpak als
    /// DiscoverLibraryControls, nu op de Apps-laag in plaats van Regions.
    /// </summary>
    private static IEnumerable<AppPageEntry> DiscoverAppPages()
    {
        var assembly = typeof(Stylebook.Components.Controls.Basis).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsPublic || type.IsAbstract || !typeof(UserControl).IsAssignableFrom(type))
            {
                continue;
            }

            var segments = type.Namespace?.Split('.');
            if (segments is { Length: >= 2 } && segments[^2] == "Apps")
            {
                yield return new AppPageEntry(segments[^1], type.Name);
            }
        }
    }

    /// <summary>
    /// Vult PageDraftAppBox met elke App die al een echte pagina heeft
    /// (Stylebook.Components/Apps/&lt;App&gt;/) EN elke App die alleen nog
    /// maar een opgeslagen concept heeft (Scratch/PageDrafts/&lt;App&gt;/) -
    /// dus ook "Jabasoft" staat er al meteen in, ook zonder concept.
    /// </summary>
    private void LoadPageDraftApps()
    {
        var draftApps = Directory.Exists(PageDraftsDirectory)
            ? Directory.GetDirectories(PageDraftsDirectory).Select(Path.GetFileName)
            : [];
        var realApps = DiscoverAppPages().Select(entry => entry.App);

        PageDraftAppBox.ItemsSource = draftApps.Concat(realApps)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        LoadPageDraftPages();
    }

    /// <summary>Zelfde combinatie als LoadPageDraftApps, maar dan voor de pagina's van de App die nu in PageDraftAppBox staat (getypt of gekozen).</summary>
    private void LoadPageDraftPages()
    {
        var app = PageDraftAppBox.Text.Trim();

        var appDirectory = Path.Combine(PageDraftsDirectory, app);
        var draftPages = app.Length > 0 && Directory.Exists(appDirectory)
            ? Directory.GetFiles(appDirectory, "*.json").Select(Path.GetFileNameWithoutExtension)
            : [];
        var realPages = DiscoverAppPages().Where(entry => entry.App == app).Select(entry => entry.Page);

        PageDraftPageBox.ItemsSource = draftPages.Concat(realPages)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>App gewijzigd (gekozen uit de lijst, of getypt en de focus kwijt) - ververst welke pagina's er voor die app bestaan en probeert meteen te laden.</summary>
    private void PageDraftApp_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        LoadPageDraftPages();
        Dispatcher.BeginInvoke(new Action(TryLoadPageDraft), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>Pagina gewijzigd (gekozen uit de lijst, of getypt en de focus kwijt) - probeert het concept voor de huidige App+Pagina te laden.</summary>
    private void PageDraftPage_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        // Dispatcher.BeginInvoke (ipv rechtstreeks aanroepen): bij een klik
        // op een item in de vervolgkeuzelijst van een IsEditable ComboBox
        // is .Text op het moment van SelectionChanged nog niet altijd
        // bijgewerkt - TryLoadPageDraft zou dan een lege App/Pagina lezen
        // en meteen (ten onrechte) stoppen. Background-prioriteit wacht tot
        // de ComboBox zijn Text heeft bijgewerkt voordat we die uitlezen.
        Dispatcher.BeginInvoke(new Action(TryLoadPageDraft), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Laadt het concept voor de huidige App+Pagina-tekst terug in de vijf
    /// keuzelijsten, als dat bestand bestaat. Bestaat er geen concept, maar
    /// is App+Pagina wel een al gebouwde ECHTE pagina (Stylebook.Components/
    /// Apps/&lt;App&gt;/&lt;Pagina&gt;.xaml, zie DiscoverAppPages) - die complete
    /// pagina dan rechtstreeks tonen (ze bestaat niet als vijf losse
    /// regio-keuzes, dus kan niet in de pickers geladen worden). Is het
    /// allebei niet, dan gebeurt er niets (zo kun je een NIEUWE naam
    /// intypen zonder de huidige keuzes kwijt te raken).
    /// </summary>
    private void TryLoadPageDraft()
    {
        var app = PageDraftAppBox.Text.Trim();
        var page = PageDraftPageBox.Text.Trim();
        if (app.Length == 0 || page.Length == 0)
        {
            return;
        }

        var path = Path.Combine(PageDraftsDirectory, app, $"{page}.json");
        if (!File.Exists(path))
        {
            var realPage = DiscoverAppPages().FirstOrDefault(entry => entry.App == app && entry.Page == page);
            if (realPage is not null)
            {
                ShowRealAppPage(realPage);
            }

            return;
        }

        var draft = JsonSerializer.Deserialize<PageDraft>(File.ReadAllText(path));
        if (draft is null)
        {
            return;
        }

        LibraryPagePreviewHost.Content = LibraryPageBasis;
        SelectPageDraftOption(LibraryPageHeaderPicker, draft.Header);
        SelectPageDraftOption(LibraryPageMenuPicker, draft.Menu);
        SelectPageDraftOption(LibraryPageInhoudPicker, draft.Inhoud);
        SelectPageDraftOption(LibraryPageActiePicker, draft.Actie);
        SelectPageDraftOption(LibraryPageFooterPicker, draft.Footer);
    }

    /// <summary>
    /// Toont een echte, complete Apps-pagina rechtstreeks in de preview
    /// (in plaats van via de vijf regio-slots van LibraryPageBasis, want
    /// zo'n pagina IS al één samengestelde UserControl). De vijf pickers
    /// gaan terug naar "(leeg)" - ze zijn niet van toepassing op wat nu
    /// getoond wordt.
    /// </summary>
    private void ShowRealAppPage(AppPageEntry realPage)
    {
        SelectPageDraftOption(LibraryPageHeaderPicker, null);
        SelectPageDraftOption(LibraryPageMenuPicker, null);
        SelectPageDraftOption(LibraryPageInhoudPicker, null);
        SelectPageDraftOption(LibraryPageActiePicker, null);
        SelectPageDraftOption(LibraryPageFooterPicker, null);

        var assembly = typeof(Stylebook.Components.Controls.Basis).Assembly;
        var type = assembly.GetTypes().First(t => t.Namespace?.EndsWith($"Apps.{realPage.App}", StringComparison.Ordinal) == true && t.Name == realPage.Page);
        LibraryPagePreviewHost.Content = Activator.CreateInstance(type);
    }

    /// <summary>
    /// Zoekt de LibraryPageOption in een regio-keuzelijst die bij de
    /// bewaarde Key hoort (Type.FullName of regioconcept) - een component of
    /// concept dat sindsdien hernoemd/verwijderd is valt terug op "(leeg)"
    /// (eerste optie) in plaats van een fout te geven.
    /// </summary>
    private static void SelectPageDraftOption(ComboBox picker, string? key)
    {
        var options = (IReadOnlyList<LibraryPageOption>)picker.ItemsSource;
        picker.SelectedItem = options.FirstOrDefault(option => option.Key == key) ?? options[0];
    }

    /// <summary>Bewaart de huidige vijf regio-keuzes onder de App+Pagina die nu in de twee velden staat - overschrijft stilzwijgend een concept met dezelfde App+Pagina.</summary>
    private void SavePageDraft_Click(object sender, RoutedEventArgs e)
    {
        var app = PageDraftAppBox.Text.Trim();
        var page = PageDraftPageBox.Text.Trim();
        if (app.Length == 0 || page.Length == 0)
        {
            return;
        }

        var draft = new PageDraft(
            (LibraryPageHeaderPicker.SelectedItem as LibraryPageOption)?.Key,
            (LibraryPageMenuPicker.SelectedItem as LibraryPageOption)?.Key,
            (LibraryPageInhoudPicker.SelectedItem as LibraryPageOption)?.Key,
            (LibraryPageActiePicker.SelectedItem as LibraryPageOption)?.Key,
            (LibraryPageFooterPicker.SelectedItem as LibraryPageOption)?.Key);

        var appDirectory = Path.Combine(PageDraftsDirectory, app);
        Directory.CreateDirectory(appDirectory);
        File.WriteAllText(Path.Combine(appDirectory, $"{page}.json"), JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true }));

        LoadPageDraftApps();
        PageDraftAppBox.Text = app;
        LoadPageDraftPages();
        PageDraftPageBox.Text = page;
    }

    /// <summary>Voorvoegsel waarmee een gekozen regioconcept in een Pagina-concept wordt bewaard, zodat het niet te verwarren is met een Type.FullName.</summary>
    private const string RegionDraftKeyPrefix = "regioconcept:";

    /// <summary>
    /// One choice in a Pagina-region ComboBox - "(leeg)" (Type en RegionDraft
    /// allebei null), a discovered real UserControl (Type), or a saved
    /// region composition from the Regions menu (RegionDraft = its name).
    /// Key is what a Pagina-concept stores: the Type.FullName for a real
    /// component (same as before, so older concepts still load) or
    /// "regioconcept:&lt;naam&gt;" for a region composition.
    /// </summary>
    private sealed record LibraryPageOption(string Label, Type? Type, string? RegionDraft = null)
    {
        public string? Key => Type?.FullName ?? (RegionDraft is null ? null : RegionDraftKeyPrefix + RegionDraft);

        public override string ToString() => Label;
    }

    /// <summary>
    /// Vult één regio-keuzelijst van de Pagina-stand, met "(leeg)" als
    /// eerste optie, dan de echte componenten van die regio, dan de in het
    /// Regions-menu bewaarde concepten voor die regio. Onthoudt de vorige
    /// keuze (op Key, niet op object-identiteit - de lijst wordt bij elke
    /// LoadLibraryControls vers opgebouwd) zodat een net toegevoegde klasse
    /// of concept wel meteen verschijnt, maar een pagina waar je middenin zit
    /// niet steeds terugspringt naar "(leeg)".
    /// </summary>
    private static void LoadLibraryPagePicker(ComboBox picker, ComponentRegion region, IReadOnlyList<LibraryEntry> discovered)
    {
        var previousKey = (picker.SelectedItem as LibraryPageOption)?.Key;

        var options = new List<LibraryPageOption> { new("(leeg)", null) };
        options.AddRange(discovered
            .Where(entry => entry.Region == region)
            .OrderBy(entry => entry.Type.Name, StringComparer.Ordinal)
            .Select(entry => new LibraryPageOption(entry.Type.Name, entry.Type)));
        options.AddRange(RegionDraftNames(region)
            .Select(name => new LibraryPageOption($"Concept: {name}", null, name)));

        picker.ItemsSource = options;
        picker.SelectedItem = options.FirstOrDefault(option => option.Key == previousKey) ?? options[0];
    }

    /// <summary>
    /// Vult de Regions-stand: de regiokeuze (eenmalig, zodat wisselen van
    /// menu je gekozen regio niet terugzet), een aanvinkvakje per Control en
    /// de lijst met bewaarde concepten voor de gekozen regio.
    /// </summary>
    private void LoadRegionComposer(IReadOnlyList<LibraryEntry> controls)
    {
        _regionComposerControls = controls;

        if (RegionComposerRegionPicker.ItemsSource is null)
        {
            RegionComposerRegionPicker.ItemsSource = Enum.GetValues<ComponentRegion>();
            RegionComposerRegionPicker.SelectedIndex = 0;
        }

        // Een control die sindsdien hernoemd/verwijderd is mag niet als
        // spook op het canvas blijven staan.
        foreach (var gone in _regionComposerPlaced.Keys.Where(type => !controls.Any(entry => entry.Type == type)).ToList())
        {
            _regionComposerPlaced.Remove(gone);
        }

        BuildRegionControlChecklist();
        LoadRegionDraftNames();
        RebuildRegionCanvas();
    }

    /// <summary>Eén aanvinkvakje per Control, aangevinkt als hij nu op het canvas staat.</summary>
    private void BuildRegionControlChecklist()
    {
        RegionComposerControlList.Children.Clear();
        foreach (var entry in _regionComposerControls)
        {
            var checkBox = new CheckBox
            {
                Content = entry.Type.Name,
                Tag = entry.Type,
                IsChecked = _regionComposerPlaced.ContainsKey(entry.Type),
                Margin = new Thickness(0, 0, 0, 4),
            };
            checkBox.SetResourceReference(ForegroundProperty, "TextPrimaryBrush");
            checkBox.Checked += RegionComposerControl_Toggled;
            checkBox.Unchecked += RegionComposerControl_Toggled;
            RegionComposerControlList.Children.Add(checkBox);
        }
    }

    private void RegionComposerControl_Toggled(object sender, RoutedEventArgs e)
    {
        var checkBox = (CheckBox)sender;
        var type = (Type)checkBox.Tag;

        if (checkBox.IsChecked == true)
        {
            // Een net aangevinkte control komt bovenop wat er al ligt.
            var top = _regionComposerPlaced.Count == 0 ? 0 : _regionComposerPlaced.Values.Max(placed => placed.Z) + 1;
            _regionComposerPlaced[type] = new PlacedControl { Type = type, Z = top };
            _regionComposerSelectedType = type;
        }
        else
        {
            _regionComposerPlaced.Remove(type);
            if (_regionComposerSelectedType == type)
            {
                _regionComposerSelectedType = null;
            }
        }

        RebuildRegionCanvas();
    }

    private void RegionComposerRegion_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        LoadRegionDraftNames();
        RebuildRegionCanvas();
    }

    /// <summary>Zet alle controls terug op de linkerbovenhoek - stapelvolgorde en uitsnedes blijven staan.</summary>
    private void RegionComposerReset_Click(object sender, RoutedEventArgs e)
    {
        foreach (var placed in _regionComposerPlaced.Values)
        {
            placed.X = 0;
            placed.Y = 0;
        }

        RebuildRegionCanvas();
    }

    private void RegionComposerBringForward_Click(object sender, RoutedEventArgs e) => MoveSelectedInStack(toFront: true);

    private void RegionComposerSendBackward_Click(object sender, RoutedEventArgs e) => MoveSelectedInStack(toFront: false);

    /// <summary>Legt de geselecteerde control helemaal bovenop of helemaal onderop de stapel.</summary>
    private void MoveSelectedInStack(bool toFront)
    {
        if (_regionComposerSelectedType is not { } type || !_regionComposerPlaced.TryGetValue(type, out var selected))
        {
            return;
        }

        var others = _regionComposerPlaced.Values.Where(placed => placed != selected).ToList();
        if (others.Count > 0)
        {
            selected.Z = toFront ? others.Max(placed => placed.Z) + 1 : others.Min(placed => placed.Z) - 1;
        }

        RebuildRegionCanvas();
    }

    /// <summary>
    /// Een van de vier afsnijvelden is gewijzigd - past de uitsnede van de
    /// geselecteerde control meteen toe. Een ongeldige of halve invoer
    /// (bv. een leeg veld tijdens het typen) telt als 0. Negeert
    /// wijzigingen die we zelf veroorzaken bij het vullen van de velden.
    /// </summary>
    private void RegionTrim_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_fillingRegionTrimFields || _regionComposerSelectedType is not { } type || !_regionComposerPlaced.TryGetValue(type, out var selected))
        {
            return;
        }

        selected.TrimLeft = ParseTrim(RegionTrimLeftBox.Text);
        selected.TrimTop = ParseTrim(RegionTrimTopBox.Text);
        selected.TrimRight = ParseTrim(RegionTrimRightBox.Text);
        selected.TrimBottom = ParseTrim(RegionTrimBottomBox.Text);

        if (FindRegionHost(type) is { } host)
        {
            ApplyTrim(host, selected);
        }

        UpdateRegionSelectionOutline();
    }

    private static double ParseTrim(string text) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : 0;

    /// <summary>
    /// Bouwt het regio-canvas opnieuw op: het canvas krijgt de ECHTE
    /// afmeting van de gekozen regio (zelfde bron als de Controls-stand,
    /// RegionDefaultContainerSize), met daarop elke geplaatste control
    /// volgens zijn positie, stapelvolgorde en uitsnede (CreatePlacedHost).
    /// Daarbovenop een stippelkader om de geselecteerde control.
    /// </summary>
    private void RebuildRegionCanvas()
    {
        if (RegionComposerRegionPicker.SelectedItem is not ComponentRegion region)
        {
            return;
        }

        var (_, width, _, height) = RegionDefaultContainerSize(region);
        RegionComposerCanvas.Width = width;
        RegionComposerCanvas.Height = height;

        RegionComposerCanvas.Children.Clear();
        foreach (var placed in _regionComposerPlaced.Values)
        {
            var host = CreatePlacedHost(placed);
            host.SizeChanged += (_, _) => UpdateRegionSelectionOutline();
            RegionComposerCanvas.Children.Add(host);
        }

        RegionComposerCanvas.Children.Add(_regionSelectionOutline);
        Panel.SetZIndex(_regionSelectionOutline, int.MaxValue);

        FillRegionSelectionPanel();
        UpdateRegionSelectionOutline();

        RegionComposerStatusLabel.Text = _regionComposerPlaced.Count == 0
            ? $"Regio {region}: {width:0} × {height:0} px - vink links Controls aan om ze hier neer te zetten."
            : $"Regio {region}: {width:0} × {height:0} px - sleep een control om 'm te verplaatsen, klik 'm aan voor stapelvolgorde en afsnijden.";
    }

    /// <summary>
    /// Maakt de weergave van één geplaatste control: de echte UserControl in
    /// een doorzichtige Border op zijn positie en stapelhoogte. Die Border
    /// vangt de muis op (een Background van null zou helemaal geen kliks
    /// krijgen) en het component zelf staat op IsHitTestVisible=false, zodat
    /// een knop erin het slepen niet opeet. Gedeeld met de Apps-stand
    /// (BuildRegionDraftCanvas), zodat een bewaard concept daar exact zo
    /// verschijnt als je 'm hier gemaakt hebt.
    /// </summary>
    private static Border CreatePlacedHost(PlacedControl placed)
    {
        var element = (FrameworkElement)Activator.CreateInstance(placed.Type)!;
        element.IsHitTestVisible = false;

        var host = new Border { Background = Brushes.Transparent, Child = element, Tag = placed.Type };
        Canvas.SetLeft(host, placed.X);
        Canvas.SetTop(host, placed.Y);
        Panel.SetZIndex(host, placed.Z);

        // De uitsnede hangt af van de werkelijke afmeting van de control,
        // en die is pas bekend na de eerste layout - vandaar SizeChanged.
        host.SizeChanged += (_, _) => ApplyTrim(host, placed);
        return host;
    }

    /// <summary>
    /// Snijdt de control bij met een Clip-rechthoek. Clip bepaalt in WPF
    /// zowel wat er getekend wordt als waar er geklikt kan worden, dus een
    /// weggesneden stuk is ook niet meer te verslepen.
    /// </summary>
    private static void ApplyTrim(FrameworkElement host, PlacedControl placed)
    {
        if (placed.TrimLeft == 0 && placed.TrimTop == 0 && placed.TrimRight == 0 && placed.TrimBottom == 0)
        {
            host.Clip = null;
            return;
        }

        host.Clip = new RectangleGeometry(new Rect(
            placed.TrimLeft,
            placed.TrimTop,
            Math.Max(0, host.ActualWidth - placed.TrimLeft - placed.TrimRight),
            Math.Max(0, host.ActualHeight - placed.TrimTop - placed.TrimBottom)));
    }

    /// <summary>Stippelkader om de geselecteerde control - zelf niet aanklikbaar, dus het zit het slepen niet in de weg.</summary>
    private readonly System.Windows.Shapes.Rectangle _regionSelectionOutline = new()
    {
        StrokeThickness = 1,
        StrokeDashArray = [4, 3],
        IsHitTestVisible = false,
        Visibility = Visibility.Collapsed,
    };

    /// <summary>Legt het stippelkader precies over het ZICHTBARE deel (na afsnijden) van de geselecteerde control.</summary>
    private void UpdateRegionSelectionOutline()
    {
        if (_regionComposerSelectedType is not { } type
            || !_regionComposerPlaced.TryGetValue(type, out var placed)
            || FindRegionHost(type) is not { } host
            || host.ActualWidth == 0)
        {
            _regionSelectionOutline.Visibility = Visibility.Collapsed;
            return;
        }

        _regionSelectionOutline.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "AccentBrush");
        _regionSelectionOutline.Width = Math.Max(1, host.ActualWidth - placed.TrimLeft - placed.TrimRight);
        _regionSelectionOutline.Height = Math.Max(1, host.ActualHeight - placed.TrimTop - placed.TrimBottom);
        Canvas.SetLeft(_regionSelectionOutline, Canvas.GetLeft(host) + placed.TrimLeft);
        Canvas.SetTop(_regionSelectionOutline, Canvas.GetTop(host) + placed.TrimTop);
        _regionSelectionOutline.Visibility = Visibility.Visible;
    }

    /// <summary>Vult het "Geselecteerd"-blok met de waarden van de geselecteerde control, of zet het uit als er niets geselecteerd is.</summary>
    private void FillRegionSelectionPanel()
    {
        _fillingRegionTrimFields = true;
        try
        {
            if (_regionComposerSelectedType is { } type && _regionComposerPlaced.TryGetValue(type, out var placed))
            {
                RegionComposerSelectedLabel.Text = type.Name;
                RegionComposerSelectionPanel.IsEnabled = true;
                RegionTrimLeftBox.Text = placed.TrimLeft.ToString(CultureInfo.InvariantCulture);
                RegionTrimTopBox.Text = placed.TrimTop.ToString(CultureInfo.InvariantCulture);
                RegionTrimRightBox.Text = placed.TrimRight.ToString(CultureInfo.InvariantCulture);
                RegionTrimBottomBox.Text = placed.TrimBottom.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                RegionComposerSelectedLabel.Text = "(klik een control aan)";
                RegionComposerSelectionPanel.IsEnabled = false;
                RegionTrimLeftBox.Text = RegionTrimTopBox.Text = RegionTrimRightBox.Text = RegionTrimBottomBox.Text = string.Empty;
            }
        }
        finally
        {
            _fillingRegionTrimFields = false;
        }
    }

    private FrameworkElement? FindRegionHost(Type type) =>
        RegionComposerCanvas.Children.OfType<Border>().FirstOrDefault(host => host.Tag as Type == type);

    private void RegionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Klik op een leeg stuk canvas = niets meer geselecteerd.
        if (e.OriginalSource is not DependencyObject source || FindRegionCanvasHost(source) is not { } host || host.Tag is not Type type)
        {
            _regionComposerSelectedType = null;
            FillRegionSelectionPanel();
            UpdateRegionSelectionOutline();
            return;
        }

        _regionComposerSelectedType = type;
        FillRegionSelectionPanel();
        UpdateRegionSelectionOutline();

        _regionDragElement = host;
        _regionDragStart = e.GetPosition(RegionComposerCanvas);
        _regionDragOrigin = new Point(Canvas.GetLeft(host), Canvas.GetTop(host));
        RegionComposerCanvas.CaptureMouse();
    }

    private void RegionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_regionDragElement is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pointer = e.GetPosition(RegionComposerCanvas);
        var x = Math.Round(_regionDragOrigin.X + (pointer.X - _regionDragStart.X));
        var y = Math.Round(_regionDragOrigin.Y + (pointer.Y - _regionDragStart.Y));

        Canvas.SetLeft(_regionDragElement, x);
        Canvas.SetTop(_regionDragElement, y);

        var type = (Type)_regionDragElement.Tag;
        var placed = _regionComposerPlaced[type];
        placed.X = x;
        placed.Y = y;
        UpdateRegionSelectionOutline();
        RegionComposerStatusLabel.Text = $"{type.Name}: X {x:0}, Y {y:0}";
    }

    /// <summary>
    /// Map waar regioconcepten als los JSON-bestand bewaard worden, genest
    /// als Regio/Naam.json. Zelfde plek en reden als PageDraftsDirectory: in
    /// de brontree, zodat concepten meegaan in git.
    /// </summary>
    private static readonly string RegionDraftsDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Scratch", "RegionDrafts"));

    /// <summary>Eén geplaatste control zoals hij in een regioconcept op schijf staat - Type als FullName, zodat een hernoemde klasse bij het laden gewoon wordt overgeslagen.</summary>
    private sealed record RegionDraftItem(string Type, double X, double Y, int Z, double TrimLeft, double TrimTop, double TrimRight, double TrimBottom);

    private sealed record RegionDraft(List<RegionDraftItem> Items);

    /// <summary>De namen van alle bewaarde concepten voor één regio, alfabetisch.</summary>
    private static IEnumerable<string> RegionDraftNames(ComponentRegion region)
    {
        var directory = Path.Combine(RegionDraftsDirectory, region.ToString());
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().OrderBy(name => name, StringComparer.Ordinal)
            : [];
    }

    /// <summary>
    /// Leest een bewaard regioconcept terug als lijst geplaatste controls.
    /// Een control die sindsdien hernoemd of verwijderd is, wordt
    /// overgeslagen in plaats van een fout te geven.
    /// </summary>
    private static List<PlacedControl>? LoadRegionDraft(ComponentRegion region, string name)
    {
        var path = Path.Combine(RegionDraftsDirectory, region.ToString(), $"{name}.json");
        if (!File.Exists(path) || JsonSerializer.Deserialize<RegionDraft>(File.ReadAllText(path)) is not { } draft)
        {
            return null;
        }

        var assembly = typeof(Stylebook.Components.Controls.Basis).Assembly;
        return draft.Items
            .Select(item => (item, type: assembly.GetType(item.Type)))
            .Where(pair => pair.type is not null)
            .Select(pair => new PlacedControl
            {
                Type = pair.type!,
                X = pair.item.X,
                Y = pair.item.Y,
                Z = pair.item.Z,
                TrimLeft = pair.item.TrimLeft,
                TrimTop = pair.item.TrimTop,
                TrimRight = pair.item.TrimRight,
                TrimBottom = pair.item.TrimBottom,
            })
            .ToList();
    }

    /// <summary>Vult de Naam-keuzelijst met de bewaarde concepten van de regio die nu gekozen is.</summary>
    private void LoadRegionDraftNames()
    {
        if (RegionComposerRegionPicker.SelectedItem is ComponentRegion region)
        {
            RegionDraftNameBox.ItemsSource = RegionDraftNames(region).ToList();
        }
    }

    /// <summary>
    /// Een concept gekozen uit de lijst - laadt 'm. Via Dispatcher.BeginInvoke,
    /// om dezelfde reden als PageDraftPage_Changed: bij een klik in de lijst
    /// van een IsEditable ComboBox is .Text op dit moment nog niet bijgewerkt.
    /// </summary>
    private void RegionDraftName_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(TryLoadRegionDraft), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>Vervangt de huidige samenstelling door het concept met de naam die nu in het veld staat - als dat concept bestaat. Een nieuwe naam intypen verandert dus niets aan wat er op het canvas staat.</summary>
    private void TryLoadRegionDraft()
    {
        var name = RegionDraftNameBox.Text.Trim();
        if (name.Length == 0 || RegionComposerRegionPicker.SelectedItem is not ComponentRegion region || LoadRegionDraft(region, name) is not { } items)
        {
            return;
        }

        _regionComposerPlaced.Clear();
        foreach (var item in items)
        {
            _regionComposerPlaced[item.Type] = item;
        }

        _regionComposerSelectedType = null;
        BuildRegionControlChecklist();
        RebuildRegionCanvas();
        RegionComposerStatusLabel.Text = $"Concept '{name}' geladen ({items.Count} controls).";
    }

    /// <summary>Bewaart de huidige samenstelling onder de gekozen regio + de naam in het veld - overschrijft stilzwijgend een concept met dezelfde naam.</summary>
    private void SaveRegionDraft_Click(object sender, RoutedEventArgs e)
    {
        var name = RegionDraftNameBox.Text.Trim();
        if (name.Length == 0 || RegionComposerRegionPicker.SelectedItem is not ComponentRegion region)
        {
            RegionComposerStatusLabel.Text = "Vul eerst een naam in om het concept onder te bewaren.";
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            RegionComposerStatusLabel.Text = $"'{name}' bevat tekens die niet in een bestandsnaam mogen.";
            return;
        }

        var draft = new RegionDraft(_regionComposerPlaced.Values
            .OrderBy(placed => placed.Z)
            .Select(placed => new RegionDraftItem(placed.Type.FullName!, placed.X, placed.Y, placed.Z, placed.TrimLeft, placed.TrimTop, placed.TrimRight, placed.TrimBottom))
            .ToList());

        var directory = Path.Combine(RegionDraftsDirectory, region.ToString());
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{name}.json"), JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true }));

        LoadRegionDraftNames();
        RegionDraftNameBox.Text = name;
        RegionComposerStatusLabel.Text = $"Concept '{name}' bewaard voor {region} - staat nu ook als keuze in Apps.";
    }

    /// <summary>
    /// Bouwt een bewaard regioconcept als niet-bewerkbare samenstelling voor
    /// de Apps-stand: dezelfde weergave per control als het Regions-canvas
    /// (CreatePlacedHost), zonder slepen of selecteren. Het canvas vult het
    /// regio-vak van Basis en snijdt af wat erbuiten valt.
    /// </summary>
    private static Canvas? BuildRegionDraftCanvas(ComponentRegion region, string name)
    {
        if (LoadRegionDraft(region, name) is not { } items)
        {
            return null;
        }

        var canvas = new Canvas { ClipToBounds = true };
        foreach (var placed in items)
        {
            canvas.Children.Add(CreatePlacedHost(placed));
        }

        return canvas;
    }

    private void RegionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_regionDragElement is null)
        {
            return;
        }

        _regionDragElement = null;
        RegionComposerCanvas.ReleaseMouseCapture();
    }

    /// <summary>Klimt vanaf het aangeklikte element omhoog tot het element dat rechtstreeks op het canvas staat - dat is wat versleept wordt.</summary>
    private FrameworkElement? FindRegionCanvasHost(DependencyObject source)
    {
        var current = source;
        while (current is not null && current != RegionComposerCanvas)
        {
            var parent = VisualTreeHelper.GetParent(current);
            if (parent == RegionComposerCanvas)
            {
                return current as FrameworkElement;
            }

            current = parent;
        }

        return null;
    }

    /// <summary>
    /// Eén regio-keuzelijst in de Pagina-stand is gewijzigd - maakt de
    /// gekozen echte UserControl of het gekozen regioconcept (of null bij
    /// "(leeg)") en zet 'm rechtstreeks op de bijbehorende content-slot van
    /// LibraryPageBasis. Basis staat op het echte 1920x1080-formaat, dus
    /// elke regio krijgt automatisch zijn eigen echte Basis-afmeting
    /// (210/256/*/48/65) - geen aparte containerlogica nodig, dat doet
    /// Basis.xaml zelf al.
    /// </summary>
    private void LibraryPagePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Handmatig een regio kiezen betekent altijd terug naar de
        // vijf-regio-samenstelling, ook als LibraryPagePreviewHost net nog
        // een complete, echte Apps-pagina toonde (zie TryLoadPageDraft).
        LibraryPagePreviewHost.Content = LibraryPageBasis;

        var picker = (ComboBox)sender;
        var region = Enum.Parse<ComponentRegion>((string)picker.Tag);
        var option = picker.SelectedItem as LibraryPageOption;
        object? content = option switch
        {
            { Type: { } type } => Activator.CreateInstance(type),
            { RegionDraft: { } draftName } => BuildRegionDraftCanvas(region, draftName),
            _ => null,
        };

        switch (region)
        {
            case ComponentRegion.Header:
                LibraryPageBasis.HeaderContent = content;
                break;
            case ComponentRegion.Menu:
                LibraryPageBasis.MenuContent = content;
                break;
            case ComponentRegion.Inhoud:
                LibraryPageBasis.MainContent = content;
                break;
            case ComponentRegion.Actie:
                LibraryPageBasis.ActionContent = content;
                break;
            case ComponentRegion.Footer:
                LibraryPageBasis.FooterContent = content;
                break;
        }
    }

    /// <summary>
    /// Instantieert de gekozen UserControl ECHT (Activator.CreateInstance,
    /// geen XamlReader.Parse - dit IS al een gecompileerde klasse) en toont
    /// 'm op de echte containerafmeting van zijn regio. Kleuren volgen
    /// nog steeds het live thema, want DynamicResource resolvet via de
    /// visual tree zodra dit element ergens in gehangen wordt - alleen
    /// de STRUCTUUR komt nu uit een bestand.
    /// </summary>
    private void LibraryComponents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LibraryComponents.SelectedItem is not LibraryEntry entry)
        {
            LibraryPreviewContent.Content = null;
            LibraryContainerSizeLabel.Text = "Containerformaat: -";
            return;
        }

        var (widthMode, width, heightMode, height) = RegionDefaultContainerSize(entry.Region);
        LibraryTestContainerBorder.Width = width;
        LibraryTestContainerBorder.Height = height;
        LibraryContainerSizeLabel.Text = $"Containerformaat ({entry.Region?.ToString() ?? "Controls"}): {width:0} × {height:0} px";

        var element = (FrameworkElement)Activator.CreateInstance(entry.Type)!;
        ApplySizeConstraints(element, widthMode, null, heightMode, null);
        LibraryPreviewContent.Content = element;
    }
}
