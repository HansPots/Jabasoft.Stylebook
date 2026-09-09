using System.Windows;
using System.Windows.Controls;

namespace Stylebook.Components.Theming;

/// <summary>
/// Lets a Border's or Control's Padding be built from up to four
/// independently DynamicResource-bound (or plain literal) side values -
/// same reasoning and usage pattern as MarginParts/CornerRadiusParts. Set
/// any subset of the four sides; a side left unset is 0. Don't ALSO set a
/// plain Padding attribute on the same element - this fully replaces it.
/// Border.Padding and Control.Padding are separate DependencyProperties
/// (both just happen to be named "Padding"), so OnPartChanged switches on
/// the target element's actual type to know which one to set.
/// </summary>
public static class PaddingParts
{
    public static readonly DependencyProperty LeftProperty = DependencyProperty.RegisterAttached(
        "Left", typeof(Thickness), typeof(PaddingParts), new PropertyMetadata(new Thickness(0), OnPartChanged));

    public static readonly DependencyProperty TopProperty = DependencyProperty.RegisterAttached(
        "Top", typeof(Thickness), typeof(PaddingParts), new PropertyMetadata(new Thickness(0), OnPartChanged));

    public static readonly DependencyProperty RightProperty = DependencyProperty.RegisterAttached(
        "Right", typeof(Thickness), typeof(PaddingParts), new PropertyMetadata(new Thickness(0), OnPartChanged));

    public static readonly DependencyProperty BottomProperty = DependencyProperty.RegisterAttached(
        "Bottom", typeof(Thickness), typeof(PaddingParts), new PropertyMetadata(new Thickness(0), OnPartChanged));

    public static void SetLeft(DependencyObject element, Thickness value) => element.SetValue(LeftProperty, value);

    public static Thickness GetLeft(DependencyObject element) => (Thickness)element.GetValue(LeftProperty);

    public static void SetTop(DependencyObject element, Thickness value) => element.SetValue(TopProperty, value);

    public static Thickness GetTop(DependencyObject element) => (Thickness)element.GetValue(TopProperty);

    public static void SetRight(DependencyObject element, Thickness value) => element.SetValue(RightProperty, value);

    public static Thickness GetRight(DependencyObject element) => (Thickness)element.GetValue(RightProperty);

    public static void SetBottom(DependencyObject element, Thickness value) => element.SetValue(BottomProperty, value);

    public static Thickness GetBottom(DependencyObject element) => (Thickness)element.GetValue(BottomProperty);

    private static void OnPartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var padding = new Thickness(GetLeft(d).Left, GetTop(d).Left, GetRight(d).Left, GetBottom(d).Left);

        switch (d)
        {
            case Border border:
                border.Padding = padding;
                break;
            case Control control:
                control.Padding = padding;
                break;
        }
    }
}
