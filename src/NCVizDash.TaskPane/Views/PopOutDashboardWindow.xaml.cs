using System.Windows;
using System.Windows.Input;
using NCVizDash.TaskPane.Controls;

namespace NCVizDash.TaskPane.Views;

/// <summary>
/// Non-modal window that hosts the live <see cref="DashboardCanvas"/> while the
/// user works in Excel alongside it. The canvas is re-parented from the task pane
/// (not cloned) and restored when this window closes.
/// </summary>
public sealed partial class PopOutDashboardWindow : Window
{
    private readonly DashboardCanvas _canvas;

    /// <summary>Detaches <paramref name="canvas"/> into this window until it is closed.</summary>
    public PopOutDashboardWindow(DashboardCanvas canvas)
    {
        InitializeComponent();

        _canvas = canvas;
        _canvas.IsHitTestVisible = true;

        var dashboardName = canvas.ViewModel?.ActiveDashboard?.Name;
        if (!string.IsNullOrWhiteSpace(dashboardName))
        {
            Title = $"NC VizDash — {dashboardName}";
            TitleText.Text = dashboardName;
        }

        CanvasHostBorder.Child = _canvas;

        Loaded += (_, _) =>
        {
            _canvas.InvalidateMeasure();
            _canvas.InvalidateArrange();
        };

        // Release the canvas before Closed handlers in the task pane re-parent it.
        Closing += (_, _) => CanvasHostBorder.Child = null;
        Closed += (_, _) => _canvas.IsHitTestVisible = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
