using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Stylebook.Components.Controls;

/// <summary>
/// A titled card with an arbitrary content area - see Card.xaml for the
/// full explanation of the two ways this takes content from its caller
/// (the Title property, and the Body ContentPresenter for child content).
///
/// [ContentProperty(nameof(Body))] redirects implicit XAML child content
/// (&lt;controls:Card&gt;...&lt;/controls:Card&gt;) to Body instead of the
/// inherited ContentControl.Content - Content is already spoken for
/// internally (InitializeComponent assigns Card.xaml's own Border/
/// StackPanel tree to it), so letting a caller's child content also
/// target Content would silently overwrite that internal tree instead of
/// flowing into the ContentPresenter.
/// </summary>
[ContentProperty(nameof(Body))]
public partial class Card : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(Card), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body), typeof(object), typeof(Card), new PropertyMetadata(null));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object Body
    {
        get => GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public Card()
    {
        InitializeComponent();
    }
}
