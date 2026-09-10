using System.Windows;
using System.Windows.Controls;

namespace Stylebook.Components.Theming;

/// <summary>
/// Lets a Border's CornerRadius be built from up to four independently
/// DynamicResource-bound (or plain literal) corner values, since WPF's own
/// CornerRadius attribute syntax can only ever be ONE markup extension or
/// ONE literal comma-list - never several {DynamicResource ...} references
/// combined in one value, tokens mixed with literals, or even several
/// tokens together. Set any subset of the four corners on the Border, e.g.
///
///   &lt;Border theming:CornerRadiusParts.TopLeft="{DynamicResource RadiusSmall}"
///           theming:CornerRadiusParts.TopRight="{DynamicResource RadiusSmall}" /&gt;
///
/// - each is its own separate attribute, so each is legally either a whole
/// token reference or a whole literal number, satisfying WPF's rule either
/// way. A corner left unset defaults to 0. Every change (a literal edit, or
/// a live DynamicResource update when a theme's radius token changes)
/// recomputes the Border's real CornerRadius immediately. Only .TopLeft is
/// read off each supplied value - a Radius-category design token always
/// resolves to a uniform CornerRadius (see DbThemeBuilder.Build), so any
/// one corner of it carries the token's actual magnitude.
///
/// Conventie voor wie deze parts gebruikt: zet ALTIJD alle vier de hoeken
/// expliciet neer, ook de hoeken die op 0 blijven staan - nooit stilzwijgend
/// op de "unset = 0"-default hierboven leunen. Zo is in de XAML zelf in één
/// oogopslag duidelijk welke hoeken bewust rond zijn en welke bewust recht.
/// </summary>
public static class CornerRadiusParts
{
    public static readonly DependencyProperty TopLeftProperty = DependencyProperty.RegisterAttached(
        "TopLeft", typeof(CornerRadius), typeof(CornerRadiusParts), new PropertyMetadata(new CornerRadius(0), OnPartChanged));

    public static readonly DependencyProperty TopRightProperty = DependencyProperty.RegisterAttached(
        "TopRight", typeof(CornerRadius), typeof(CornerRadiusParts), new PropertyMetadata(new CornerRadius(0), OnPartChanged));

    public static readonly DependencyProperty BottomRightProperty = DependencyProperty.RegisterAttached(
        "BottomRight", typeof(CornerRadius), typeof(CornerRadiusParts), new PropertyMetadata(new CornerRadius(0), OnPartChanged));

    public static readonly DependencyProperty BottomLeftProperty = DependencyProperty.RegisterAttached(
        "BottomLeft", typeof(CornerRadius), typeof(CornerRadiusParts), new PropertyMetadata(new CornerRadius(0), OnPartChanged));

    public static void SetTopLeft(DependencyObject element, CornerRadius value) => element.SetValue(TopLeftProperty, value);

    public static CornerRadius GetTopLeft(DependencyObject element) => (CornerRadius)element.GetValue(TopLeftProperty);

    public static void SetTopRight(DependencyObject element, CornerRadius value) => element.SetValue(TopRightProperty, value);

    public static CornerRadius GetTopRight(DependencyObject element) => (CornerRadius)element.GetValue(TopRightProperty);

    public static void SetBottomRight(DependencyObject element, CornerRadius value) => element.SetValue(BottomRightProperty, value);

    public static CornerRadius GetBottomRight(DependencyObject element) => (CornerRadius)element.GetValue(BottomRightProperty);

    public static void SetBottomLeft(DependencyObject element, CornerRadius value) => element.SetValue(BottomLeftProperty, value);

    public static CornerRadius GetBottomLeft(DependencyObject element) => (CornerRadius)element.GetValue(BottomLeftProperty);

    private static void OnPartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Border border)
        {
            return;
        }

        border.CornerRadius = new CornerRadius(
            GetTopLeft(border).TopLeft,
            GetTopRight(border).TopRight,
            GetBottomRight(border).BottomRight,
            GetBottomLeft(border).BottomLeft);
    }
}
