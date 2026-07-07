using System.Windows;
using System.Windows.Input;
using NCVizDash.TaskPane.Controls;
using NCVizDash.TaskPane.ViewModels;

namespace NCVizDash.TaskPane.Views;

/// <summary>
/// Non-modal window that hosts the live <see cref="DashboardCanvas"/> while the
/// user works in Excel alongside it. The canvas is re-parented from the task pane
/// (not cloned) and restored when this window closes.
/// </summary>
public sealed partial class PopOutDashboardWindow : Window
{
    private readonly DashboardCanvas _canvas;
    private readonly CanvasPanelViewModel _panel;

    /// <summary>Detaches <paramref name="canvas"/> into this window until it is closed.</summary>
    public PopOutDashboardWindow(DashboardCanvas canvas, CanvasPanelViewModel panel)
    {
        InitializeComponent();

        _canvas = canvas;
        _panel = panel;
        _canvas.IsHitTestVisible = true;

        FilterBar.DataContext = panel.GlobalFilterBar;

        var dashboardName = panel.ActiveDashboard?.Name;
        if (!string.IsNullOrWhiteSpace(dashboardName))
        {
            Title = $"NC VizDash — {dashboardName}";
            TitleText.Text = dashboardName;
        }

        CanvasHostGrid.Children.Add(_canvas);

        Loaded += OnLoaded;

        // Release the canvas before Closed handlers in the task pane re-parent it.
        Closing += (_, _) => CanvasReparentHelper.Detach(_canvas);
        Closed += (_, _) => _canvas.IsHitTestVisible = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FitToWorkArea();

        CanvasReparentHelper.ApplyCanvasBindings(_canvas, _panel);
        _panel.RequestRenderAllWidgets();

        _canvas.InvalidateMeasure();
        _canvas.InvalidateArrange();
        CanvasScrollViewer.ScrollToHorizontalOffset(0);
        CanvasScrollViewer.ScrollToVerticalOffset(0);
    }

    /// <summary>Sizes and centers the window within the current monitor work area.</summary>
    private void FitToWorkArea()
    {
        var area = SystemParameters.WorkArea;

        MaxWidth = area.Width;
        MaxHeight = area.Height;

        Width = Math.Min(1024, Math.Max(MinWidth, area.Width * 0.92));
        Height = Math.Min(720, Math.Max(MinHeight, area.Height * 0.88));

        Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
        Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }
}
