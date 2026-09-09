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
///   &lt;Border theming:MarginParts.Left="-18" theming:MarginParts.Top="-18"
///           theming:MarginParts.Right="-18"
///           theming:MarginParts.Bottom="{DynamicResource SpaceSmall}" /&gt;
///
/// - each side is its own separate attribute, so each is legally either a
/// whole token reference or a whole literal number (there's no token for a
/// negative offset, so those sides above stay plain literals - exactly as
/// legal as a token side, just not driven by the Stylebook). Don't ALSO set
/// a plain Margin attribute on the same element - like CornerRadiusParts,
/// this fully replaces it; a side left unset here becomes 0, not whatever a
/// separate Margin attribute might have said. Only .Left is read off each
/// supplied value - a Spacing-category design token always resolves to a
/// uniform Thickness (see DbThemeBuilder.Build), so any one side of it
/// carries the token's actual magnitude.
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
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.Margin = new Thickness(
            GetLeft(element).Left,
            GetTop(element).Left,
            GetRight(element).Left,
            GetBottom(element).Left);
    }
}
