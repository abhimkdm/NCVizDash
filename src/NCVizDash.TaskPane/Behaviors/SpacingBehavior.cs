using System.Windows;
using System.Windows.Controls;

namespace NCVizDash.TaskPane.Behaviors;

/// <summary>
/// Attached-property replacement for WPF's <c>StackPanel.Spacing</c> property,
/// which only exists on modern .NET's WPF (added well after .NET Framework 4.8's
/// <c>PresentationFramework.dll</c> was last updated) — this project targets net48
/// for VSTO compatibility, so the native property isn't available. Applies a
/// margin to every child after the first, in the panel's orientation direction,
/// achieving the same visual spacing without the native property.
/// </summary>
public static class SpacingBehavior
{
    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.RegisterAttached(
            "Spacing", typeof(double), typeof(SpacingBehavior),
            new PropertyMetadata(0d, OnSpacingChanged));

    public static double GetSpacing(DependencyObject obj) => (double)obj.GetValue(SpacingProperty);
    public static void SetSpacing(DependencyObject obj, double value) => obj.SetValue(SpacingProperty, value);

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not System.Windows.Controls.Panel panel) return;

        // Children may not exist yet at the moment XAML sets this property, and can
        // also change later (ItemsControl-generated content, dynamic Add/Remove) —
        // re-apply whenever the logical children change, not just once at load.
        panel.Loaded -= Panel_LayoutRefresh;
        panel.Loaded += Panel_LayoutRefresh;

        ApplySpacing(panel, (double)e.NewValue);
    }

    private static void Panel_LayoutRefresh(object sender, RoutedEventArgs e)
    {
        var panel = (System.Windows.Controls.Panel)sender;
        ApplySpacing(panel, GetSpacing(panel));
    }

    private static void ApplySpacing(System.Windows.Controls.Panel panel, double spacing)
    {
        var isHorizontal = panel is not StackPanel sp || sp.Orientation == Orientation.Horizontal;

        for (var i = 0; i < panel.Children.Count; i++)
        {
            if (panel.Children[i] is not FrameworkElement child) continue;

            var margin = child.Margin;
            if (i == 0)
            {
                // First child keeps whatever margin it already had on its leading edge.
                continue;
            }

            child.Margin = isHorizontal
                ? new Thickness(spacing, margin.Top, margin.Right, margin.Bottom)
                : new Thickness(margin.Left, spacing, margin.Right, margin.Bottom);
        }
    }
}
