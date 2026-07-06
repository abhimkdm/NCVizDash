using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;
using NCVizDash.TaskPane.Geometry;
using System.Collections.ObjectModel;

namespace NCVizDash.TaskPane.ViewModels;

// ─────────────────────────────────────────────────────────────────────────────
//  Explorer Panel ViewModel
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Drives the left panel: workbook data source tree and field list.
/// Loads data sources from Excel and ingests them into the analytics engine.
/// </summary>
public sealed partial class ExplorerPanelViewModel : ObservableObject
{
    private readonly ILogger<ExplorerPanelViewModel> _logger;
    private readonly IExcelDataReader _excelDataReader;
    private readonly IAnalyticsEngine _analyticsEngine;

    private const int PreviewRowLimit = 10;

    [ObservableProperty]
    private ObservableCollection<DataSourceDescriptor> _dataSources = [];

    [ObservableProperty]
    private DataSourceDescriptor? _selectedDataSource;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private ObservableCollection<IReadOnlyDictionary<string, object?>> _previewRows = [];

    [ObservableProperty]
    private DataSourceDescriptor? _previewSource;

    [ObservableProperty]
    private bool _isPreviewLoading;

    /// <summary>
    /// The set of data sources currently visible in the explorer tree, after
    /// applying <see cref="SearchText"/>. The view binds to this rather than
    /// <see cref="DataSources"/> directly.
    /// </summary>
    public ObservableCollection<DataSourceDescriptor> FilteredDataSources { get; } = [];

    /// <summary>Invoked when the user requests a one-click dashboard for a data source.</summary>
    public Action<DataSourceDescriptor>? GenerateDashboardForSource { get; set; }

    /// <summary>Initialises the explorer with the Excel reader and analytics engine.</summary>
    public ExplorerPanelViewModel(
        ILogger<ExplorerPanelViewModel> logger,
        IExcelDataReader excelDataReader,
        IAnalyticsEngine analyticsEngine)
    {
        _logger = logger;
        _excelDataReader = excelDataReader;
        _analyticsEngine = analyticsEngine;

        DataSources.CollectionChanged += (_, _) => RefreshFilteredDataSources();
    }

    /// <summary>
    /// Discovers all data sources in the active workbook, reads their rows,
    /// classifies fields, and ingests each into the analytics engine.
    /// </summary>
    public async Task LoadDataSourcesAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        _logger.LogInformation("Beginning data source discovery and load.");

        try
        {
            var discovered = await _excelDataReader.GetDataSourcesAsync(ct);

            DataSources.Clear();

            foreach (var source in discovered)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var rows = await _excelDataReader.ReadRowsAsync(source.Id, ct);
                    await _analyticsEngine.LoadDataSourceAsync(source, rows, ct);
                    DataSources.Add(source);

                    _logger.LogInformation(
                        "Loaded '{Source}' ({RowCount} rows, {FieldCount} fields).",
                        source.Name, rows.Count, source.Fields.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Skipped data source '{Source}' due to a load error.", source.Name);
                }
            }

            _logger.LogInformation("Data source load complete: {Count} source(s) ready.", DataSources.Count);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Builds a one-click dashboard from the selected data source.</summary>
    public void GenerateDashboard(DataSourceDescriptor? source)
    {
        var target = source ?? SelectedDataSource ?? DataSources.FirstOrDefault();
        if (target is null)
        {
            _logger.LogWarning("Generate dashboard ignored: no data source.");
            return;
        }

        if (GenerateDashboardForSource is null)
        {
            _logger.LogWarning("Generate dashboard ignored: shell handler not wired.");
            return;
        }

        _logger.LogInformation("Generating dashboard for '{Source}'.", target.Name);
        GenerateDashboardForSource(target);
    }

    /// <summary>
    /// Re-reads only the data source(s) whose <see cref="DataSourceDescriptor.SheetName"/>
    /// matches, reloading them into the analytics engine in place — the
    /// <see cref="DataSourceDescriptor"/> object identity and <see cref="DataSourceDescriptor.Id"/>
    /// are preserved, so widgets already bound to it via <c>DataSourceId</c> stay valid with
    /// no dashboard/layout/filter changes required. This is the v2.0 "Live Refresh"
    /// path — a targeted alternative to <see cref="LoadDataSourcesAsync"/>'s full
    /// workbook rescan, used when a specific sheet changes rather than on an
    /// explicit user-triggered "Refresh All".
    /// </summary>
    /// <returns>The IDs of every data source that was actually refreshed, for the caller to selectively re-render affected widgets.</returns>
    public async Task<IReadOnlyList<Guid>> RefreshSheetAsync(string sheetName, CancellationToken ct = default)
    {
        var affected = DataSources.Where(ds => ds.SheetName == sheetName).ToList();
        if (affected.Count == 0) return [];

        var refreshedIds = new List<Guid>();

        foreach (var source in affected)
        {
            try
            {
                var rows = await _excelDataReader.ReadRowsAsync(source.Id, ct);
                await _analyticsEngine.LoadDataSourceAsync(source, rows, ct);
                refreshedIds.Add(source.Id);

                _logger.LogInformation("Live refresh: '{Source}' reloaded ({RowCount} rows).", source.Name, rows.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live refresh failed for '{Source}'; leaving previous data in place.", source.Name);
            }
        }

        return refreshedIds;
    }

    /// <summary>Selects a data source in the explorer tree.</summary>
    [RelayCommand]
    public void SelectDataSource(DataSourceDescriptor? source)
    {
        SelectedDataSource = source;
        _logger.LogDebug("Data source selected: {Name}", source?.Name ?? "(none)");
    }

    /// <summary>
    /// Loads a small sample of rows (capped at <see cref="PreviewRowLimit"/>) for the
    /// given data source, for display in the explorer's hover preview popup.
    /// </summary>
    [RelayCommand]
    public async Task LoadPreviewAsync(DataSourceDescriptor? source)
    {
        if (source is null)
            return;

        PreviewRows ??= [];

        if (PreviewSource?.Id == source.Id && PreviewRows.Count > 0)
            return; // already cached for this source

        IsPreviewLoading = true;
        PreviewSource = source;

        try
        {
            var rows = await _excelDataReader.ReadRowsAsync(source.Id);
            if (rows is null)
                return;

            PreviewRows.Clear();
            foreach (var row in rows.Take(PreviewRowLimit))
                PreviewRows.Add(row);

            _logger.LogDebug("Loaded preview for '{Source}': {Count} row(s).", source.Name, PreviewRows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load preview for '{Source}'.", source.Name);
            PreviewRows.Clear();
        }
        finally
        {
            IsPreviewLoading = false;
        }
    }

    /// <summary>Clears the active preview (called when the pointer leaves a data source row).</summary>
    public void ClearPreview()
    {
        PreviewSource = null;
        PreviewRows ??= [];
        PreviewRows.Clear();
    }

    /// <summary>
    /// Returns the data sources filtered by <see cref="SearchText"/>, matching
    /// either the source name or any field display name (case-insensitive).
    /// </summary>
    public IEnumerable<DataSourceDescriptor> GetFilteredDataSources()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return DataSources;

        var term = SearchText.Trim();

        return DataSources.Where(ds =>
            ds.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
            ds.Fields.Any(f => f.DisplayName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
    }

    // ── Search reactivity ─────────────────────────────────────────────────────

    /// <summary>
    /// Auto-generated partial hook (CommunityToolkit.Mvvm) invoked whenever
    /// <see cref="SearchText"/> changes. Re-applies the filter live as the user types.
    /// </summary>
    partial void OnSearchTextChanged(string value) => RefreshFilteredDataSources();

    private void RefreshFilteredDataSources()
    {
        var matches = GetFilteredDataSources().ToList();

        FilteredDataSources.Clear();
        foreach (var match in matches)
            FilteredDataSources.Add(match);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Canvas Panel ViewModel
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Drives the centre panel: the dashboard canvas.
/// Owns widget selection, move/resize/duplicate operations, snap-to-grid clamping,
/// and live alignment-guide computation during drag/resize gestures.
/// </summary>
public sealed partial class CanvasPanelViewModel : ObservableObject
{
    private readonly ILogger<CanvasPanelViewModel> _logger;
    private readonly IVisualizationRuleEngine _ruleEngine;

    /// <summary>
    /// Bridges widget field mappings to rendered chart payloads. Exposed so
    /// <c>CanvasPanelView.xaml</c> can bind it straight onto <c>DashboardCanvas.RenderCoordinator</c>.
    /// </summary>
    public Services.WidgetRenderCoordinator RenderCoordinator { get; }

    /// <summary>
    /// Cross-filter coordinator (Phase 8). Exposed so <c>CanvasPanelView.xaml</c> can
    /// bind it onto <c>DashboardCanvas.FilterManager</c>.
    /// </summary>
    public IFilterManager FilterManager { get; }

    /// <summary>
    /// Dashboard-wide filter coordinator (Phase 9). Exposed so <c>CanvasPanelView.xaml</c>
    /// can bind it onto <c>DashboardCanvas.GlobalFilterManager</c>.
    /// </summary>
    public IGlobalFilterManager GlobalFilterManager { get; }

    /// <summary>The dynamic global filter bar ViewModel, hosted above the canvas.</summary>
    public GlobalFilterBarViewModel GlobalFilterBar { get; }

    /// <summary>Undo/redo history for the active dashboard's widget layout (Phase 12).</summary>
    public Services.UndoRedoManager UndoRedo { get; }

    /// <summary>Drives full-screen Story Mode presentations (v2.0 Feature 3).</summary>
    public NCVizDash.TaskPane.Presentation.PresentationController Presentation { get; }

    /// <summary>
    /// Raised when a specific data source has been selectively reloaded (v2.0 Live
    /// Refresh) — <see cref="Controls.DashboardCanvas"/> subscribes to this and
    /// re-renders only the widgets bound to that data source, leaving every other
    /// widget's current render untouched (no full-dashboard refresh).
    /// </summary>
    public event EventHandler<Guid>? DataSourceRefreshed;

    /// <summary>Notifies subscribers that the given data source was reloaded — call after a selective (per-sheet) refresh.</summary>
    public void NotifyDataSourceRefreshed(Guid dataSourceId) => DataSourceRefreshed?.Invoke(this, dataSourceId);

    [ObservableProperty]
    private string _activeTheme = "Light";

    [ObservableProperty]
    private int _activeFilterCount;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private Dashboard? _activeDashboard;

    [ObservableProperty]
    private ObservableCollection<DashboardWidget> _widgets = [];

    [ObservableProperty]
    private DashboardWidget? _selectedWidget;

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _snapToGrid = true;

    /// <summary>All widgets currently selected on the canvas (supports Ctrl+click multi-select).</summary>
    public ObservableCollection<DashboardWidget> SelectedWidgets { get; } = [];

    /// <summary>
    /// Alignment guides currently active during a drag/resize gesture, bound by the
    /// view to render dashed guide lines. Empty when nothing is being dragged.
    /// </summary>
    public ObservableCollection<AlignmentGuide> ActiveGuides { get; } = [];

    /// <summary>Initialises the canvas.</summary>
    public CanvasPanelViewModel(
        ILogger<CanvasPanelViewModel> logger,
        IVisualizationRuleEngine ruleEngine,
        Services.WidgetRenderCoordinator renderCoordinator,
        IFilterManager filterManager,
        IGlobalFilterManager globalFilterManager,
        GlobalFilterBarViewModel globalFilterBar,
        Services.UndoRedoManager undoRedo,
        NCVizDash.TaskPane.Presentation.PresentationController presentation)
    {
        _logger = logger;
        _ruleEngine = ruleEngine;
        RenderCoordinator = renderCoordinator;
        FilterManager = filterManager;
        GlobalFilterManager = globalFilterManager;
        GlobalFilterBar = globalFilterBar;
        UndoRedo = undoRedo;
        Presentation = presentation;

        FilterManager.FiltersChanged += (_, _) => ActiveFilterCount = FilterManager.ActiveFilterCount;
        UndoRedo.StateChanged += (_, _) => { CanUndo = UndoRedo.CanUndo; CanRedo = UndoRedo.CanRedo; };
    }

    /// <summary>Loads a dashboard onto the canvas.</summary>
    [RelayCommand]
    public void OpenDashboard(Dashboard dashboard)
    {
        ClearSelection();
        ActiveDashboard = dashboard;
        ReplaceWidgets(dashboard.Widgets);
        GlobalFilterManager.SetDashboard(dashboard);
        _logger.LogInformation("Dashboard '{Name}' loaded onto canvas.", dashboard.Name);
    }

    private void ReplaceWidgets(IEnumerable<DashboardWidget> widgets)
    {
        Widgets.Clear();
        foreach (var widget in widgets)
            Widgets.Add(widget);
    }

    /// <summary>Adds a new widget to the canvas.</summary>
    [RelayCommand]
    public void AddWidget(DashboardWidget widget)
    {
        if (ActiveDashboard?.IsReadOnly == true) return;
        if (ActiveDashboard is not null) UndoRedo.RecordSnapshot(ActiveDashboard);
        Widgets.Add(widget);
        ActiveDashboard?.Widgets.Add(widget);
        SelectWidget(widget, additive: false);
        _logger.LogInformation("Widget '{Title}' added to canvas.", widget.Title);
    }

    /// <summary>Removes every currently-selected widget (falls back to <see cref="SelectedWidget"/> if the multi-select set is empty).</summary>
    [RelayCommand]
    public void DeleteSelectedWidget()
    {
        var toRemove = SelectedWidgets.Count > 0
            ? SelectedWidgets.ToList()
            : SelectedWidget is not null ? [SelectedWidget] : new List<DashboardWidget>();

        if (toRemove.Count == 0) return;
        if (ActiveDashboard?.IsReadOnly == true) return;

        if (ActiveDashboard is not null) UndoRedo.RecordSnapshot(ActiveDashboard);

        foreach (var widget in toRemove)
        {
            Widgets.Remove(widget);
            ActiveDashboard?.Widgets.Remove(widget);
            _logger.LogInformation("Widget '{Title}' removed from canvas.", widget.Title);
        }

        ClearSelection();
    }

    /// <summary>
    /// Creates a new widget seeded by a single field dropped from the explorer.
    /// The visual type is chosen by the rule engine; the caller may override it
    /// by passing an explicit <paramref name="overrideVisual"/>.
    /// </summary>
    public DashboardWidget AddWidgetFromFieldDrop(
        FieldDescriptor droppedField,
        Guid dataSourceId,
        VisualType? overrideVisual = null)
    {
        var fields = new[] { droppedField };
        var (recommendedVisual, _, explanation) = _ruleEngine.RecommendWithExplanation(fields);
        var visual = overrideVisual ?? recommendedVisual;

        _logger.LogInformation(
            "Field drop: '{Field}' → {Visual} (rule: {Explanation}).",
            droppedField.DisplayName, visual, explanation);

        return AddWidgetFromDrop(visual, droppedField, dataSourceId);
    }

    /// <summary>
    /// Creates a new widget of the given visual type, optionally seeded with a
    /// field from the explorer, and places it at a default grid position.
    /// </summary>
    public DashboardWidget AddWidgetFromDrop(VisualType visualType, FieldDescriptor? seedField = null, Guid? dataSourceId = null)
    {
        if (ActiveDashboard is null)
        {
            ActiveDashboard = new Dashboard { Name = "Untitled Dashboard" };
            GlobalFilterManager.SetDashboard(ActiveDashboard);
        }

        var widget = new DashboardWidget
        {
            Title = seedField is not null
                ? $"{seedField.DisplayName} ({visualType})"
                : visualType.ToString(),
            VisualType = visualType,
            DataSourceId = dataSourceId ?? Guid.Empty,
            Layout = new WidgetLayout
            {
                Column = NextDropColumn(),
                Row = NextDropRow(),
                ColumnSpan = DefaultColumnSpan(visualType),
                RowSpan = DefaultRowSpan(visualType)
            }
        };

        if (seedField is not null)
        {
            if (seedField.FieldType == FieldType.Measure)
                widget.MeasureFields.Add(seedField.Name);
            else if (seedField.FieldType == FieldType.Dimension)
                widget.DimensionFields.Add(seedField.Name);
            else if (seedField.FieldType == FieldType.Time)
                widget.DimensionFields.Add(seedField.Name);
        }

        AddWidget(widget);
        return widget;
    }

    /// <summary>
    /// Default column span per visual type. Compact visuals (KPI, Gauge) get
    /// a narrower default; wide visuals (Table, Area) get more horizontal room.
    /// </summary>
    private static int DefaultColumnSpan(VisualType visual) => visual switch
    {
        VisualType.Kpi     => 4,
        VisualType.Gauge   => 4,
        VisualType.Pie     => 5,
        VisualType.Donut   => 5,
        VisualType.Radar   => 7,
        VisualType.Table   => 10,
        VisualType.Heatmap => 8,
        VisualType.Treemap => 8,
        _                  => 6
    };

    /// <summary>Default row span per visual type.</summary>
    private static int DefaultRowSpan(VisualType visual) => visual switch
    {
        VisualType.Kpi     => 3,
        VisualType.Gauge   => 4,
        VisualType.Table   => 6,
        VisualType.Radar   => 6,
        VisualType.Heatmap => 6,
        VisualType.Treemap => 6,
        _                  => 4
    };

    /// <summary>
    /// Creates an independent copy of the given widget — new <see cref="DashboardWidget.Id"/>,
    /// same chart type / field mappings / local filters, offset by one grid unit so the
    /// copy doesn't sit exactly on top of the original — and adds it to the canvas.
    /// </summary>
    public DashboardWidget DuplicateWidget(DashboardWidget source)
    {
        if (ActiveDashboard is not null) UndoRedo.RecordSnapshot(ActiveDashboard);
        var gridColumns = ActiveDashboard?.GridColumns;

        var copy = new DashboardWidget
        {
            Title = $"{source.Title} (Copy)",
            VisualType = source.VisualType,
            DataSourceId = source.DataSourceId,
            DimensionFields = [.. source.DimensionFields],
            MeasureFields = [.. source.MeasureFields],
            SeriesFields = [.. source.SeriesFields],
            StyleOverrides = new Dictionary<string, string>(source.StyleOverrides),
            LocalFilters = source.LocalFilters
                .Select(f => new WidgetFilter
                {
                    FieldName = f.FieldName,
                    Operator = f.Operator,
                    Values = [.. f.Values],
                    IsEnabled = f.IsEnabled
                })
                .ToList(),
            IsCrossFilterSource = source.IsCrossFilterSource,
            IsCrossFilterTarget = source.IsCrossFilterTarget,
            Layout = new WidgetLayout
            {
                Column = GridGeometryHelper.ClampPosition(source.Layout.Column + 1, source.Layout.ColumnSpan, gridColumns),
                Row = source.Layout.Row + 1,
                ColumnSpan = source.Layout.ColumnSpan,
                RowSpan = source.Layout.RowSpan
            }
        };

        AddWidget(copy);
        _logger.LogInformation("Duplicated widget '{Source}' → '{Copy}'.", source.Title, copy.Title);
        return copy;
    }

    /// <summary>
    /// Duplicates every selected widget (or just <see cref="SelectedWidget"/> if nothing
    /// is multi-selected). Bound to the canvas toolbar's Duplicate button.
    /// </summary>
    [RelayCommand]
    public void DuplicateSelectedWidgets()
    {
        var source = SelectedWidgets.Count > 0
            ? SelectedWidgets.ToList()
            : SelectedWidget is not null ? [SelectedWidget] : new List<DashboardWidget>();

        foreach (var widget in source)
            DuplicateWidget(widget);
    }

    /// <summary>Clears every active cross-filter on the dashboard.</summary>
    [RelayCommand]
    public void ClearFilters()
    {
        FilterManager.ClearAll();
        _logger.LogInformation("Cross-filters cleared via toolbar command.");
    }

    /// <summary>Undoes the most recent layout change.</summary>
    [RelayCommand]
    public void Undo()
    {
        if (ActiveDashboard is null) return;
        var restored = UndoRedo.Undo(ActiveDashboard);
        if (restored is null) return;

        ActiveDashboard.Widgets = restored;
        ReplaceWidgets(restored);
        ClearSelection();
    }

    /// <summary>Re-applies the most recently undone layout change.</summary>
    [RelayCommand]
    public void Redo()
    {
        if (ActiveDashboard is null) return;
        var restored = UndoRedo.Redo(ActiveDashboard);
        if (restored is null) return;

        ActiveDashboard.Widgets = restored;
        ReplaceWidgets(restored);
        ClearSelection();
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Selects a widget. With <paramref name="additive"/> = false (a plain click),
    /// this clears any existing selection first. With true (Ctrl+click), the widget
    /// is toggled into/out of the existing multi-select set.
    /// </summary>
    public void SelectWidget(DashboardWidget widget, bool additive)
    {
        if (!additive)
        {
            foreach (var w in SelectedWidgets)
                w.IsSelected = false;
            SelectedWidgets.Clear();

            widget.IsSelected = true;
            SelectedWidgets.Add(widget);
            SelectedWidget = widget;
            return;
        }

        if (SelectedWidgets.Contains(widget))
        {
            widget.IsSelected = false;
            SelectedWidgets.Remove(widget);
            SelectedWidget = SelectedWidgets.LastOrDefault();
        }
        else
        {
            widget.IsSelected = true;
            SelectedWidgets.Add(widget);
            SelectedWidget = widget;
        }
    }

    /// <summary>Clears all selection state.</summary>
    public void ClearSelection()
    {
        foreach (var w in SelectedWidgets)
            w.IsSelected = false;
        SelectedWidgets.Clear();
        SelectedWidget = null;
    }

    // ── Move / resize (snap-to-grid + bounds clamping) ──────────────────────────

    /// <summary>
    /// Moves a widget to a new grid position, snapping and clamping as configured.
    /// <paramref name="proposedColumn"/>/<paramref name="proposedRow"/> are raw (already
    /// grid-unit) targets; clamping keeps the widget within the dashboard's column bound
    /// and never lets it go negative.
    /// </summary>
    public void MoveWidget(DashboardWidget widget, int proposedColumn, int proposedRow)
    {
        if (ActiveDashboard?.IsReadOnly == true) return;
        var gridColumns = SnapToGrid ? ActiveDashboard?.GridColumns : null;

        widget.Layout.Column = GridGeometryHelper.ClampPosition(proposedColumn, widget.Layout.ColumnSpan, gridColumns);
        widget.Layout.Row = GridGeometryHelper.ClampPosition(proposedRow, widget.Layout.RowSpan, null);
    }

    /// <summary>
    /// Resizes a widget to a new span, clamping to the configured minimum size so a
    /// widget can never be dragged down to nothing.
    /// </summary>
    public void ResizeWidget(DashboardWidget widget, int proposedColumnSpan, int proposedRowSpan)
    {
        if (ActiveDashboard?.IsReadOnly == true) return;
        widget.Layout.ColumnSpan = GridGeometryHelper.ClampColumnSpan(proposedColumnSpan);
        widget.Layout.RowSpan = GridGeometryHelper.ClampRowSpan(proposedRowSpan);
    }

    // ── Alignment guides ──────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes alignment guides for the widget currently being dragged/resized
    /// against every other widget on the canvas, and publishes them to
    /// <see cref="ActiveGuides"/> for the view to render.
    /// </summary>
    public void UpdateGuides(DashboardWidget moving)
    {
        var guides = GridGeometryHelper.ComputeAlignmentGuides(moving, Widgets);

        ActiveGuides.Clear();
        foreach (var guide in guides)
            ActiveGuides.Add(guide);
    }

    /// <summary>Clears any active alignment guides. Call when a drag/resize gesture ends.</summary>
    public void ClearGuides() => ActiveGuides.Clear();

    // ── Drop placement helpers ───────────────────────────────────────────────

    /// <summary>
    /// Naive staggered placement so successive drops don't all land in the same
    /// spot. A real layout-aware placement (skip occupied cells) is a natural next
    /// refinement once Phase 11 templates start placing many widgets at once.
    /// </summary>
    private int NextDropColumn() => (Widgets.Count * 2) % Math.Max(ActiveDashboard?.GridColumns ?? 24, 1);

    private int NextDropRow() => (Widgets.Count / 4) * 4;
}

// ─────────────────────────────────────────────────────────────────────────────
//  Visual Library ViewModel
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Drives the right panel: the library of available chart types.
/// Phase 3 adds drag-to-canvas support.
/// </summary>
public sealed partial class VisualLibraryViewModel : ObservableObject
{
    private readonly ILogger<VisualLibraryViewModel> _logger;

    /// <summary>All visual types available for drag-and-drop.</summary>
    public IReadOnlyList<VisualTypeEntry> AvailableVisuals { get; } =
        Enum.GetValues(typeof(VisualType))
            .Cast<VisualType>()
            .Select(vt => new VisualTypeEntry(vt))
            .ToList();

    [ObservableProperty]
    private VisualTypeEntry? _selectedVisual;

    /// <summary>Initialises the visual library.</summary>
    public VisualLibraryViewModel(ILogger<VisualLibraryViewModel> logger)
    {
        _logger = logger;
    }
}

/// <summary>Wraps a <see cref="VisualType"/> for display in the library panel.</summary>
public sealed class VisualTypeEntry
{
    /// <summary>The visual type enum value.</summary>
    public VisualType VisualType { get; }

    /// <summary>Display name.</summary>
    public string DisplayName { get; }

    /// <summary>Material icon key used in the library tile.</summary>
    public string IconKey { get; }

    /// <summary>Initialises the entry for the given type.</summary>
    public VisualTypeEntry(VisualType visualType)
    {
        VisualType = visualType;
        DisplayName = visualType.ToString();
        IconKey = visualType switch
        {
            VisualType.Kpi       => "Numeric",
            VisualType.Bar       => "ChartBar",
            VisualType.Line      => "ChartLine",
            VisualType.Pie       => "ChartPie",
            VisualType.Donut     => "ChartDonut",
            VisualType.Area      => "ChartAreaspline",
            VisualType.Scatter   => "ChartScatterPlot",
            VisualType.Bubble    => "ChartBubble",
            VisualType.Radar     => "ChartRadar",
            VisualType.Heatmap   => "ViewComfy",
            VisualType.Treemap   => "ChartTree",
            VisualType.Gauge     => "Gauge",
            VisualType.Table     => "Table",
            _                    => "ChartBox"
        };
    }
}
