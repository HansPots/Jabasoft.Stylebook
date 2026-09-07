using System.Windows;
using System.Windows.Controls;

namespace Stylebook.Components.Controls;

/// <summary>
/// A titled card with an arbitrary content area - see Card.xaml for the
/// full explanation of the two ways this takes content from its caller
/// (the Title property, and the ContentPresenter for child content).
/// </summary>
public partial class Card : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(Card), new PropertyMetadata(string.Empty));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public Card()
    {
        InitializeComponent();
    }
}
