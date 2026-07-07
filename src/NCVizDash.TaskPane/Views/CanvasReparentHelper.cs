using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NCVizDash.TaskPane.Views;

/// <summary>Helpers for moving the shared <see cref="Controls.DashboardCanvas"/> between hosts.</summary>
internal static class CanvasReparentHelper
{
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
