using NCVizDash.Models;

using NCVizDash.Core.Analytics;

namespace NCVizDash.Core.Abstractions;

// ─────────────────────────────────────────────
//  Data layer abstractions
// ─────────────────────────────────────────────

/// <summary>
/// Reads workbook structure and raw data from Excel.
/// Implemented in NCVizDash.ExcelAddIn (has VSTO references).
/// </summary>
public interface IExcelDataReader
{
    /// <summary>Discovers every Excel Table and Named Range in the active workbook.</summary>
    Task<IReadOnlyList<DataSourceDescriptor>> GetDataSourcesAsync(CancellationToken ct = default);

    /// <summary>Reads all rows for a previously-discovered data source.</summary>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(Guid dataSourceId, CancellationToken ct = default);

    /// <summary>The name of whichever worksheet is currently active in Excel, or null if there's no active workbook.</summary>
    string? GetActiveSheetName();
}

/// <summary>
/// Loads data into an in-process DuckDB instance and runs analytical queries.
/// </summary>
public interface IAnalyticsEngine
{
    /// <summary>Registers (or refreshes) a data source in DuckDB.</summary>
    Task LoadDataSourceAsync(
        DataSourceDescriptor descriptor,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        CancellationToken ct = default);

    /// <summary>Executes a raw SQL query and returns rows as dictionaries.</summary>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        string sql, CancellationToken ct = default);

    /// <summary>
    /// Executes a structured <see cref="QuerySpec"/> — translated to DuckDB SQL by
    /// <c>AnalyticsQueryBuilder</c> — covering aggregation, filtering, sorting, Top N,
    /// window functions, and pivot queries without callers needing to hand-write SQL.
    /// </summary>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        QuerySpec spec, CancellationToken ct = default);

    /// <summary>Drops a previously loaded data source from memory.</summary>
    Task UnloadDataSourceAsync(Guid dataSourceId, CancellationToken ct = default);

    /// <summary>
    /// Returns the backing table name for a previously loaded data source, or null
    /// if it hasn't been loaded. Used by chart rendering to build ad-hoc SELECT queries
    /// without callers needing to know the engine's internal naming scheme.
    /// </summary>
    string? GetTableName(Guid dataSourceId);
}

// ─────────────────────────────────────────────
//  Dashboard persistence abstractions
// ─────────────────────────────────────────────

/// <summary>Saves and retrieves dashboards from the active workbook's custom XML parts.</summary>
public interface IDashboardRepository
{
    /// <summary>Returns every dashboard saved in the active workbook.</summary>
    Task<IReadOnlyList<Dashboard>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Returns a single dashboard by ID, or null if none matches.</summary>
    Task<Dashboard?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Saves (creating or overwriting) a dashboard.</summary>
    Task SaveAsync(Dashboard dashboard, CancellationToken ct = default);

    /// <summary>Deletes a dashboard by ID.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

// ─────────────────────────────────────────────
//  Rule engine abstraction
// ─────────────────────────────────────────────

/// <summary>Deterministically selects the best visual type for a set of fields.</summary>
public interface IVisualizationRuleEngine
{
    /// <summary>
    /// Given a list of field descriptors that the user has selected,
    /// returns the recommended <see cref="VisualType"/>.
    /// </summary>
    VisualType Recommend(IReadOnlyList<FieldDescriptor> fields);

    /// <summary>
    /// Returns the recommended visual type together with a human-readable explanation
    /// of which rule fired and why. Used by the visual-picker tooltip and the
    /// Phase 12 "Explain Chart" feature.
    /// </summary>
    (VisualType Visual, string RuleName, string Explanation) RecommendWithExplanation(
        IReadOnlyList<FieldDescriptor> fields);

    /// <summary>
    /// Returns all matching visual types in priority order — used by the visual picker
    /// to offer alternative suggestions beyond the primary recommendation.
    /// </summary>
    IReadOnlyList<(VisualType Visual, string RuleName)> AllMatches(
        IReadOnlyList<FieldDescriptor> fields);
}

// ─────────────────────────────────────────────
//  Chart engine abstraction
// ─────────────────────────────────────────────

/// <summary>Generates Apache ECharts option JSON for a given widget + data.</summary>
public interface IChartEngine
{
    /// <summary>
    /// Produces the complete ECharts <c>option</c> object as a JSON string,
    /// ready to be injected into the WebView2 host.
    /// </summary>
    string BuildChartOption(
        DashboardWidget widget,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> data);
}

// ─────────────────────────────────────────────
//  Cross-filter abstraction
// ─────────────────────────────────────────────

/// <summary>Manages cross-filter state and notifies subscribers.</summary>
public interface IFilterManager
{
    /// <summary>
    /// Applies a selection from a source widget.
    /// Pass an empty collection to clear the filter for that field.
    /// Clicking the same value(s) already active for that field from the same
    /// source toggles the filter off (standard cross-filter click-to-deselect UX).
    /// </summary>
    void ApplyFilter(Guid sourceWidgetId, string fieldName, IReadOnlyList<object?> selectedValues);

    /// <summary>Clears all active cross-filters.</summary>
    void ClearAll();

    /// <summary>Returns the current combined filter as a SQL WHERE clause fragment (no leading "WHERE").</summary>
    string BuildWhereClause();

    /// <summary>
    /// Returns all currently active cross-filters as <see cref="WidgetFilter"/> instances,
    /// ready to merge into a <c>QuerySpec.Filters</c> list. When <paramref name="excludeSourceWidgetId"/>
    /// is supplied, filters originating from that widget are omitted — used so a widget
    /// doesn't filter itself down to only the value it was just clicked on.
    /// </summary>
    IReadOnlyList<WidgetFilter> GetActiveFilters(Guid? excludeSourceWidgetId = null);

    /// <summary>Number of distinct fields currently under an active cross-filter (for UI badges).</summary>
    int ActiveFilterCount { get; }

    /// <summary>Raised whenever the filter state changes.</summary>
    event EventHandler FiltersChanged;
}

// ─────────────────────────────────────────────
//  Global (dashboard-wide) filter abstraction
// ─────────────────────────────────────────────

/// <summary>
/// Manages the active <see cref="Dashboard"/>'s dashboard-wide filters.
/// Unlike <see cref="IFilterManager"/> (transient, click-driven cross-filters that
/// self-exclude their source widget), global filters are explicit, persisted
/// (<see cref="Dashboard.GlobalFilters"/>), apply to every widget unconditionally,
/// and can target any field from any loaded data source — not just fields already
/// mapped onto a widget.
/// </summary>
public interface IGlobalFilterManager
{
    /// <summary>Binds the manager to the dashboard whose <see cref="Dashboard.GlobalFilters"/> it will mutate.</summary>
    void SetDashboard(Dashboard? dashboard);

    /// <summary>The dashboard currently bound, or null if none is open.</summary>
    Dashboard? ActiveDashboard { get; }

    /// <summary>All filters on the active dashboard (enabled and disabled).</summary>
    IReadOnlyList<WidgetFilter> GetFilters();

    /// <summary>All *enabled* filters — what <c>WidgetRenderCoordinator</c> merges into every query.</summary>
    IReadOnlyList<WidgetFilter> GetEnabledFilters();

    /// <summary>Adds a new filter or replaces an existing one with the same <see cref="WidgetFilter.Id"/>.</summary>
    void AddOrUpdateFilter(WidgetFilter filter);

    /// <summary>Removes a filter by ID.</summary>
    void RemoveFilter(Guid filterId);

    /// <summary>Enables or disables a filter without removing it.</summary>
    void SetFilterEnabled(Guid filterId, bool enabled);

    /// <summary>Removes every global filter from the active dashboard.</summary>
    void ClearAll();

    /// <summary>Raised whenever the filter list changes (add, remove, enable/disable, clear, or dashboard swap).</summary>
    event EventHandler FiltersChanged;
}

// ─────────────────────────────────────────────
//  Configuration abstraction
// ─────────────────────────────────────────────

/// <summary>Provides strongly-typed access to <see cref="AppSettings"/>.</summary>
public interface IAppSettingsProvider
{
    /// <summary>The current, in-memory settings snapshot.</summary>
    AppSettings Settings { get; }

    /// <summary>Re-reads settings from disk, refreshing <see cref="Settings"/>.</summary>
    void Reload();

    /// <summary>Persists the current in-memory <see cref="Settings"/> back to disk.</summary>
    void Save();
}
