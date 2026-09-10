using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.Json;
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
using Microsoft.Web.WebView2.Core;
using Stylebook.Components.Theming;
using Stylebook.Data;
using Stylebook.Data.Entities;
using Stylebook.Playground.Editor;
using Stylebook.Playground.Theming;
using ComponentsTheme = Stylebook.Components.Theming.Theme;
using DataTheme = Stylebook.Data.Entities.Theme;
using DataApplication = Stylebook.Data.Entities.Application;
using DataPage = Stylebook.Data.Entities.Page;

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

    private sealed record ThemePresetOption(ComponentsTheme Value, string Label);

    private static readonly ThemePresetOption[] ThemePresetOptions =
    [
        new ThemePresetOption(ComponentsTheme.Lcars, "LCARS"),
        new ThemePresetOption(ComponentsTheme.VisualStudio, "Visual Studio"),
    ];

    private BuilderMode _builderMode = BuilderMode.PageBuilder;
    private StylebookComponent? _lastSelectedComponent;
    private bool _initializing = true;

    /// <summary>Welke Applicatie/Pagina in Paginabouwer is gekozen - null zolang er nog geen (of geen enkele) is. Zie ApplicationPicker_SelectionChanged/PagePicker_SelectionChanged.</summary>
    private DataApplication? _selectedApplication;

    private DataPage? _selectedPage;

    /// <summary>
    /// True terwijl LoadPageRegionsIntoSelections de regio-ListBoxen
    /// programmatisch vult vanuit een net gekozen Pagina - voorkomt dat
    /// Component_SelectionChanged die eigen wijzigingen aanziet voor een
    /// echte gebruikersactie en ze meteen weer terugschrijft.
    /// </summary>
    private bool _loadingPageRegions;

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

    /// <summary>Completes once MonacoDiffView's page has actually loaded Monaco (CDN script loading is async) - SetMonacoDiffContent awaits this so a proposal shown before that finishes still lands correctly.</summary>
    private readonly TaskCompletionSource _monacoReady = new();

    public MainWindow()
    {
        InitializeComponent();

        ThemePresetPicker.ItemsSource = ThemePresetOptions;
        ThemePresetPicker.DisplayMemberPath = nameof(ThemePresetOption.Label);
        ThemePresetPicker.SelectedIndex = Array.FindIndex(
            ThemePresetOptions, o => string.Equals(o.Value.ToString(), App.CurrentTheme.ToString(), StringComparison.Ordinal));
        _initializing = false;

        LoadComponentsByRegion();
        LoadApplications();
        PageBuilderModeButton.IsChecked = true;

        _ = InitializeMonacoDiffEditor();
    }

    /// <summary>Boots the WebView2 host and points it at the Monaco diff-editor page - fire-and-forget from the constructor, awaited implicitly via _monacoReady by anything that needs the editor.</summary>
    private async Task InitializeMonacoDiffEditor()
    {
        await MonacoDiffView.EnsureCoreWebView2Async();
        MonacoDiffView.CoreWebView2.WebMessageReceived += OnMonacoWebMessage;
        MonacoDiffView.NavigateToString(MonacoDiffHtml.Content);
    }

    /// <summary>
    /// The two messages Monaco's page sends back: "ready" once it has
    /// actually finished loading from the CDN (unblocks _monacoReady, so
    /// a proposal shown before that completes still lands correctly), and
    /// "change" every time the MODIFIED (editable) side is typed in -
    /// keeps _proposedXaml in sync exactly like ProposedXamlText_TextChanged
    /// did for the plain-TextBox version of this comparison.
    /// </summary>
    private void OnMonacoWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var json = e.TryGetWebMessageAsString();
        using var message = JsonDocument.Parse(json);
        var type = message.RootElement.GetProperty("type").GetString();

        if (type == "ready")
        {
            _monacoReady.TrySetResult();
        }
        else if (type == "change" && _proposedXaml is not null)
        {
            _proposedXaml = message.RootElement.GetProperty("value").GetString();
            RefreshProposalPreview();
        }
    }

    /// <summary>Sets both sides of Monaco's diff editor - waits for _monacoReady first, so this is safe to call even if the page hasn't finished loading yet.</summary>
    private async void SetMonacoDiffContent(string originalXaml, string proposedXaml)
    {
        await _monacoReady.Task;
        var script = $"window.setDiffContent({JsonSerializer.Serialize(originalXaml)}, {JsonSerializer.Serialize(proposedXaml)})";
        await MonacoDiffView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private async void ClearMonacoDiffContent()
    {
        await _monacoReady.Task;
        await MonacoDiffView.CoreWebView2.ExecuteScriptAsync("window.clearDiffContent()");
    }

    /// <summary>Called from a style-reference-bar row click - see BuildStyleReferenceBar. Runs entirely in JS (window.replaceSelectionWithToken) since Monaco's selection/edit APIs live there.</summary>
    private async void ReplaceSelectionWithToken(string tokenName)
    {
        await _monacoReady.Task;
        await MonacoDiffView.CoreWebView2.ExecuteScriptAsync(
            $"window.replaceSelectionWithToken({JsonSerializer.Serialize(tokenName)})");
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

    /// <summary>De regio's die daadwerkelijk een Basis-slot hebben - Algemeen niet, zie ComponentRegion's class comment, dus die telt niet mee voor een Pagina.</summary>
    private static readonly ComponentRegion[] PageRegionsInBasis =
    [
        ComponentRegion.Header, ComponentRegion.Menu, ComponentRegion.Inhoud, ComponentRegion.Actie, ComponentRegion.Footer,
    ];

    private void LoadApplications()
    {
        ApplicationPicker.ItemsSource = App.Db.Applications.AsEnumerable().OrderBy(a => a.Name, StringComparer.Ordinal).ToList();
        ApplicationPicker.SelectedIndex = ApplicationPicker.Items.Count > 0 ? 0 : -1;
    }

    private void ApplicationPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedApplication = ApplicationPicker.SelectedItem as DataApplication;
        LoadPages();
    }

    private void LoadPages()
    {
        PagePicker.ItemsSource = _selectedApplication is null
            ? null
            : App.Db.Pages.AsEnumerable().Where(p => p.ApplicationId == _selectedApplication.Id).OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        PagePicker.SelectedIndex = PagePicker.Items.Count > 0 ? 0 : -1;

        if (PagePicker.Items.Count == 0)
        {
            // Geen PagePicker_SelectionChanged-event bij een lege lijst -
            // zelf de regio-ListBoxen leegtrekken zodat er geen vorige
            // pagina's selecties blijven hangen.
            _selectedPage = null;
            LoadPageRegionsIntoSelections();
        }
    }

    private void PagePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPage = PagePicker.SelectedItem as DataPage;
        LoadPageRegionsIntoSelections();
    }

    /// <summary>
    /// Zet elke regio-ListBox's selectie op wat er voor de huidige
    /// _selectedPage is opgeslagen (of leeg, zonder pagina of zonder
    /// opgeslagen keuze voor die regio) - zie SavePageRegionSelection
    /// voor de schrijf-kant. _loadingPageRegions voorkomt dat dit
    /// programmatisch zetten zelf weer als een gebruikerswijziging
    /// wordt opgeslagen.
    /// </summary>
    private void LoadPageRegionsIntoSelections()
    {
        _loadingPageRegions = true;
        try
        {
            var regions = _selectedPage is { } page
                ? App.Db.PageRegions.AsEnumerable().Where(r => r.PageId == page.Id).ToList()
                : [];

            foreach (var region in PageRegionsInBasis)
            {
                var componentId = regions.FirstOrDefault(r => r.Region == region)?.ComponentId;
                var listBox = ComponentsListBox(region);
                listBox.SelectedItem = componentId is null
                    ? null
                    : listBox.Items.Cast<StylebookComponent>().FirstOrDefault(c => c.Id == componentId);
            }
        }
        finally
        {
            _loadingPageRegions = false;
        }

        RefreshPreview();
    }

    private void AddPage_Click(object sender, RoutedEventArgs e) => AddPage();

    private void NewPageName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddPage();
        }
    }

    private void AddPage()
    {
        var name = NewPageNameBox.Text.Trim();
        if (name.Length == 0 || _selectedApplication is not { } application)
        {
            return;
        }

        App.Db.Pages.Add(new DataPage { ApplicationId = application.Id, Name = name });
        App.Db.SaveChanges();
        NewPageNameBox.Clear();
        LoadPages();
    }

    /// <summary>
    /// Herlaadt alles wat uit de database komt - componenten, Applicaties/
    /// Pagina's, de regio-toewijzingen van de huidige Pagina, en het live
    /// thema - voor als de database buiten deze lopende app om is
    /// gewijzigd (bv. rechtstreeks via SQL). Behoudt de huidige
    /// Applicatie-/Pagina-keuze (opnieuw opgezocht op Id in de vernieuwde
    /// lijst) in plaats van, zoals LoadApplications/LoadPages normaal
    /// doen, terug te springen naar het eerste item - verversen mag de
    /// lopende sessie niet verstoren.
    /// </summary>
    private void RefreshFromDatabase_Click(object sender, RoutedEventArgs e)
    {
        var previousApplicationId = _selectedApplication?.Id;
        var previousPageId = _selectedPage?.Id;

        // Zonder dit blijven alle entiteiten die deze sessie al eerder
        // heeft ingeladen (bv. bij opstarten) door EF Core's
        // change tracker vastgehouden - een gewone query geeft dan
        // gewoon die oude, in-memory objecten terug in plaats van de
        // verse databasewaarden, ook al is de SQL-query zelf prima. Pas
        // ontdekt doordat een rechtstreeks via SQL aangepast component na
        // Verversen nog steeds de oude XAML liet zien. Clear() maakt alle
        // entiteiten "niet meer gevolgd", zodat de Load*-aanroepen
        // hieronder ze echt opnieuw uit de database materialiseren.
        App.Db.ChangeTracker.Clear();

        LoadComponentsByRegion();

        if (_builderMode == BuilderMode.ComponentBuilder)
        {
            ClearComponentEditorState();
        }

        LoadApplications();
        if (previousApplicationId is { } applicationId)
        {
            var application = ApplicationPicker.Items.Cast<DataApplication>().FirstOrDefault(a => a.Id == applicationId);
            if (application is not null)
            {
                ApplicationPicker.SelectedItem = application;
            }
        }

        if (previousPageId is { } pageId)
        {
            var page = PagePicker.Items.Cast<DataPage>().FirstOrDefault(p => p.Id == pageId);
            if (page is not null)
            {
                PagePicker.SelectedItem = page;
            }
        }

        App.ReapplyLiveTheme();
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

        // De regio-ListBoxen worden gedeeld met Paginabouwer (zie
        // LoadPageRegionsIntoSelections) - zonder dit bleef een
        // pagina-gekoppeld component daar geselecteerd staan zodra je
        // naar Componentenbouwer wisselde, waardoor "+" dat bestaande
        // component stilzwijgend hernoemde in plaats van een nieuwe aan
        // te maken, en Verwijderen het verkeerde component raakte.
        if (mode == BuilderMode.ComponentBuilder)
        {
            ClearComponentEditorState();
        }
        else if (mode == BuilderMode.PageBuilder)
        {
            LoadPageRegionsIntoSelections();
        }

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
        PageContextPicker.Visibility = mode == BuilderMode.PageBuilder ? Visibility.Visible : Visibility.Collapsed;

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
        RenameButton(region).IsEnabled = selected is not null;

        if (selected is not null)
        {
            if (_builderMode == BuilderMode.ComponentBuilder)
            {
                // Componentenbouwer edits exactly one component - clear the
                // other regions' lists so their selection highlight can't
                // keep showing a component that isn't the one being edited
                // anymore (looked like "selecting a different one doesn't
                // work" even though the editor/preview had switched fine).
                // Not done in Paginabouwer: there, each region's own
                // selection is what fills that Basis-slot, so all of them
                // staying selected at once is the intended behavior.
                DeselectOtherRegionLists(region);
            }

            _lastSelectedComponent = selected;
            ComponentTitleBox.Text = selected.Title ?? string.Empty;
            ComponentBodyBox.Text = selected.BodyText ?? string.Empty;
            ComponentXamlBox.Text = selected.Xaml ?? string.Empty;
            XamlErrorText.Visibility = Visibility.Collapsed;
            ClearProposal(); // a pending AI proposal belongs to whichever component was selected when it was asked for.
            _aiConversation.Clear(); // same for the conversation itself - it was about that component's XAML.

            // Indices line up 1-to-1 with ContainerSizeMode (Fixed=0,
            // Variable=1, same order as the ComboBoxItems in XAML) - see
            // SaveTestContainerSettings for the write-back half of this.
            WidthModeCombo.SelectedIndex = (int)selected.TestContainerWidthMode;
            HeightModeCombo.SelectedIndex = (int)selected.TestContainerHeightMode;
            ContainerWidthSlider.Value = selected.TestContainerWidth;
            ContainerHeightSlider.Value = selected.TestContainerHeight;
        }

        SavePageRegionSelection(region, selected);
        RefreshPreview();
    }

    private void DeselectOtherRegionLists(ComponentRegion keep)
    {
        foreach (ComponentRegion region in Enum.GetValues<ComponentRegion>())
        {
            if (region != keep)
            {
                ComponentsListBox(region).SelectedItem = null;
            }
        }
    }

    /// <summary>Zoals DeselectOtherRegionLists, maar zonder uitzondering - gebruikt bij het overschakelen naar Componentenbouwer zodat die altijd met een schone lei begint.</summary>
    private void ClearAllRegionListSelections()
    {
        foreach (ComponentRegion region in Enum.GetValues<ComponentRegion>())
        {
            ComponentsListBox(region).SelectedItem = null;
        }
    }

    /// <summary>
    /// Zet de Componentenbouwer terug naar "niets geselecteerd" - gebruikt
    /// bij het overschakelen naar die modus (SetBuilderMode) en bij
    /// RefreshFromDatabase_Click, waar de herladen ListBoxen toch andere
    /// StylebookComponent-objecten bevatten dus een vorige selectie nooit
    /// meer kan kloppen.
    /// </summary>
    private void ClearComponentEditorState()
    {
        ClearAllRegionListSelections();
        _lastSelectedComponent = null;
        ComponentTitleBox.Text = string.Empty;
        ComponentBodyBox.Text = string.Empty;
        ComponentXamlBox.Text = string.Empty;
        XamlErrorText.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Paginabouwer only: persists which component (if any) occupies
    /// this region for the currently selected Page - upsert by
    /// (PageId, Region), see PageRegionConfiguration's unique index.
    /// No-op outside Paginabouwer, without a selected Page, or while
    /// LoadPageRegionsIntoSelections is programmatically restoring a
    /// page's saved selections (_loadingPageRegions) - that's a read,
    /// not a user edit, and must never write back.
    /// </summary>
    private void SavePageRegionSelection(ComponentRegion region, StylebookComponent? selected)
    {
        if (_loadingPageRegions || _builderMode != BuilderMode.PageBuilder || _selectedPage is not { } page)
        {
            return;
        }

        var pageRegion = App.Db.PageRegions.FirstOrDefault(r => r.PageId == page.Id && r.Region == region);
        if (pageRegion is null)
        {
            App.Db.PageRegions.Add(new PageRegion { PageId = page.Id, Region = region, ComponentId = selected?.Id });
        }
        else
        {
            pageRegion.ComponentId = selected?.Id;
        }

        App.Db.SaveChanges();
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

    private const double ComponentPreviewMinZoom = 0.25;
    private const double ComponentPreviewMaxZoom = 3.0;

    /// <summary>
    /// Ctrl+scrollwiel zoomt de geïsoleerde Componentenbouwer-preview -
    /// ORIGINEEL en, indien zichtbaar, AI-VOORSTEL ernaast zoomen altijd
    /// samen, maar elk rond ZIJN EIGEN middelpunt (twee losse
    /// ScaleTransforms, zie MainWindow.xaml) - niet rond het midden van
    /// de omvattende twee-koloms-Grid, anders schuift ORIGINEEL bij het
    /// verschijnen van VOORSTEL (kolom "*" halveert dan) weg van zijn
    /// eigen middelpunt en kan bij een afwijkend zoomniveau buiten beeld
    /// belanden. Zonder Ctrl doet het wiel niets - er is hier toch geen
    /// scrollbare inhoud onder.
    /// </summary>
    private void ComponentPreviewZoom_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        e.Handled = true;

        var factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        var newScale = Math.Clamp(ComponentPreviewZoomTransform.ScaleX * factor, ComponentPreviewMinZoom, ComponentPreviewMaxZoom);
        ComponentPreviewZoomTransform.ScaleX = newScale;
        ComponentPreviewZoomTransform.ScaleY = newScale;
        ProposedPreviewZoomTransform.ScaleX = newScale;
        ProposedPreviewZoomTransform.ScaleY = newScale;
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
    /// Writes the Testcontainer's current Vast/Variabel + afmeting
    /// choices onto the component being saved, so they come back the
    /// next time it's selected instead of resetting to Vast/400×260 -
    /// see Component_SelectionChanged for the read-back half. Called
    /// from every place that actually commits a component to the
    /// database (GenerateFromProperties_Click, AcceptProposal_Click) -
    /// not from SaveXaml_Click itself, since that only opens the
    /// voorstel-vergelijken flow and doesn't write anything yet.
    /// </summary>
    private void SaveTestContainerSettings(StylebookComponent component)
    {
        component.TestContainerWidthMode = (ContainerSizeMode)WidthModeCombo.SelectedIndex;
        component.TestContainerHeightMode = (ContainerSizeMode)HeightModeCombo.SelectedIndex;
        component.TestContainerWidth = ContainerWidthSlider.Value;
        component.TestContainerHeight = ContainerHeightSlider.Value;
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

    /// <summary>
    /// Naslag van elk stijl-token (icoon/swatch + naam) voor de balk links
    /// van de preview in Componentenbouwer - hergebruikt dezelfde icoon-
    /// generatie als het Stylebook-tabblad (CreatePreview), zonder de
    /// Apply-kant. Elke rij is klikbaar: selecteer een letterlijke waarde
    /// in het VOORSTEL (rechterkant van de diff-editor) en klik een token
    /// om die selectie te vervangen door {DynamicResource TokenNaam} - zie
    /// ReplaceSelectionWithToken. Zonder selectie voegt het token gewoon
    /// in op de cursorpositie.
    /// </summary>
    private FrameworkElement BuildStyleReferenceBar()
    {
        var tokens = App.Db.DesignTokens.Where(t => t.Theme == App.CurrentTheme).AsEnumerable()
            .OrderBy(t => (int)t.Category)
            .ThenBy(TokenSortValue)
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

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8),
                Background = Brushes.Transparent, // hit-testable over de hele rij, niet alleen waar kinderen tekenen
                Cursor = Cursors.Hand,
            };

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

            var tokenName = token.Name;
            row.PreviewMouseLeftButtonDown += (_, _) => ReplaceSelectionWithToken(tokenName);
            row.MouseEnter += (_, _) => row.SetResourceReference(Panel.BackgroundProperty, "BorderBrush");
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

            stack.Children.Add(row);
        }

        return stack;
    }

    /// <summary>
    /// Klein-naar-groot binnen een categorie i.p.v. alfabetisch op naam -
    /// alfabetisch zette RadiusLarge/RadiusMedium/RadiusSmall bijvoorbeeld
    /// grofweg groot-medium-klein neer, puur toeval van de namen. Color en
    /// FontFamily hebben geen zinvolle grootte-volgorde (hex/fontnaam
    /// parsen niet als getal) - die vallen terug op 0, dus voor hen blijft
    /// de ThenBy(Name) erna gewoon de doorslag geven, exact zoals eerst.
    /// </summary>
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

    /// <summary>
    /// XamlReader.Parse and WPF's layout engine (Measure/Arrange) are both
    /// recursive, proportional to nesting depth - absurdly large or
    /// deeply nested XAML (an accidental whole-clipboard paste, a
    /// runaway AI response) can genuinely blow the stack.
    /// StackOverflowException cannot be caught in .NET (not even by
    /// App's DispatcherUnhandledException) - the whole process dies with
    /// no error message at all. The number of '<' characters is a crude
    /// but usable upper bound on nesting depth (depth can never exceed
    /// total element count), set well above anything a hand-built or
    /// AI-generated component would ever legitimately need. Used at
    /// every XamlReader.Parse call site that touches XAML from outside
    /// this app's own control (pasted, AI-generated, or otherwise).
    /// </summary>
    private const int MaxXamlElementCount = 500;

    private static bool IsXamlSafeToParse(string xaml) => xaml.Count(c => c == '<') <= MaxXamlElementCount;

    /// <summary>Same rendering CreateComponentVisual uses, but for a raw Xaml string not (yet) attached to a saved component - see ShowProposal.</summary>
    [DebuggerStepThrough]
    private static FrameworkElement RenderXamlPreview(string name, string? xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml))
        {
            return Placeholder(name, "TextPrimaryBrush", "BorderBrush");
        }

        // Iets zonder een enkele '<' kan sowieso geen XML zijn (bv. een
        // los geplakt tokennaam als "TextPrimaryColor") - dat rechtstreeks
        // aan XamlReader.Parse voeren gooit dezelfde uitzondering
        // meermaals (elke keystroke/paste-deelbewerking triggert een
        // nieuwe poging), wat bij "break on exceptions" in de debugger als
        // een stortvloed aan foutmeldingen voelt. Dit voorkomt die worp
        // helemaal voor het overduidelijke geval.
        if (!xaml.Contains('<'))
        {
            return Placeholder($"'{name}' is geen XAML (geen '<' gevonden)", "TextMutedBrush", "AccentBrush");
        }

        if (!IsXamlSafeToParse(xaml))
        {
            return Placeholder($"'{name}' is te groot/diep genest om veilig te previewen (meer dan {MaxXamlElementCount} elementen)", "TextMutedBrush", "AccentBrush");
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

        SnapshotComponentVersion(component);
        component.Title = ComponentTitleBox.Text;
        component.BodyText = ComponentBodyBox.Text;
        component.Xaml = GenerateCardXaml(ComponentTitleBox.Text, ComponentBodyBox.Text);
        ComponentXamlBox.Text = component.Xaml;
        SaveTestContainerSettings(component);

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
               "Zet nooit een expliciete Width of Height op het root-element van de component, tenzij " +
               "daar expliciet om gevraagd wordt - dit component wordt in een Header/Menu/Inhoud/Actie/" +
               "Footer-regio geplaatst die zelf al de juiste afmeting bepaalt en vult (zie Basis.xaml); " +
               "een vaste maat op het root-element overschrijft dat en zorgt dat het component niet meer " +
               "de volledige regio vult, ook al was dat er in de vorige versie niet in gezet.\n" +
               "Vervang je een losse CornerRadius=\"...\" attribuut door theming:CornerRadiusParts (nodig " +
               "zodra meerdere hoeken elk hun eigen token/waarde moeten krijgen, zie CornerRadiusParts.cs), " +
               "zet dan ALTIJD alle vier de hoeken expliciet (TopLeft, TopRight, BottomRight, BottomLeft) - " +
               "ook de hoeken die 0 zijn. Nooit een hoek weglaten omdat 'ie toch op 0 uitkomt: de hele set " +
               "moet in één oogopslag duidelijk zijn zonder dat je de impliciete 0-standaard hoeft te kennen.\n" +
               DbThemeBuilder.DescribeForAi(App.Db, App.CurrentTheme);
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
        if (!IsXamlSafeToParse(xaml))
        {
            element = null;
            return false;
        }

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
        SetMonacoDiffContent(originalXaml, proposedXaml);
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
        ClearMonacoDiffContent();
        XamlComparisonRow.Visibility = Visibility.Collapsed;
        XamlComparisonSplitter.Visibility = Visibility.Collapsed;
        ProposalActionsRow.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Called by App.OnDispatcherUnhandledException right after catching a
    /// crash. e.Handled = true stops the process from dying, but it does
    /// NOT finish the layout pass that was in progress - the subtree
    /// being measured when the exception hit (almost always the proposal
    /// preview, since that's the one place externally-supplied XAML - AI
    /// or hand-typed - gets rendered) is left stuck at zero size, which
    /// looks like "the editor disappeared" even though _proposedXaml and
    /// every visibility flag are still exactly what they were. Simply
    /// forcing another layout pass would hit the same broken content and
    /// crash again; discarding the proposal (same effect as clicking
    /// Negeren) removes the broken content first, so the next layout
    /// pass has nothing left to trip over.
    /// </summary>
    internal void RecoverFromUnhandledException()
    {
        if (_proposedXaml is not null)
        {
            ClearProposal();
        }
    }

    /// <summary>Commits the pending AI proposal exactly like a manual "Opslaan en toepassen" would - errors included, so a bad answer is visible and recoverable rather than silently discarded.</summary>
    private void AcceptProposal_Click(object sender, RoutedEventArgs e)
    {
        if (_proposedXaml is not { } xaml || _lastSelectedComponent is not { } component)
        {
            return;
        }

        ComponentXamlBox.Text = xaml;
        SnapshotComponentVersion(component);
        component.Xaml = xaml;
        SaveTestContainerSettings(component);
        App.Db.SaveChanges();

        if (!IsXamlSafeToParse(xaml))
        {
            XamlErrorText.Text = $"Te groot/diep genest om te parsen (meer dan {MaxXamlElementCount} elementen) - overgeslagen.";
            XamlErrorText.Visibility = Visibility.Visible;
        }
        else
        {
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
        CreateComponent(Enum.Parse<ComponentRegion>((string)((Button)sender).Tag));
    }

    /// <summary>
    /// Enter in het naamveld doet hetzelfde als "+" (altijd aanmaken) -
    /// niet hetzelfde als Hernoemen, dat is bewust alleen via de eigen
    /// knop bereikbaar zodat er maar één manier is om per ongeluk een
    /// bestaand component te overschrijven te vermijden.
    /// </summary>
    private void NewComponentName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        CreateComponent(Enum.Parse<ComponentRegion>((string)((TextBox)sender).Tag));
    }

    /// <summary>
    /// Maakt ALTIJD een nieuw component aan - negeert opzettelijk een
    /// eventuele selectie in de regio-ListBox. Vroeger hernoemde "+" een
    /// geselecteerd component zodra je de naam erin typte, wat een
    /// bestaand component (bv. Infoblok) stilzwijgend kon overschrijven
    /// zonder dat het leek te "werken" - zie RenameComponent voor de nu
    /// losse, bewuste hernoem-actie. CreatedAtUtc/UpdatedAtUtc worden
    /// door StylebookDbContext.SaveChanges gezet, nooit hier.
    /// </summary>
    private void CreateComponent(ComponentRegion region)
    {
        var nameBox = NewComponentNameBox(region);
        var name = nameBox.Text.Trim();
        if (name.Length == 0)
        {
            return;
        }

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

        App.Db.SaveChanges();
        nameBox.Clear();
        LoadComponentsByRegion();
    }

    private void RenameComponent_Click(object sender, RoutedEventArgs e)
    {
        RenameComponent(Enum.Parse<ComponentRegion>((string)((Button)sender).Tag));
    }

    /// <summary>
    /// Hernoemt het component dat nu in de regio-ListBox geselecteerd
    /// staat naar de tekst in het naamveld - de enige plek die een
    /// bestaand component overschrijft, dus altijd een bewuste, losse
    /// knop (nooit een side effect van "+" of Enter). Bewaart eerst een
    /// snapshot van de oude naam, zie SnapshotComponentVersion.
    /// </summary>
    private void RenameComponent(ComponentRegion region)
    {
        if (ComponentsListBox(region).SelectedItem is not StylebookComponent existing)
        {
            return;
        }

        var nameBox = NewComponentNameBox(region);
        var name = nameBox.Text.Trim();
        if (name.Length == 0)
        {
            return;
        }

        SnapshotComponentVersion(existing);
        existing.Name = name;

        App.Db.SaveChanges();
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

    private const int MaxComponentVersionsToKeep = 5;

    /// <summary>
    /// Bewaart een snapshot van component's HUIDIGE (nog niet gewijzigde)
    /// Name/Title/BodyText/Xaml, vlak vóór Hernoemen, "Genereer en
    /// opslaan", of "Overnemen" die staat overschrijft - een noodgreep om
    /// via SQL terug te vinden wat een component vroeger was (geen UI).
    /// Trimt daarna terug tot de laatste MaxComponentVersionsToKeep voor
    /// dat component - oudste eerst weg.
    /// </summary>
    private void SnapshotComponentVersion(StylebookComponent component)
    {
        // Eigen SaveChanges nodig: de nieuwe versie moet al ECHT in de
        // database staan (met een Id) vóórdat de trim-query hieronder
        // 'm kan meetellen - anders zou "laatste 5" per ongeluk 6 rijen
        // overhouden (de query ziet de nog-ongesaved rij niet mee).
        App.Db.ComponentVersions.Add(new ComponentVersion
        {
            ComponentId = component.Id,
            Name = component.Name,
            Title = component.Title,
            BodyText = component.BodyText,
            Xaml = component.Xaml,
        });
        App.Db.SaveChanges();

        var idsToRemove = App.Db.ComponentVersions
            .Where(v => v.ComponentId == component.Id)
            .OrderByDescending(v => v.CreatedAtUtc)
            .Select(v => v.Id)
            .AsEnumerable()
            .Skip(MaxComponentVersionsToKeep)
            .ToList();

        if (idsToRemove.Count > 0)
        {
            App.Db.ComponentVersions.RemoveRange(App.Db.ComponentVersions.Where(v => idsToRemove.Contains(v.Id)));
            App.Db.SaveChanges();
        }
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

    private Button RenameButton(ComponentRegion region) => region switch
    {
        ComponentRegion.Header => HeaderRenameButton,
        ComponentRegion.Menu => MenuRenameButton,
        ComponentRegion.Inhoud => InhoudRenameButton,
        ComponentRegion.Actie => ActieRenameButton,
        ComponentRegion.Footer => FooterRenameButton,
        ComponentRegion.Algemeen => AlgemeenRenameButton,
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, null),
    };
}
