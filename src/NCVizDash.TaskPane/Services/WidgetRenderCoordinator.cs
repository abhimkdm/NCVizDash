using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Analytics;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Services;

/// <summary>
/// Bridges a <see cref="DashboardWidget"/>'s field mappings, local filters, and
/// active cross-filters to a <see cref="QuerySpec"/>, runs it through
/// <see cref="IAnalyticsEngine"/>, and returns the rendering payload JSON ready
/// for <see cref="Controls.ChartHost.RenderAsync"/>.
/// <para>
/// Query construction is engine-agnostic here — <see cref="QuerySpec"/> is translated
/// to DuckDB SQL by <c>AnalyticsQueryBuilder</c> inside the concrete
/// <c>DuckDbAnalyticsEngine</c>. This class never builds SQL strings directly.
/// </para>
/// </summary>
public sealed class WidgetRenderCoordinator
{
    private readonly IAnalyticsEngine _analyticsEngine;
    private readonly IChartEngine _chartEngine;
    private readonly IFilterManager _filterManager;
    private readonly IGlobalFilterManager _globalFilterManager;
    private readonly ILogger<WidgetRenderCoordinator> _logger;

    private const int DefaultRowLimit = 500;
    private const int TableRowLimit = 200;

    /// <summary>Initialises the coordinator.</summary>
    public WidgetRenderCoordinator(
        IAnalyticsEngine analyticsEngine,
        IChartEngine chartEngine,
        IFilterManager filterManager,
        IGlobalFilterManager globalFilterManager,
        ILogger<WidgetRenderCoordinator> logger)
    {
        _analyticsEngine = analyticsEngine;
        _chartEngine = chartEngine;
        _filterManager = filterManager;
        _globalFilterManager = globalFilterManager;
        _logger = logger;
    }

    /// <summary>
    /// Queries the widget's data source — aggregated per its field mappings, filtered
    /// by its own <see cref="DashboardWidget.LocalFilters"/> plus any active
    /// cross-filters from other widgets (unless the widget opted out via
    /// <see cref="DashboardWidget.IsCrossFilterTarget"/>) — and returns the JSON
    /// rendering payload for the given theme. Never throws; returns a JSON error
    /// envelope if the data source isn't loaded or the widget is unconfigured.
    /// </summary>
    public async Task<string> RenderWidgetAsync(DashboardWidget widget, string theme, CancellationToken ct = default)
    {
        var tableName = _analyticsEngine.GetTableName(widget.DataSourceId);

        if (tableName is null)
        {
            return ErrorPayload("No data source is loaded for this widget yet — drag a field from the Data Explorer onto the canvas.");
        }

        if (widget.DimensionFields.Count == 0 && widget.MeasureFields.Count == 0)
        {
            return ErrorPayload("This widget has no fields configured. Drag a field onto it to get started.");
        }

        try
        {
            var spec = BuildQuerySpec(tableName, widget);
            _logger.LogDebug(
                "Rendering widget '{Title}' via QuerySpec (dims={Dims}, measures={Measures}, filters={Filters}).",
                widget.Title, spec.Dimensions.Count, spec.Measures.Count, spec.Filters.Count);

            var rows = await _analyticsEngine.QueryAsync(spec, ct);

            return _chartEngine is NCVizDash.ChartEngine.EChartsChartEngine themedEngine
                ? themedEngine.BuildChartOption(widget, rows, theme)
                : _chartEngine.BuildChartOption(widget, rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render widget '{Title}'.", widget.Title);
            return ErrorPayload($"Rendering failed: {ex.Message}");
        }
    }

    // ── QuerySpec construction ────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="QuerySpec"/> from the widget's field mappings, own local
    /// filters, and — when the widget participates as a cross-filter target — the
    /// active cross-filters from every other widget on the dashboard. The widget's
    /// own clicks never filter itself back down to a single value (excluded via
    /// <c>excludeSourceWidgetId</c>), so re-clicking a different category still works.
    /// </summary>
    private QuerySpec BuildQuerySpec(string tableName, DashboardWidget widget)
    {
        var needsRawRows = widget.VisualType is VisualType.Scatter or VisualType.Bubble;

        var measures = widget.MeasureFields
            .Select(f => new MeasureSpec
            {
                Field = f,
                Aggregate = needsRawRows ? AggregateFunction.None : AggregateFunction.Sum
            })
            .ToList();

        var limit = widget.TopN ?? (widget.VisualType == VisualType.Table ? TableRowLimit : DefaultRowLimit);
        var sortField = widget.TopN.HasValue ? widget.MeasureFields.FirstOrDefault() : null;

        var filters = widget.LocalFilters.Where(f => f.IsEnabled).ToList();

        // Global filters apply to every widget unconditionally — no self-exclusion,
        // no per-widget opt-out — unlike cross-filters which respect IsCrossFilterTarget
        // and exclude their own source widget.
        filters.AddRange(_globalFilterManager.GetEnabledFilters());

        if (widget.IsCrossFilterTarget)
        {
            var crossFilters = _filterManager.GetActiveFilters(excludeSourceWidgetId: widget.Id);
            filters.AddRange(crossFilters);
        }

        return new QuerySpec
        {
            TableName = tableName,
            Dimensions = [.. widget.DimensionFields],
            Measures = measures,
            Filters = filters,
            Limit = limit,
            SortField = sortField,
            SortDescending = widget.TopNDescending
        };
    }

    // ── Error payload ─────────────────────────────────────────────────────────

    private static string ErrorPayload(string message) =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "error",
            ["error"] = message
        });
}
