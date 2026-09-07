# Shared.Telemetry

Losstaande database voor LLM-tokengebruik, gedeeld door alle JabaSoft-apps.
Elke rij is één LLM-call met tijdstip, zodat er een grafiek van verbruik
over tijd gemaakt kan worden, per app of over alle apps heen.

Oorspronkelijk gebouwd in `JabaSoft.TabStudio`; verplaatst naar dit gedeelde
repo zodat elke JabaSoft-app (TabStudio, LocalAiStudio, Jabasoft, en
toekomstige apps) ernaar kan verwijzen zonder kopieën.

## Waarom een aparte database

Dit is een tijdreeks (`TokenUsageEntry` per call, met `Timestamp`) in een
eigen database (`JabasoftBase`), zodat meerdere apps er allemaal in kunnen
schrijven zonder elkaars data te raken, en er één grafiek over alle apps
heen gemaakt kan worden.

## Hergebruik door een app

Zie `../README.md` (root van dit repo) voor de volledige instructies. Kort:

1. Projectverwijzing toevoegen.
2. Zelfde `ConnectionStrings:JabasoftBase` in `appsettings.json`.
3. `TelemetryDbContext` + `ITokenUsageRepository` registreren in `Program.cs`.
4. `RecordAsync(...)` aanroepen bij elke LLM-call.

## Migraties

```bash
dotnet ef migrations add <Naam> --project Shared.Telemetry --startup-project Shared.Telemetry
dotnet ef database update --project Shared.Telemetry --startup-project Shared.Telemetry
```
