# Jabasoft.Shared

Gedeelde class libraries voor de hele JabaSoft-familie (`JabaSoft.TabStudio`,
`JabaSoft.LocalAiStudio`, `Jabasoft`, en toekomstige apps). Bevat geen eigen
uitvoerbare app — alleen herbruikbare projecten waar andere JabaSoft-repos
naar verwijzen.

## Projecten

### Shared.Telemetry

Losstaande database (`JabaSoftTelemetry`) voor LLM-tokengebruik, gedeeld
door alle apps. Elke rij is één LLM-call met tijdstip en app-naam. Zie
`Shared.Telemetry/README.md`.

### Shared.UI

Razor Class Library met de huisstijl:
- `wwwroot/jabasoft-theme.css` — het enige canonieke CSS-bestand van de hele
  familie. Niet kopiëren; altijd rechtstreeks laden (zie hieronder).
- `docs/STYLEBOOK.md` — het huisstijlhandboek: de geschreven regels achter
  de tokens in `jabasoft-theme.css`.
- `TokenUsageDashboard.razor` — herbruikbare Blazor-component die
  tokenverbruik toont (per app, of over alle apps heen).

## Hergebruik door een andere app

Alle repos staan als sibling-mappen naast elkaar in `C:\Repos`. Andere apps
verwijzen naar deze projecten via een relatief projectpad, niet via NuGet
(geen NuGet-feed nodig zolang alles lokaal naast elkaar staat):

```xml
<ProjectReference Include="..\..\Jabasoft.Shared\Shared.Telemetry\Shared.Telemetry.csproj" />
<ProjectReference Include="..\..\Jabasoft.Shared\Shared.UI\Shared.UI.csproj" />
```

### Shared.Telemetry aansluiten

1. Voeg de projectverwijzing hierboven toe.
2. Zet dezelfde connection string (`ConnectionStrings:JabaSoftTelemetry` in
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
4. Gebruik `<TokenUsageDashboard Application="JouwAppNaam" />` voor een
   token-verbruikpagina in de app zelf.

De Jabasoft WPF-shell (niet ASP.NET Core) laadt `jabasoft-theme.css`
rechtstreeks van schijf via `CoreWebView2.SetVirtualHostNameToFolderMapping`
— zie `Jabasoft/Jabasoft.App/README.md`.

## Migraties (Shared.Telemetry)

```bash
dotnet ef migrations add <Naam> --project Shared.Telemetry --startup-project Shared.Telemetry
dotnet ef database update --project Shared.Telemetry --startup-project Shared.Telemetry
```
