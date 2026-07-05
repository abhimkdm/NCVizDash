using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NCVizDash.TaskPane.ViewModels;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace NCVizDash.TaskPane.Views;

/// <summary>Code-behind for the Visual Library right panel.</summary>
public sealed partial class VisualLibraryView : System.Windows.Controls.UserControl
{
    /// <summary>Clipboard/drag-drop data format identifying a dragged <see cref="VisualTypeEntry"/>.</summary>
    public const string VisualDragFormat = "NCVizDash.VisualType";

    private Point _dragStartPoint;

    /// <summary>Initialises the view.</summary>
    public VisualLibraryView() => InitializeComponent();

    private void VisualTile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void VisualTile_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not FrameworkElement { DataContext: VisualTypeEntry entry } element) return;

        var current = e.GetPosition(null);
        var diff = _dragStartPoint - current;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var data = new DataObject(VisualDragFormat, entry);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
    }
}
