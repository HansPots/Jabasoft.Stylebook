namespace Stylebook.Data.Entities;

/// <summary>
/// One registered JabaSoft-familielid (bv. "Jabasoft", "LocalAiStudio")
/// waarvan de pagina's in Stylebook gestyled kunnen worden - zie Page.
/// Naam botst met System.Windows.Application (WPF) - elke consumer die
/// ook WPF-namespaces gebruikt, aliast dit (zie MainWindow.xaml.cs,
/// "using DataApplication = ...").
/// </summary>
public class Application : IAuditableEntity
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public override string ToString() => Name;
}
