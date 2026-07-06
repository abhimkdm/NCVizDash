using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NCVizDash.Models;
using NCVizDash.TaskPane.ViewModels;
using Point = System.Windows.Point;

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

    /// <summary>Drag-drop payload carrying the owning data source id for a field.</summary>
    public const string DataSourceIdDragFormat = "NCVizDash.DataSourceId";

    private Point _dragStartPoint;
    private readonly DispatcherTimer _previewHoverTimer;
    private DataSourceDescriptor? _pendingPreviewSource;

    private FieldDescriptor? _pendingDragField;
    private DataSourceDescriptor? _pendingDragSource;
    private FrameworkElement? _dragCaptureElement;

    /// <summary>Initialises the view.</summary>
    public ExplorerPanelView()
    {
        InitializeComponent();

        _previewHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _previewHoverTimer.Tick += PreviewHoverTimer_Tick;

        PreviewMouseMove += OnFieldDragPreviewMouseMove;
        PreviewMouseLeftButtonUp += OnFieldDragPreviewMouseLeftButtonUp;
    }

    private ExplorerPanelViewModel? ViewModel => DataContext as ExplorerPanelViewModel;

    // ── Field drag source ────────────────────────────────────────────────────

    private void FieldRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FieldDescriptor field } element)
            return;

        _dragStartPoint = e.GetPosition(null);
        _pendingDragField = field;
        _pendingDragSource = GetDataSourceForField(element);
        _dragCaptureElement = element;
        element.CaptureMouse();
    }

    private void OnFieldDragPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCaptureElement is null || _pendingDragField is null)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CancelFieldDrag();
            return;
        }

        var current = e.GetPosition(null);
        var diff = _dragStartPoint - current;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var field = _pendingDragField;
        var source = _pendingDragSource;
        var element = _dragCaptureElement;

        CancelFieldDrag();

        var data = new DataObject(FieldDragFormat, field);
        data.SetData(DataSourceIdDragFormat, source?.Id ?? Guid.Empty);
        DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
    }

    private void OnFieldDragPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        CancelFieldDrag();

    private void CancelFieldDrag()
    {
        _dragCaptureElement?.ReleaseMouseCapture();
        _dragCaptureElement = null;
        _pendingDragField = null;
        _pendingDragSource = null;
    }

    // ── Hover data preview ────────────────────────────────────────────────────

    private void DataSourceHeader_MouseEnter(object sender, MouseEventArgs e)
    {
        var source = GetDataSourceFromSender(sender);
        if (source is null) return;

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

    private static DataSourceDescriptor? GetDataSourceFromSender(object sender)
    {
        if (sender is FrameworkElement { DataContext: DataSourceDescriptor direct })
            return direct;

        return GetDataSourceForField(sender as DependencyObject);
    }

    /// <summary>Walks the visual tree to find the owning <see cref="DataSourceDescriptor"/>.</summary>
    private static DataSourceDescriptor? GetDataSourceForField(DependencyObject? start)
    {
        for (var element = start; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is FrameworkElement { DataContext: DataSourceDescriptor source })
                return source;

            if (element is Expander expander && expander.DataContext is DataSourceDescriptor expanderSource)
                return expanderSource;
        }

        return null;
    }

    /// <summary>
    /// Prevents the Expander from toggling when the user clicks the Generate button
    /// in the header (WPF routes header clicks to the expand/collapse handler).
    /// </summary>
    private void Expander_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject origin)
            return;

        for (var element = origin; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is Button)
            {
                e.Handled = true;
                return;
            }
        }
    }

    private async void PreviewHoverTimer_Tick(object? sender, EventArgs e)
    {
        _previewHoverTimer.Stop();

        var source = _pendingPreviewSource;
        var viewModel = ViewModel;
        if (source is null || viewModel is null)
            return;

        PreviewPopup.PlacementTarget = this;
        PreviewPopup.IsOpen = true;

        try
        {
            await viewModel.LoadPreviewAsync(source);
        }
        catch (Exception ex)
        {
            PreviewPopup.IsOpen = false;
            System.Diagnostics.Debug.WriteLine($"Preview load failed: {ex.Message}");
        }
    }
}
