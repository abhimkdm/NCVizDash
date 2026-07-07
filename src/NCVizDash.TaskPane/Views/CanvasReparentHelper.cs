using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NCVizDash.TaskPane.Controls;
using NCVizDash.TaskPane.ViewModels;

namespace NCVizDash.TaskPane.Views;

/// <summary>Helpers for moving the shared <see cref="DashboardCanvas"/> between hosts.</summary>
internal static class CanvasReparentHelper
{
    /// <summary>
    /// Re-applies canvas dependencies after re-parenting. Bindings to the task-pane
    /// <see cref="CanvasPanelView"/> DataContext break once the canvas is hosted elsewhere.
    /// </summary>
    public static void ApplyCanvasBindings(DashboardCanvas canvas, CanvasPanelViewModel vm)
    {
        canvas.DataContext = vm;
        canvas.ViewModel = vm;
        canvas.RenderCoordinator = vm.RenderCoordinator;
        canvas.FilterManager = vm.FilterManager;
        canvas.GlobalFilterManager = vm.GlobalFilterManager;
        canvas.Theme = vm.ActiveTheme;
        canvas.ShowGrid = vm.ShowGrid;
        canvas.SnapToGrid = vm.SnapToGrid;
    }

    /// <summary>Removes <paramref name="element"/> from its current visual/logical parent.</summary>
    public static void Detach(FrameworkElement element)
    {
        switch (element.Parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Decorator decorator:
                decorator.Child = null;
                break;
            case ContentControl contentControl:
                contentControl.Content = null;
                break;
        }
    }

    /// <summary>Returns the canvas to the task-pane host grid after a pop-out or presentation window closes.</summary>
    public static void RestoreToGrid(FrameworkElement canvas, Panel hostGrid)
    {
        Detach(canvas);

        if (!hostGrid.Children.Contains(canvas))
            hostGrid.Children.Add(canvas);

        canvas.InvalidateMeasure();
        canvas.InvalidateArrange();
    }
}
