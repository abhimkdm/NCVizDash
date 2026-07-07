using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NCVizDash.Models;
using NCVizDash.TaskPane.Geometry;
using NCVizDash.TaskPane.ViewModels;

namespace NCVizDash.TaskPane.Views;

/// <summary>
/// Code-behind for the canvas panel. Handles:
/// - Keyboard shortcuts (Delete → delete selected; Ctrl+D → duplicate).
/// - DragDrop routing from the explorer / visual library into <see cref="CanvasPanelViewModel"/>.
/// All interactive widget-level gestures (move/resize/rubber-band) live in
/// <see cref="Controls.DashboardCanvas"/>.
/// </summary>
public sealed partial class CanvasPanelView : System.Windows.Controls.UserControl
{
    private static readonly Brush DragHighlight =
        new SolidColorBrush(Color.FromArgb(255, 103, 58, 183));

    private static readonly Brush DragIdle = Brushes.Transparent;

    /// <summary>Initialises the view.</summary>
    public CanvasPanelView() => InitializeComponent();

    private CanvasPanelViewModel? ViewModel => DataContext as CanvasPanelViewModel;

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private void Canvas_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel is null) return;

        if (e.Key == Key.Delete || e.Key == Key.Back)
        {
            ViewModel.DeleteSelectedWidgetCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            ViewModel.DuplicateSelectedWidgetsCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.ClearSelection();
            e.Handled = true;
        }
    }

    // ── DragDrop ──────────────────────────────────────────────────────────────

    private void Canvas_DragEnter(object sender, DragEventArgs e) => UpdateDragFeedback(e, entering: true);
    private void Canvas_DragOver(object sender, DragEventArgs e)  => UpdateDragFeedback(e, entering: true);

    private void Canvas_DragLeave(object sender, DragEventArgs e)
    {
        TheCanvas.BorderBrush = DragIdle;
        TheCanvas.BorderThickness = new Thickness(0);
    }

    private void Canvas_Drop(object sender, DragEventArgs e)
    {
        TheCanvas.BorderBrush = DragIdle;
        TheCanvas.BorderThickness = new Thickness(0);
        TheCanvas.ResetInteractionState();

        if (ViewModel is null) return;

        var dropColumn = (int?)GridGeometryHelper.SnapToGrid(e.GetPosition(TheCanvas).X);
        var dropRow = (int?)GridGeometryHelper.SnapToGrid(e.GetPosition(TheCanvas).Y);

        // Visual Library tile → empty widget of that type.
        if (e.Data.GetDataPresent(VisualLibraryView.VisualDragFormat))
        {
            if (e.Data.GetData(VisualLibraryView.VisualDragFormat) is VisualTypeEntry entry)
                ViewModel.AddWidgetFromDrop(entry.VisualType, dropColumn: dropColumn, dropRow: dropRow);
            return;
        }

        // Explorer field → rule engine picks the best visual type automatically.
        if (e.Data.GetDataPresent(ExplorerPanelView.FieldDragFormat))
        {
            if (e.Data.GetData(ExplorerPanelView.FieldDragFormat) is FieldDescriptor field)
            {
                var dataSourceId = e.Data.GetData(ExplorerPanelView.DataSourceIdDragFormat) is Guid id
                    ? id
                    : Guid.Empty;
                var resolvedId = ViewModel.ResolveDataSourceId?.Invoke(dataSourceId) ?? dataSourceId;
                ViewModel.AddWidgetFromFieldDrop(field, resolvedId, dropColumn: dropColumn, dropRow: dropRow);
            }
        }
    }

    private void UpdateDragFeedback(DragEventArgs e, bool entering)
    {
        var acceptable =
            e.Data.GetDataPresent(VisualLibraryView.VisualDragFormat) ||
            e.Data.GetDataPresent(ExplorerPanelView.FieldDragFormat);

        if (acceptable && entering)
        {
            TheCanvas.BorderBrush = DragHighlight;
            TheCanvas.BorderThickness = new Thickness(2);
        }
        else
        {
            TheCanvas.BorderBrush = DragIdle;
            TheCanvas.BorderThickness = new Thickness(0);
        }

        e.Effects = acceptable ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    // ── Story Mode (v2.0 Feature 3) ──────────────────────────────────────────

    /// <summary>
    /// Launches full-screen Story Mode by re-parenting the live
    /// <see cref="Controls.DashboardCanvas"/> into a <see cref="PresentationWindow"/> —
    /// no second canvas is created, so the presentation always reflects real,
    /// current widget data. Restored back to this view when the window closes.
    /// </summary>
    private void PresentButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not { ActiveDashboard: { } dashboard } vm) return;

        var hostGrid = (Grid)TheCanvas.Parent;
        hostGrid.Children.Remove(TheCanvas);

        var window = new PresentationWindow(vm.Presentation, vm.GlobalFilterManager, TheCanvas, dashboard.Id);
        window.Closed += (_, _) => hostGrid.Children.Add(TheCanvas);
        window.ShowDialog();
    }
}
