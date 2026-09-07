# JabaSoft — huisstijlhandboek

Dit document beschrijft de visuele regels die voor **alle** JabaSoft-apps
gelden (TabStudio, LocalAiStudio, de Jabasoft WPF-shell, en toekomstige
apps). De design-tokens staan in `wwwroot/jabasoft-theme.css` in dit
project — dat is het enige bestand dat de daadwerkelijke waarden bevat.
Elke app laadt dat bestand rechtstreeks (geen kopie, zie `README.md`).

Oorspronkelijk geschreven voor `JabaSoft.LocalAiStudio/LocalAiStudio.Web`; nu de
gedeelde standaard voor de hele JabaSoft-familie.

## Ontwerpprincipes

- De interface heeft een technische LCARS-geïnspireerde uitstraling.
- Zwart vormt de rustige basis; accentkleuren geven structuur en status aan.
- Gebruik bestaande kleuren en tokens. Voeg niet lokaal een nieuwe kleur toe
  wanneer een bestaande stijlkleur dezelfde functie heeft.
- Houd afstanden, balkbreedtes, rondingen en titelstijlen consistent tussen
  apps — dat is het hele punt van dit gedeelde bestand.
- Werkruimte heeft voorrang: decoratieve vlakken mogen de content niet onnodig
  verkleinen.

## Globale design-tokens

| Token | Huidige waarde | Gebruik |
| --- | ---: | --- |
| `--lcars-space` | `6px` | Standaardruimte tussen en rondom hoofdcomponenten |
| `--lcars-gap` | `var(--lcars-space)` | Compatibele alias voor bestaande shell-layout |
| `--lcars-header-height` | `175px` | Totale hoogte van de header |
| `--lcars-footer-height` | `43px` | Totale hoogte van de footer |
| `--lcars-menu-width` | `238px` | Breedte van het linkermenu |
| `--lcars-actionrail-width` | `41px` | Breedte van action rail en refreshknop |

Gebruik `var(--lcars-space)` voor een standaardafstand van 6px. Vermijd een
losse `6px` wanneer de waarde de afstand tussen hoofdonderdelen voorstelt.
Kleinere interne details of bewust afwijkende afstanden mogen een eigen waarde
houden, maar moeten in het component worden toegelicht.

## Kleuren

### Globale kleuren

| Token | Functie |
| --- | --- |
| `--lcars-black` | Achtergrond van shell, panelen en tussenruimtes |
| `--lcars-header-start`, `--lcars-header-end` | Headergradient |
| `--lcars-menu` | Achtergrond van het linkermenu |
| `--lcars-actionrail-start`, `--lcars-actionrail-end` | Paarse rail- en actiekleuren |
| `--lcars-footer-start`, `--lcars-footer-end` | Gereserveerde footeraccenten |
| `--lcars-text-dark`, `--lcars-text-light` | Donkere en lichte standaardtekst |

Componenten mogen lokale aliassen zoals `--purple`, `--orange` en `--panel`
gebruiken wanneer deze bovenaan het geïsoleerde CSS-bestand worden verklaard.
Gebruik bij voorkeur een bestaande stijlkleur uit een verwant component.

## Shell-layout

De shell bestaat uit drie kolommen en drie rijen:

1. Header over de volledige breedte.
2. Menu, content-area en action rail.
3. Footer over de volledige breedte.

De shell gebruikt `--lcars-space` zowel als grid-gap als buitenpadding. Daardoor
staat een element zonder eigen rechtermarge exact 6px van de schermrand.

Deze grid-structuur (`.shell`, `.shell-header`, `.shell-menu`,
`.shell-content`, `.shell-actionrail`, `.shell-footer`) staat kant-en-klaar
in `jabasoft-theme.css` — elke app (inclusief de Jabasoft WPF-shell, als
gewone HTML in een WebView2) gebruikt dezelfde classes.

## Menu

- Breedte: `--lcars-menu-width` (`238px`).
- Menu-items zijn zwart met perzikkleurige tekst.
- Actieve items krijgen geen omlijning en behouden dezelfde tekststijl.
- Hover gebruikt de bestaande oranje accentkleur met zwarte tekst en iconen.
- De gouden onderstrook toont de versie rechtsonder.
- Hoofdrondingen volgen de bestaande waarden van `30px` en `42px`.
- Basisopmaak voor menu-links/-knoppen staat als `.shell-menu .nav a/button`
  in `jabasoft-theme.css` — apps met een eigen `ShellMenu.razor.css` mogen
  dit verfijnen, maar niet de kleuren/tokens overschrijven.

## Content-panelen

- Panelen gebruiken een zwarte achtergrond en de bestaande grijze kaderkleur.
- Titelbalken (headerboxen, paneeltitels) zijn paars, links uitgelijnd, vet
  en alleen bovenaan afgerond met `10px`.
- De titeltekst is bewust iets kleiner dan de headerlabels.

## Action rail

- Breedte: `--lcars-actionrail-width` (`41px`).
- De railachtergrond is zwart.
- Actieknoppen gebruiken een bestaande paarse railkleur en hebben alleen
  rechts ronde hoeken.

## Footer

- Totale hoogte: `--lcars-footer-height` (`43px`).
- De footer is zwart en gebruikt rondom `var(--lcars-space)` ruimte.
- Statusblokken zijn even breed, schalen responsief mee en zijn `31px` hoog.
- Meterlijnen tonen het gebruikte aandeel zwart en 4px dik; het resterende
  aandeel gebruikt de bestaande grijze kaderkleur en is 2px dik.

## Token-verbruik dashboard

- De gedeelde `TokenUsageOverview.razor`-component staat in `Jabasoft.Base`
  (niet in dit project) en toont totalen plus per-week inklapbare details.
- Stijl staat onder `.token-usage-dashboard` in `jabasoft-theme.css`.
- De Jabasoft WPF-shell toont dit via een eigen, losstaande `BlazorWebView`
  (niet een HTML-pagina) die dezelfde `TokenUsageOverview.razor`-component
  rechtstreeks host — geen aparte HTML/CSS-kopie.

## Typografie

- Algemene UI: `Segoe UI`, met Arial en sans-serif als fallback.
- Labels in titelbalken zijn in hoofdletters, links uitgelijnd en vet.
- Headerlabels: `12px`.
- Contentlabels: `11px`.
- Vermijd zeer kleine tekst onder 9px, behalve voor bewust secundaire metadata.

## Responsief gedrag

- Gebruik `minmax(0, 1fr)` voor gelijk verdeelde flexibele kolommen.
- Voorkom vaste contentbreedtes wanneer een verhouding volstaat.
- Tekst die niet past krijgt `text-overflow: ellipsis` in plaats van de layout
  breder te drukken.
- Controleer wijzigingen minimaal rond de bestaande breakpoint van `1500px`.

## Onderhoudsregels

1. Wijzig alleen `jabasoft-theme.css` in dit project — nooit een lokale kopie
   van tokens in een app's eigen `wwwroot`. Een app-specifieke `app.css` mag
   alleen dingen bevatten die echt niet gedeeld zijn.
2. Pas eerst een bestaand token aan voordat dezelfde waarde op meerdere
   plekken handmatig wordt gewijzigd.
3. Nieuwe globale tokens horen in `jabasoft-theme.css` én in dit stylebook.
4. Component-specifieke regels blijven in het bijbehorende `.razor.css`-bestand
   van de app zelf.
5. Leg afwijkende vaste maten uit in het commentaarblok van het component.
6. Controleer geïsoleerde Razor-CSS na een rebuild; Hot Reload verwerkt deze
   bestanden niet altijd onmiddellijk. Wijzigingen aan `jabasoft-theme.css`
   zelf zijn in Development wél direct zichtbaar na een browser-herlaad
   (static web assets worden dan rechtstreeks vanuit dit project geserveerd).
