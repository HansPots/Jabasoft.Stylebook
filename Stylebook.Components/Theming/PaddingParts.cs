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
///
/// The matching *Negative flag (LeftNegative/TopNegative/RightNegative/
/// BottomNegative, default False) flips that one side negative regardless
/// of whether its value came from a token or a literal - see MarginParts'
/// class comment, same mechanism. Padding is rarely negative in practice,
/// but WPF doesn't forbid it, so this stays symmetric with MarginParts
/// rather than being a special case.
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

    public static readonly DependencyProperty LeftNegativeProperty = DependencyProperty.RegisterAttached(
        "LeftNegative", typeof(bool), typeof(PaddingParts), new PropertyMetadata(false, OnPartChanged));

    public static readonly DependencyProperty TopNegativeProperty = DependencyProperty.RegisterAttached(
        "TopNegative", typeof(bool), typeof(PaddingParts), new PropertyMetadata(false, OnPartChanged));

    public static readonly DependencyProperty RightNegativeProperty = DependencyProperty.RegisterAttached(
        "RightNegative", typeof(bool), typeof(PaddingParts), new PropertyMetadata(false, OnPartChanged));

    public static readonly DependencyProperty BottomNegativeProperty = DependencyProperty.RegisterAttached(
        "BottomNegative", typeof(bool), typeof(PaddingParts), new PropertyMetadata(false, OnPartChanged));

    public static void SetLeft(DependencyObject element, Thickness value) => element.SetValue(LeftProperty, value);

    public static Thickness GetLeft(DependencyObject element) => (Thickness)element.GetValue(LeftProperty);

    public static void SetTop(DependencyObject element, Thickness value) => element.SetValue(TopProperty, value);

    public static Thickness GetTop(DependencyObject element) => (Thickness)element.GetValue(TopProperty);

    public static void SetRight(DependencyObject element, Thickness value) => element.SetValue(RightProperty, value);

    public static Thickness GetRight(DependencyObject element) => (Thickness)element.GetValue(RightProperty);

    public static void SetBottom(DependencyObject element, Thickness value) => element.SetValue(BottomProperty, value);

    public static Thickness GetBottom(DependencyObject element) => (Thickness)element.GetValue(BottomProperty);

    public static void SetLeftNegative(DependencyObject element, bool value) => element.SetValue(LeftNegativeProperty, value);

    public static bool GetLeftNegative(DependencyObject element) => (bool)element.GetValue(LeftNegativeProperty);

    public static void SetTopNegative(DependencyObject element, bool value) => element.SetValue(TopNegativeProperty, value);

    public static bool GetTopNegative(DependencyObject element) => (bool)element.GetValue(TopNegativeProperty);

    public static void SetRightNegative(DependencyObject element, bool value) => element.SetValue(RightNegativeProperty, value);

    public static bool GetRightNegative(DependencyObject element) => (bool)element.GetValue(RightNegativeProperty);

    public static void SetBottomNegative(DependencyObject element, bool value) => element.SetValue(BottomNegativeProperty, value);

    public static bool GetBottomNegative(DependencyObject element) => (bool)element.GetValue(BottomNegativeProperty);

    private static void OnPartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var padding = new Thickness(
            Signed(GetLeft(d).Left, GetLeftNegative(d)),
            Signed(GetTop(d).Left, GetTopNegative(d)),
            Signed(GetRight(d).Left, GetRightNegative(d)),
            Signed(GetBottom(d).Left, GetBottomNegative(d)));

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

    private static double Signed(double value, bool negative) => negative ? -Math.Abs(value) : value;
}
