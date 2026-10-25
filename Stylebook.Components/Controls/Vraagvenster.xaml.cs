using System.Windows;
using System.Windows.Input;

namespace Stylebook.Components.Controls;

/// <summary>
/// Interaction logic for Vraagvenster.xaml - see that file for what it looks like.
///
/// Vervangt MessageBox.Show(..., MessageBoxButton.YesNo) in de apps: dezelfde
/// vraag, maar in de huisstijl en midden boven het venster van de app.
/// </summary>
public partial class Vraagvenster : Window
{
    private Vraagvenster()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Stelt een ja/nee-vraag en wacht op het antwoord. True = <paramref name="ja"/> geklikt; Nee, Escape of
    /// sluiten = false.
    /// </summary>
    /// <param name="titel">Komt in de accentbalk bovenin.</param>
    /// <param name="tekst">De vraag zelf.</param>
    /// <param name="details">Optioneel: een selecteerbare regel eronder, bijv. de link waar het om gaat.</param>
    /// <param name="letOp">Optioneel: een waarschuwing in accentkleur onderaan.</param>
    public static bool Vraag(string titel, string tekst, string? details = null, string? letOp = null,
                             string ja = "Ja", string nee = "Nee")
    {
        var venster = new Vraagvenster();
        venster.TitelText.Text = titel;
        venster.Title = titel;
        venster.TekstBlok.Text = tekst;
        if (!string.IsNullOrWhiteSpace(details))
        {
            venster.DetailBlok.Text = details;
            venster.DetailBlok.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(letOp))
        {
            venster.LetOpBlok.Text = letOp;
            venster.LetOpBlok.Visibility = Visibility.Visible;
        }

        venster.JaButton.Content = ja;
        venster.NeeButton.Content = nee;

        var eigenaar = Application.Current?.MainWindow;
        if (eigenaar is { IsLoaded: true } && !ReferenceEquals(eigenaar, venster))
        {
            venster.Owner = eigenaar;
        }
        else
        {
            venster.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return venster.ShowDialog() == true;
    }

    private void JaButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Titelbalk_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}
