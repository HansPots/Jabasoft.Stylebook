using System.Windows;

namespace Stylebook.Components.Theming;

/// <summary>
/// Lets any FrameworkElement's Margin be built from up to four independently
/// DynamicResource-bound (or plain literal) side values - same reasoning as
/// CornerRadiusParts: WPF's own Margin attribute syntax can only ever be ONE
/// markup extension or ONE literal comma-list, never several tokens (or a
/// token mixed with literals) combined in one value. Set any subset of the
/// four sides on the element, e.g.
///
///   &lt;Border theming:MarginParts.Left="{DynamicResource SpaceMedium}"
///           theming:MarginParts.LeftNegative="True" /&gt;
///
/// - each side is its own separate attribute, so each is legally either a
/// whole token reference or a whole literal number. Don't ALSO set a plain
/// Margin attribute on the same element - like CornerRadiusParts, this
/// fully replaces it; a side left unset here becomes 0, not whatever a
/// separate Margin attribute might have said. Only .Left is read off each
/// supplied value - a Spacing-category design token always resolves to a
/// uniform Thickness (see DbThemeBuilder.Build), so any one side of it
/// carries the token's actual magnitude.
///
/// Every Spacing token is a positive magnitude, but a "pull outside the
/// parent's padding" margin is very often negative - there's no separate
/// "negative token" to reference for that. The matching *Negative flag
/// (LeftNegative/TopNegative/RightNegative/BottomNegative, default False)
/// flips that one side negative regardless of whether its value came from
/// a token or a literal, so a token-driven side can still be negative -
/// e.g. Left="{DynamicResource SpaceMedium}" LeftNegative="True" behaves
/// like a live-updating "-SpaceMedium".
/// </summary>
public static class MarginParts
{
    public static readonly DependencyProperty LeftProperty = DependencyProperty.RegisterAttached(
        "Left", typeof(Thickness), typeof(MarginParts), new PropertyMetadata(new Thickness(0), OnPartChanged));

    public static readonly DependencyProperty TopProperty = DependencyProperty.RegisterAttached(
        "Top", typeof(Thickness), typeof(MarginParts), new PropertyMetadata(new Thickness(0), OnPartChanged));

    public static readonly DependencyProperty RightProperty = DependencyProperty.RegisterAttached(
        "Right", typeof(Thickness), typeof(MarginParts), new PropertyMetadata(new Thickness(0), OnPartChanged));

    public static readonly DependencyProperty BottomProperty = DependencyProperty.RegisterAttached(
        "Bottom", typeof(Thickness), typeof(MarginParts), new PropertyMetadata(new Thickness(0), OnPartChanged));

    public static readonly DependencyProperty LeftNegativeProperty = DependencyProperty.RegisterAttached(
        "LeftNegative", typeof(bool), typeof(MarginParts), new PropertyMetadata(false, OnPartChanged));

    public static readonly DependencyProperty TopNegativeProperty = DependencyProperty.RegisterAttached(
        "TopNegative", typeof(bool), typeof(MarginParts), new PropertyMetadata(false, OnPartChanged));

    public static readonly DependencyProperty RightNegativeProperty = DependencyProperty.RegisterAttached(
        "RightNegative", typeof(bool), typeof(MarginParts), new PropertyMetadata(false, OnPartChanged));

    public static readonly DependencyProperty BottomNegativeProperty = DependencyProperty.RegisterAttached(
        "BottomNegative", typeof(bool), typeof(MarginParts), new PropertyMetadata(false, OnPartChanged));

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
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.Margin = new Thickness(
            Signed(GetLeft(element).Left, GetLeftNegative(element)),
            Signed(GetTop(element).Left, GetTopNegative(element)),
            Signed(GetRight(element).Left, GetRightNegative(element)),
            Signed(GetBottom(element).Left, GetBottomNegative(element)));
    }

    private static double Signed(double value, bool negative) => negative ? -Math.Abs(value) : value;
}
