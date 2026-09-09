namespace Stylebook.Data.Entities;

/// <summary>
/// One named screen of an Application (bv. Jabasoft's "Hoofdscherm") -
/// which Component occupies which Basis-regio for this specific page is
/// stored per row in PageRegion, not here. Naam botst met
/// System.Windows.Controls.Page (WPF) - elke consumer die ook WPF-
/// namespaces gebruikt, aliast dit (zie MainWindow.xaml.cs,
/// "using DataPage = ...").
/// </summary>
public class Page : IAuditableEntity
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public override string ToString() => Name;
}
