using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NCVizDash.Models;
using NCVizDash.TaskPane.ViewModels;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace NCVizDash.TaskPane.Views;

/// <summary>
/// Code-behind for the Workbook Explorer left panel.
/// Handles field drag-start (so fields can be dropped onto the canvas) and the
/// hover data-preview popup.
/// </summary>
public sealed partial class ExplorerPanelView : System.Windows.Controls.UserControl
{
    /// <summary>Clipboard/drag-drop data format identifying a dragged <see cref="FieldDescriptor"/>.</summary>
    public const string FieldDragFormat = "NCVizDash.Field";

    private Point _dragStartPoint;
    private readonly DispatcherTimer _previewHoverTimer;
    private DataSourceDescriptor? _pendingPreviewSource;

    /// <summary>Initialises the view.</summary>
    public ExplorerPanelView()
    {
        InitializeComponent();

        // Small delay before showing the preview popup so a quick mouse pass-over
        // doesn't fire an unnecessary data read.
        _previewHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _previewHoverTimer.Tick += PreviewHoverTimer_Tick;
    }

    private ExplorerPanelViewModel? ViewModel => DataContext as ExplorerPanelViewModel;

    // ── Field drag source ────────────────────────────────────────────────────

    private void FieldRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void FieldRow_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not FrameworkElement { DataContext: FieldDescriptor field } element) return;

        var current = e.GetPosition(null);
        var diff = _dragStartPoint - current;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var data = new DataObject(FieldDragFormat, field);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
    }

    // ── Hover data preview ────────────────────────────────────────────────────

    private void DataSourceHeader_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DataSourceDescriptor source }) return;

        _pendingPreviewSource = source;
        _previewHoverTimer.Stop();
        _previewHoverTimer.Start();
    }

    private void DataSourceHeader_MouseLeave(object sender, MouseEventArgs e)
    {
        _previewHoverTimer.Stop();
        _pendingPreviewSource = null;
        PreviewPopup.IsOpen = false;
        ViewModel?.ClearPreview();
    }

    private async void PreviewHoverTimer_Tick(object? sender, EventArgs e)
    {
        _previewHoverTimer.Stop();

        if (_pendingPreviewSource is null || ViewModel is null) return;

        PreviewPopup.PlacementTarget = this;
        PreviewPopup.IsOpen = true;

        await ViewModel.LoadPreviewCommand.ExecuteAsync(_pendingPreviewSource);
    }
}
