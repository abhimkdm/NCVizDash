using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NCVizDash.Models;
using NCVizDash.TaskPane.Converters;
using NCVizDash.TaskPane.Geometry;
using NCVizDash.TaskPane.ViewModels;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace NCVizDash.TaskPane.Controls;

/// <summary>
/// Custom WPF panel that is the visual heart of the dashboard designer.
/// <para>
/// Responsibilities:
/// <list type="bullet">
///   <item>Absolute pixel positioning of child <see cref="WidgetCard"/> elements via <see cref="ArrangeOverride"/>.</item>
///   <item>Mouse-driven <b>move</b> (click-drag anywhere on a card header).</item>
///   <item>Mouse-driven <b>resize</b> (drag the 8px handle at the bottom-right corner).</item>
///   <item>Single-click <b>selection</b> (Ctrl = additive).</item>
///   <item>Click-on-background <b>deselect-all</b>.</item>
///   <item><b>Rubber-band multi-select</b> (drag on background).</item>
///   <item>Rendering <b>alignment guide</b> lines during drag/resize.</item>
/// </list>
/// </para>
/// All grid math (snap, clamp, guide detection) is delegated to the stateless
/// <see cref="GridGeometryHelper"/> so it stays testable without a UI host.
/// </summary>
public sealed class DashboardCanvas : System.Windows.Controls.Panel
{
    // ── Dependency properties ────────────────────────────────────────────────

    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(CanvasPanelViewModel),
            typeof(DashboardCanvas), new PropertyMetadata(null, OnViewModelChanged));

    /// <summary>Identifies the <see cref="ShowGrid"/> dependency property.</summary>
    public static readonly DependencyProperty ShowGridProperty =
        DependencyProperty.Register(nameof(ShowGrid), typeof(bool),
            typeof(DashboardCanvas), new PropertyMetadata(true, (d, _) => ((DashboardCanvas)d).InvalidateVisual()));

    /// <summary>Identifies the <see cref="SnapToGrid"/> dependency property.</summary>
    public static readonly DependencyProperty SnapToGridProperty =
        DependencyProperty.Register(nameof(SnapToGrid), typeof(bool),
            typeof(DashboardCanvas), new PropertyMetadata(true));

    /// <summary>
    /// Border brush drawn around the canvas edge — used by <see cref="Views.CanvasPanelView"/>
    /// to highlight the canvas while a drag-and-drop operation is over it. <see cref="Panel"/>
    /// has no built-in border (unlike <see cref="System.Windows.Controls.Control"/>/
    /// <see cref="System.Windows.Controls.Border"/>), so it's added here as a plain
    /// dependency property and drawn manually in <see cref="OnRender"/>.
    /// </summary>
    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(nameof(BorderBrush), typeof(Brush),
            typeof(DashboardCanvas), new PropertyMetadata(Brushes.Transparent, (d, _) => ((DashboardCanvas)d).InvalidateVisual()));

    /// <summary>Thickness of the highlight border drawn per <see cref="BorderBrush"/>.</summary>
    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(nameof(BorderThickness), typeof(Thickness),
            typeof(DashboardCanvas), new PropertyMetadata(new Thickness(0), (d, _) => ((DashboardCanvas)d).InvalidateVisual()));

    /// <summary>Border brush drawn around the canvas edge (see <see cref="BorderBrushProperty"/>).</summary>
    public Brush BorderBrush
    {
        get => (Brush)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>Thickness of the highlight border drawn around the canvas edge.</summary>
    public Thickness BorderThickness
    {
        get => (Thickness)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>The canvas ViewModel. Provides widget list, selection, and operation entry-points.</summary>
    public CanvasPanelViewModel? ViewModel
    {
        get => (CanvasPanelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Whether the background dot-grid is painted.</summary>
    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>Whether move/resize operations snap to the nearest grid unit.</summary>
    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    // ── Brushes / pens (allocated once) ─────────────────────────────────────

    private static readonly Pen GridPen =
        new(new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), 0.5) { DashStyle = DashStyles.Dot };

    private static readonly Pen SelectionRubberBandPen =
        new(new SolidColorBrush(Color.FromArgb(180, 103, 58, 183)), 1.5) { DashStyle = DashStyles.Dash };

    private static readonly Brush SelectionRubberBandFill =
        new SolidColorBrush(Color.FromArgb(25, 103, 58, 183));

    private static readonly Pen GuideVerticalPen =
        new(new SolidColorBrush(Color.FromArgb(200, 103, 58, 183)), 1.0) { DashStyle = DashStyles.Dash };

    private static readonly Pen GuideHorizontalPen =
        new(new SolidColorBrush(Color.FromArgb(200, 0, 150, 136)), 1.0) { DashStyle = DashStyles.Dash };

    private const double ResizeHandleSize = 10d;

    /// <summary>Minimum canvas size — keep in sync with <c>CanvasPanelView.xaml</c>.</summary>
    private const double MinCanvasWidth = 960d;

    /// <summary>Minimum canvas size — keep in sync with <c>CanvasPanelView.xaml</c>.</summary>
    private const double MinCanvasHeight = 640d;

    // ── Drag state ───────────────────────────────────────────────────────────

    private enum DragMode { None, Move, Resize, RubberBand }

    private DragMode _dragMode = DragMode.None;
    private DashboardWidget? _dragWidget;
    private Point _dragStartMouse;
    private WidgetLayout? _dragStartLayout;
    private Rect _rubberBandRect;

    // ── Panel layout ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var contentSize = ComputeContentSize();

        var childConstraint = new Size(
            double.IsPositiveInfinity(availableSize.Width) ? contentSize.Width : availableSize.Width,
            double.IsPositiveInfinity(availableSize.Height) ? contentSize.Height : availableSize.Height);

        foreach (UIElement child in Children)
            child.Measure(childConstraint);

        var width = contentSize.Width;
        var height = contentSize.Height;

        if (!double.IsPositiveInfinity(availableSize.Width))
            width = Math.Min(width, availableSize.Width);
        if (!double.IsPositiveInfinity(availableSize.Height))
            height = Math.Min(height, availableSize.Height);

        return new Size(width, height);
    }

    private Size ComputeContentSize()
    {
        var width = MinCanvasWidth;
        var height = MinCanvasHeight;

        if (ViewModel is null)
            return new Size(width, height);

        foreach (var widget in ViewModel.Widgets)
        {
            width = Math.Max(width, GridGeometryHelper.ToPixels(widget.Layout.Column + widget.Layout.ColumnSpan));
            height = Math.Max(height, GridGeometryHelper.ToPixels(widget.Layout.Row + widget.Layout.RowSpan));
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in Children)
        {
            if (child is WidgetCard card && card.Widget is { } widget)
            {
                var x = GridGeometryHelper.ToPixels(widget.Layout.Column);
                var y = GridGeometryHelper.ToPixels(widget.Layout.Row);
                var w = GridGeometryHelper.ToPixels(widget.Layout.ColumnSpan);
                var h = GridGeometryHelper.ToPixels(widget.Layout.RowSpan);

                card.Arrange(new Rect(x, y, w, h));
            }
        }

        return finalSize;
    }

    // ── Custom rendering (grid + guides + rubber-band) ───────────────────────

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var bounds = new Rect(RenderSize);
        dc.DrawRectangle(Brushes.Transparent, null, bounds); // ensure hit-testing

        if (ShowGrid)
            DrawGrid(dc, bounds);

        DrawAlignmentGuides(dc, bounds);

        if (_dragMode == DragMode.RubberBand)
            DrawRubberBand(dc);

        DrawHighlightBorder(dc, bounds);
    }

    private void DrawHighlightBorder(DrawingContext dc, Rect bounds)
    {
        var thickness = BorderThickness;
        var maxEdge = Math.Max(Math.Max(thickness.Left, thickness.Top), Math.Max(thickness.Right, thickness.Bottom));
        if (maxEdge <= 0) return;

        var pen = new Pen(BorderBrush, maxEdge);
        var inset = maxEdge / 2;
        dc.DrawRectangle(null, pen,
            new Rect(bounds.X + inset, bounds.Y + inset,
                     Math.Max(0, bounds.Width - maxEdge), Math.Max(0, bounds.Height - maxEdge)));
    }

    private void DrawGrid(DrawingContext dc, Rect bounds)
    {
        var unit = GridGeometryHelper.UnitSize;

        for (var x = 0d; x < bounds.Width; x += unit)
            dc.DrawLine(GridPen, new Point(x, 0), new Point(x, bounds.Height));

        for (var y = 0d; y < bounds.Height; y += unit)
            dc.DrawLine(GridPen, new Point(0, y), new Point(bounds.Width, y));
    }

    private void DrawAlignmentGuides(DrawingContext dc, Rect bounds)
    {
        if (ViewModel is null) return;

        foreach (var guide in ViewModel.ActiveGuides)
        {
            var px = GridGeometryHelper.ToPixels(guide.GridPosition);

            if (guide.Orientation == GuideOrientation.Vertical)
                dc.DrawLine(GuideVerticalPen, new Point(px, 0), new Point(px, bounds.Height));
            else
                dc.DrawLine(GuideHorizontalPen, new Point(0, px), new Point(bounds.Width, px));
        }
    }

    private void DrawRubberBand(DrawingContext dc)
    {
        dc.DrawRectangle(SelectionRubberBandFill, SelectionRubberBandPen, _rubberBandRect);
    }

    // ── Mouse handling ───────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        var pos = e.GetPosition(this);
        var hit = HitTestWidget(pos);

        if (hit is null)
        {
            // Click on background: begin rubber-band or clear selection
            ViewModel?.ClearSelection();
            _dragMode = DragMode.RubberBand;
            _dragStartMouse = pos;
            _rubberBandRect = new Rect(pos, new Size(0, 0));
            CaptureMouse();
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        BeginWidgetInteraction(hit, pos, resize: IsResizeHandleHit(hit, pos), e);
    }

    /// <summary>
    /// Starts a move or resize gesture for a widget (from the canvas or a card title bar).
    /// </summary>
    internal void BeginWidgetInteraction(
        DashboardWidget widget,
        Point canvasPos,
        bool resize,
        MouseButtonEventArgs e)
    {
        ResetInteractionState();

        var isAdditive = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        ViewModel?.SelectWidget(widget, isAdditive);

        _dragWidget = widget;
        _dragStartMouse = canvasPos;
        _dragStartLayout = new WidgetLayout
        {
            Column = widget.Layout.Column,
            Row = widget.Layout.Row,
            ColumnSpan = widget.Layout.ColumnSpan,
            RowSpan = widget.Layout.RowSpan
        };

        _dragMode = resize ? DragMode.Resize : DragMode.Move;

        if (ViewModel?.ActiveDashboard is { } dashboard)
            ViewModel.UndoRedo.RecordSnapshot(dashboard);

        CaptureMouse();
        e.Handled = true;
    }

    /// <summary>Clears any in-progress move/resize/rubber-band gesture.</summary>
    public void ResetInteractionState()
    {
        if (IsMouseCaptured)
            ReleaseMouseCapture();

        ClearDragState();
    }

    /// <inheritdoc/>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_dragMode != DragMode.None)
            ClearDragState();

        base.OnLostMouseCapture(e);
    }

    private void ClearDragState()
    {
        _dragMode = DragMode.None;
        _dragWidget = null;
        _dragStartLayout = null;
        ViewModel?.ClearGuides();
        InvalidateVisual();
    }

    private bool IsResizeHandleHit(DashboardWidget widget, Point canvasPos)
    {
        var cardBounds = GetWidgetPixelBounds(widget);
        var handleRect = new Rect(
            cardBounds.Right - ResizeHandleSize,
            cardBounds.Bottom - ResizeHandleSize,
            ResizeHandleSize, ResizeHandleSize);

        return handleRect.Contains(canvasPos);
    }

    /// <inheritdoc/>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var pos = e.GetPosition(this);

        // Update cursor based on hover (no drag active)
        if (_dragMode == DragMode.None)
        {
            UpdateCursor(pos);
            return;
        }

        if (_dragMode == DragMode.RubberBand)
        {
            _rubberBandRect = BuildRect(_dragStartMouse, pos);
            InvalidateVisual();
            return;
        }

        if (_dragWidget is null || _dragStartLayout is null) return;

        var deltaX = pos.X - _dragStartMouse.X;
        var deltaY = pos.Y - _dragStartMouse.Y;

        if (_dragMode == DragMode.Move)
        {
            var newCol = _dragStartLayout.Column + (SnapToGrid
                ? GridGeometryHelper.SnapToGrid(deltaX)
                : (int)(deltaX / GridGeometryHelper.UnitSize));
            var newRow = _dragStartLayout.Row + (SnapToGrid
                ? GridGeometryHelper.SnapToGrid(deltaY)
                : (int)(deltaY / GridGeometryHelper.UnitSize));

            ViewModel?.MoveWidget(_dragWidget, newCol, newRow);
        }
        else // Resize
        {
            var newColSpan = _dragStartLayout.ColumnSpan + (SnapToGrid
                ? GridGeometryHelper.SnapToGrid(deltaX)
                : (int)(deltaX / GridGeometryHelper.UnitSize));
            var newRowSpan = _dragStartLayout.RowSpan + (SnapToGrid
                ? GridGeometryHelper.SnapToGrid(deltaY)
                : (int)(deltaY / GridGeometryHelper.UnitSize));

            ViewModel?.ResizeWidget(_dragWidget, newColSpan, newRowSpan);
        }

        ViewModel?.UpdateGuides(_dragWidget);
        InvalidateArrange();
        InvalidateVisual();
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_dragMode == DragMode.RubberBand)
            CommitRubberBandSelection();

        _dragMode = DragMode.None;
        _dragWidget = null;
        _dragStartLayout = null;

        ViewModel?.ClearGuides();
        ReleaseMouseCapture();
        InvalidateMeasure();
        InvalidateVisual();
        e.Handled = true;
    }

    // ── Rubber-band selection ─────────────────────────────────────────────────

    private void CommitRubberBandSelection()
    {
        if (ViewModel is null) return;

        ViewModel.ClearSelection();

        foreach (var widget in ViewModel.Widgets)
        {
            var widgetBounds = GetWidgetPixelBounds(widget);
            if (_rubberBandRect.IntersectsWith(widgetBounds))
                ViewModel.SelectWidget(widget, additive: true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private DashboardWidget? HitTestWidget(Point pos)
    {
        if (ViewModel is null) return null;

        // Reverse-iterate so topmost (last-added) widget wins.
        foreach (var widget in ViewModel.Widgets.Reverse())
        {
            if (GetWidgetPixelBounds(widget).Contains(pos))
                return widget;
        }

        return null;
    }

    private static Rect GetWidgetPixelBounds(DashboardWidget widget) =>
        new(
            GridGeometryHelper.ToPixels(widget.Layout.Column),
            GridGeometryHelper.ToPixels(widget.Layout.Row),
            GridGeometryHelper.ToPixels(widget.Layout.ColumnSpan),
            GridGeometryHelper.ToPixels(widget.Layout.RowSpan));

    private void UpdateCursor(Point pos)
    {
        if (ViewModel is null) return;

        foreach (var widget in ViewModel.Widgets.Reverse())
        {
            var bounds = GetWidgetPixelBounds(widget);
            if (!bounds.Contains(pos)) continue;

            var handleRect = new Rect(
                bounds.Right - ResizeHandleSize,
                bounds.Bottom - ResizeHandleSize,
                ResizeHandleSize, ResizeHandleSize);

            Cursor = handleRect.Contains(pos) ? System.Windows.Input.Cursors.SizeNWSE : System.Windows.Input.Cursors.SizeAll;
            return;
        }

        Cursor = System.Windows.Input.Cursors.Arrow;
    }

    private static Rect BuildRect(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
            Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    // ── ViewModel wiring ──────────────────────────────────────────────────────

    /// <summary>
    /// Bridges widget field mappings → DuckDB query → chart engine → WebView2.
    /// Must be set (typically via DI in the host window) before widgets can render
    /// actual charts; without it, cards show their chrome but no chart content.
    /// </summary>
    public static readonly DependencyProperty RenderCoordinatorProperty =
        DependencyProperty.Register(nameof(RenderCoordinator), typeof(Services.WidgetRenderCoordinator),
            typeof(DashboardCanvas), new PropertyMetadata(null));

    /// <summary>Gets or sets the widget render coordinator for chart rendering.</summary>
    public Services.WidgetRenderCoordinator? RenderCoordinator
    {
        get => (Services.WidgetRenderCoordinator?)GetValue(RenderCoordinatorProperty);
        set => SetValue(RenderCoordinatorProperty, value);
    }

    /// <summary>Active theme name ("Light"/"Dark"), forwarded to the chart engine for palette selection.</summary>
    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(nameof(Theme), typeof(string),
            typeof(DashboardCanvas), new PropertyMetadata("Light", OnThemeChanged));

    /// <summary>Gets or sets the active chart theme name.</summary>
    public string Theme
    {
        get => (string)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    /// <summary>
    /// Coordinates cross-filter state (Phase 8). When set, clicking a data point in
    /// any widget whose <see cref="DashboardWidget.IsCrossFilterSource"/> is true
    /// applies a filter that every other <see cref="DashboardWidget.IsCrossFilterTarget"/>
    /// widget picks up on its next render.
    /// </summary>
    public static readonly DependencyProperty FilterManagerProperty =
        DependencyProperty.Register(nameof(FilterManager), typeof(Core.Abstractions.IFilterManager),
            typeof(DashboardCanvas), new PropertyMetadata(null, OnFilterManagerChanged));

    /// <summary>Gets or sets the cross-filter manager for linked widget filtering.</summary>
    public Core.Abstractions.IFilterManager? FilterManager
    {
        get => (Core.Abstractions.IFilterManager?)GetValue(FilterManagerProperty);
        set => SetValue(FilterManagerProperty, value);
    }

    /// <summary>
    /// Coordinates dashboard-wide filter state (Phase 9). When set, any filter added,
    /// removed, or toggled via the global filter bar triggers a re-render of every
    /// widget on the canvas, unconditionally (unlike cross-filters, global filters
    /// have no per-widget opt-out or self-exclusion).
    /// </summary>
    public static readonly DependencyProperty GlobalFilterManagerProperty =
        DependencyProperty.Register(nameof(GlobalFilterManager), typeof(Core.Abstractions.IGlobalFilterManager),
            typeof(DashboardCanvas), new PropertyMetadata(null, OnGlobalFilterManagerChanged));

    /// <summary>Gets or sets the global filter manager for dashboard-wide filters.</summary>
    public Core.Abstractions.IGlobalFilterManager? GlobalFilterManager
    {
        get => (Core.Abstractions.IGlobalFilterManager?)GetValue(GlobalFilterManagerProperty);
        set => SetValue(GlobalFilterManagerProperty, value);
    }

    private readonly Dictionary<Guid, WidgetCard> _cardsByWidgetId = new();

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (DashboardCanvas)d;

        if (e.OldValue is CanvasPanelViewModel oldVm)
        {
            oldVm.Widgets.CollectionChanged -= canvas.OnWidgetsChanged;
            oldVm.DataSourceRefreshed -= canvas.OnDataSourceRefreshed;
            oldVm.RenderAllWidgetsRequested -= canvas.OnRenderAllWidgetsRequested;
        }

        if (e.NewValue is CanvasPanelViewModel newVm)
        {
            newVm.Widgets.CollectionChanged += canvas.OnWidgetsChanged;
            newVm.DataSourceRefreshed += canvas.OnDataSourceRefreshed;
            newVm.RenderAllWidgetsRequested += canvas.OnRenderAllWidgetsRequested;
            canvas.ScheduleRebuildChildren();
        }
    }

    private void OnRenderAllWidgetsRequested(object? sender, EventArgs e)
    {
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            _ = RenderAllWidgetsAsync();
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Live Refresh (v2.0 Feature 4): re-renders only the widget cards bound to
    /// the data source that was just selectively reloaded, leaving every other
    /// widget's current render untouched — no full-canvas rebuild, no layout or
    /// filter disruption.
    /// </summary>
    private void OnDataSourceRefreshed(object? sender, Guid dataSourceId)
    {
        foreach (var card in _cardsByWidgetId.Values.Where(c => c.Widget.DataSourceId == dataSourceId))
            _ = RenderWidgetAsync(card);
    }

    private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (DashboardCanvas)d;
        foreach (var card in canvas._cardsByWidgetId.Values)
            _ = canvas.RenderWidgetAsync(card);
    }

    private static void OnFilterManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (DashboardCanvas)d;

        if (e.OldValue is Core.Abstractions.IFilterManager oldFm)
            oldFm.FiltersChanged -= canvas.OnFiltersChanged;

        if (e.NewValue is Core.Abstractions.IFilterManager newFm)
            newFm.FiltersChanged += canvas.OnFiltersChanged;
    }

    private static void OnGlobalFilterManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (DashboardCanvas)d;

        if (e.OldValue is Core.Abstractions.IGlobalFilterManager oldGfm)
            oldGfm.FiltersChanged -= canvas.OnFiltersChanged;

        if (e.NewValue is Core.Abstractions.IGlobalFilterManager newGfm)
            newGfm.FiltersChanged += canvas.OnFiltersChanged;
    }

    /// <summary>
    /// Re-renders every widget on the dashboard in response to a cross-filter change.
    /// Deliberately re-renders ALL widgets (not just targets) so a widget that just
    /// toggled <see cref="DashboardWidget.IsCrossFilterTarget"/> off still shows its
    /// unfiltered state immediately rather than on its next unrelated refresh.
    /// </summary>
    private void OnFiltersChanged(object? sender, EventArgs e)
    {
        foreach (var card in _cardsByWidgetId.Values)
            _ = RenderWidgetAsync(card);
    }

    /// <summary>
    /// Handles a data-point click forwarded from a widget's <see cref="ChartHost"/>.
    /// Applies (or, per <see cref="Core.Abstractions.IFilterManager.ApplyFilter"/>'s
    /// toggle semantics, clears) a cross-filter on the widget's first dimension field
    /// using the clicked category/series name as the filter value.
    /// </summary>
    private void OnCardChartClicked(DashboardWidget widget, ChartClickEventArgs args)
    {
        if (FilterManager is null) return;
        if (!widget.IsCrossFilterSource) return;

        var field = widget.DimensionFields.FirstOrDefault();
        if (string.IsNullOrEmpty(field)) return;

        var value = args.CategoryName ?? args.SeriesName;
        if (string.IsNullOrEmpty(value)) return;

        FilterManager.ApplyFilter(widget.Id, field, [value]);
    }

    private void OnWidgetsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        ScheduleRebuildChildren();
    }

    private bool _rebuildScheduled;

    private void ScheduleRebuildChildren()
    {
        if (_rebuildScheduled) return;
        _rebuildScheduled = true;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _rebuildScheduled = false;
            RebuildChildren();
            InvalidateMeasure();
            InvalidateArrange();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Synchronises the panel's <see cref="System.Windows.Controls.Panel.Children"/> with
    /// <see cref="CanvasPanelViewModel.Widgets"/>. Disposes the <see cref="ChartHost"/>
    /// of any removed card before dropping the reference, then creates and renders
    /// a fresh <see cref="WidgetCard"/> for every current widget.
    /// </summary>
    private void RebuildChildren()
    {
        ResetInteractionState();

        foreach (var card in _cardsByWidgetId.Values)
            card.Cleanup();
        _cardsByWidgetId.Clear();
        Children.Clear();

        if (ViewModel is null) return;

        foreach (var widget in ViewModel.Widgets)
        {
            var card = new WidgetCard(widget);
            card.ChartClicked += (_, args) => OnCardChartClicked(widget, args);
            _cardsByWidgetId[widget.Id] = card;
            Children.Add(card);
            _ = RenderWidgetAsync(card);
        }
    }

    /// <summary>
    /// Re-queries and re-renders a single widget's chart — call after changing a
    /// widget's field mappings, data source, or visual type outside of a full
    /// canvas rebuild (e.g. from a field-mapping editor added in a later phase).
    /// </summary>
    public void RefreshWidget(DashboardWidget widget)
    {
        if (_cardsByWidgetId.TryGetValue(widget.Id, out var card))
            _ = RenderWidgetAsync(card);
    }

    /// <summary>
    /// Re-renders every widget on the canvas in parallel, throttled to avoid
    /// saturating the WebView2/DuckDB pipeline when a dashboard has many widgets
    /// (Phase 16). A semaphore caps concurrent in-flight renders; each widget still
    /// renders independently so a slow/failing one doesn't block the rest.
    /// </summary>
    public async Task RenderAllWidgetsAsync(int maxConcurrency = 4)
    {
        using var throttle = new SemaphoreSlim(maxConcurrency);

        var tasks = _cardsByWidgetId.Values.Select(async card =>
        {
            await throttle.WaitAsync();
            try { await RenderWidgetAsync(card); }
            finally { throttle.Release(); }
        });

        await Task.WhenAll(tasks);
    }

    private async Task RenderWidgetAsync(WidgetCard card)
    {
        if (RenderCoordinator is null) return;

        try
        {
            var payload = await RenderCoordinator.RenderWidgetAsync(card.Widget, Theme);
            await card.RenderAsync(payload);
        }
        catch
        {
            // RenderWidgetAsync never throws (returns an error payload instead);
            // this catch only guards against the WebView2 call itself faulting
            // during teardown (e.g. card removed mid-render).
        }
    }
}
