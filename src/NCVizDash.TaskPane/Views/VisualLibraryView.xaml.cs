using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NCVizDash.TaskPane.ViewModels;
using Point = System.Windows.Point;

namespace NCVizDash.TaskPane.Views;

/// <summary>Code-behind for the Visual Library right panel.</summary>
public sealed partial class VisualLibraryView : System.Windows.Controls.UserControl
{
    /// <summary>Clipboard/drag-drop data format identifying a dragged <see cref="VisualTypeEntry"/>.</summary>
    public const string VisualDragFormat = "NCVizDash.VisualType";

    private Point _dragStartPoint;
    private VisualTypeEntry? _pendingDragEntry;
    private FrameworkElement? _dragCaptureElement;

    /// <summary>Initialises the view.</summary>
    public VisualLibraryView()
    {
        InitializeComponent();
        PreviewMouseMove += OnVisualDragPreviewMouseMove;
        PreviewMouseLeftButtonUp += OnVisualDragPreviewMouseLeftButtonUp;
    }

    private void VisualTile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: VisualTypeEntry entry } element)
            return;

        _dragStartPoint = e.GetPosition(null);
        _pendingDragEntry = entry;
        _dragCaptureElement = element;
        element.CaptureMouse();
    }

    private void OnVisualDragPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCaptureElement is null || _pendingDragEntry is null)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CancelVisualDrag();
            return;
        }

        var current = e.GetPosition(null);
        var diff = _dragStartPoint - current;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var entry = _pendingDragEntry;
        var element = _dragCaptureElement;
        CancelVisualDrag();

        var data = new DataObject(VisualDragFormat, entry);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
    }

    private void OnVisualDragPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        CancelVisualDrag();

    private void CancelVisualDrag()
    {
        _dragCaptureElement?.ReleaseMouseCapture();
        _dragCaptureElement = null;
        _pendingDragEntry = null;
    }
}
