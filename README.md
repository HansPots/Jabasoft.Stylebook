# Jabasoft.Stylebook

Gedeelde class libraries voor de hele JabaSoft-familie (`JabaSoft.TabStudio`,
`JabaSoft.LocalAiStudio`, `Jabasoft`, en toekomstige apps), plus **Stylebook.Web**
— een eigen, uitvoerbare app: de Stijlgids/component-bibliotheek-tool
(pagina's bekijken, elementen aanwijzen en als herbruikbaar component
opslaan, CSS bewerken/genereren met AI, materialiseren naar een echt
Blazor-component in `Jabasoft.Base`).

> Voor een stap-voor-stap herbouwplan van de hele JabaSoft-familie (met
> geleerde lessen/valkuilen) zie `C:\Repos\Bewaren\JabaSoft-Herbouw\`.

## Projecten

### Shared.Telemetry

Losstaande database (`JabasoftBase`) voor LLM-tokengebruik, gedeeld door
alle apps. Elke rij is één LLM-call met tijdstip en app-naam. Zie
`Shared.Telemetry/README.md`. (Het herbruikbare token-verbruiksscherm zelf,
`TokenUsageOverview.razor`, staat in `Jabasoft.Base` — niet hier.)

### Shared.UI

Razor Class Library met de huisstijl — de ENE plek waar deze bestanden
bestaan, nooit gekopieerd naar een app's eigen wwwroot:
- `wwwroot/jabasoft-theme.css` — de LCARS-basisstijl (kleurtokens, spacing,
  de `.shell`-grid-basis).
- `wwwroot/vs-theme.css` — het Visual Studio-thema, gelijkwaardig aan LCARS
  (geen "extra"), geactiveerd via `data-theme="vs"` op `<html>`.
- `wwwroot/shell-menu.css` — de rose-sidebar-menuvorm die elke app deelt;
  een app overschrijft alleen de kleur-CSS-variabelen voor een eigen palet.
- `wwwroot/settings-page.css` — de gedeelde Settings-pagina-kaarten-look,
  inclusief de `.theme-picker` (LCARS/VS Code-keuze, altijd de eerste
  sectie op een Settings-pagina).
- `wwwroot/theme.js` — leest/schrijft het gekozen thema (`localStorage`),
  `window.jabasoftTheme = {apply, set, sync}`.
- `wwwroot/embed.js` — brug voor apps die embedded in Jabasoft's shell
  draaien (`window.jabasoftEmbed = {isEmbedded, goHome}`).
- `docs/STYLEBOOK.md` — het huisstijlhandboek: de geschreven regels achter
  de tokens in `jabasoft-theme.css`.

### Stylebook.Web

Zie de eigen documentatie in `Stylebook.Web/` (of het herbouwplan) voor de
werking van de Pagina's-/Componenten-/Instellingen-tabs.

## Hergebruik door een andere app

Alle repos staan als sibling-mappen naast elkaar in `C:\Repos`. Andere apps
verwijzen naar deze projecten via een relatief projectpad, niet via NuGet
(geen NuGet-feed nodig zolang alles lokaal naast elkaar staat):

```xml
<ProjectReference Include="..\..\Jabasoft.Stylebook\Shared.Telemetry\Shared.Telemetry.csproj" />
<ProjectReference Include="..\..\Jabasoft.Stylebook\Shared.UI\Shared.UI.csproj" />
```

### Shared.Telemetry aansluiten

1. Voeg de projectverwijzing hierboven toe.
2. Zet dezelfde connection string (`ConnectionStrings:JabasoftBase` in
   `appsettings.json`) — alle apps moeten naar dezelfde database wijzen.
3. Registreer `TelemetryDbContext` en `ITokenUsageRepository` in
   `Program.cs` (zie `TabStudio.Web/Program.cs` in `JabaSoft.TabStudio` voor
   het patroon).
4. Roep `ITokenUsageRepository.RecordAsync(...)` aan bij elke LLM-call.

### Shared.UI (huisstijl) aansluiten

1. Voeg de projectverwijzing hierboven toe.
2. Voeg in de hoofdlayout toe: `<link rel="stylesheet" href="_content/Shared.UI/jabasoft-theme.css">`.
3. Verwijder lokale kopieën van de `--lcars-*`-tokens en de `.shell*`-regels
   uit de eigen `app.css` — die komen nu uit het gedeelde bestand.
4. Voeg (in de app die het toont) een `ProjectReference` naar
   `Jabasoft.Base` toe en gebruik `<TokenUsageOverview />` voor een
   token-verbruikpagina — dit component staat in `Jabasoft.Base`, niet in
   dit repo (optioneel: `Lookback` als `TimeSpan`-parameter, default 30
   dagen).

De Jabasoft WPF-shell (niet ASP.NET Core) laadt `jabasoft-theme.css`
rechtstreeks van schijf via `CoreWebView2.SetVirtualHostNameToFolderMapping`
— zie `Jabasoft/Jabasoft.App/README.md`.

## Migraties (Shared.Telemetry)

```bash
dotnet ef migrations add <Naam> --project Shared.Telemetry --startup-project Shared.Telemetry
dotnet ef database update --project Shared.Telemetry --startup-project Shared.Telemetry
```
