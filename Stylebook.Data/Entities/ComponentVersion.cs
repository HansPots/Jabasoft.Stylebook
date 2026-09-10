namespace Stylebook.Data.Entities;

/// <summary>
/// Snapshot van een StylebookComponent's Name/Title/BodyText/Xaml vlak
/// vóór een wijziging (hernoemen, "Genereer en opslaan", of
/// "Overnemen") - alleen een noodgreep om via SQL terug te vinden wat
/// een component vroeger was, geen UI. Nooit zelf gemuteerd na
/// aanmaken. Alleen de laatste 5 per component blijven bewaard, zie
/// StylebookDbContext.TrimComponentVersions.
/// </summary>
public class ComponentVersion : IAuditableEntity
{
    public int Id { get; set; }

    public int ComponentId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? BodyText { get; set; }

    public string? Xaml { get; set; }

    // Audit fields last, by convention - see feedback_db_audit_timestamps
    // memory. UpdatedAtUtc blijft in de praktijk altijd gelijk aan
    // CreatedAtUtc (een versie wordt nooit bewerkt), maar krijgt 'm toch
    // voor consistentie met elke andere entiteit.
    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
