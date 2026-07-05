using Microsoft.Extensions.Logging;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Generation;

/// <summary>
/// Scans a single data source and deterministically produces a complete,
/// professional dashboard — no AI, no user configuration, matching the v2.0
/// "One-Click Dashboard Generator" spec exactly. Every widget type it can produce
/// is decided purely from field composition (what measures/dimensions/time fields
/// exist), the same deterministic philosophy as the Phase 5 rule engine, just
/// applied to "build a whole dashboard" instead of "recommend one chart type".
/// </summary>
public sealed class OneClickDashboardGenerator
{
    private readonly ILogger<OneClickDashboardGenerator> _logger;
    private const int GridColumns = 24;

    /// <summary>Initialises the one-click dashboard generator with a logger.</summary>
    public OneClickDashboardGenerator(ILogger<OneClickDashboardGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a complete dashboard from the given data source. Produces, in
    /// order, whichever of the following the data actually supports: Executive
    /// KPI cards (one per measure, capped at 4), a monthly trend chart (if a time
    /// field exists), a category-analysis bar chart, a Top 10 chart, a Bottom 10
    /// chart, a pie chart, and a summary table. Sections the data doesn't support
    /// (e.g. no time field → no trend chart) are simply omitted rather than
    /// produced empty or broken.
    /// </summary>
    public Dashboard Generate(DataSourceDescriptor dataSource)
    {
        var dashboard = new Dashboard
        {
            Name = $"{dataSource.Name} — Auto-Generated",
            Description = "Generated automatically from your data. Rearrange, resize, or delete anything.",
            TemplateName = "Auto-Generated"
        };

        var measures = dataSource.Fields.Where(f => f.FieldType == FieldType.Measure).ToList();
        var dimensions = dataSource.Fields.Where(f => f.FieldType == FieldType.Dimension).ToList();
        var timeFields = dataSource.Fields.Where(f => f.FieldType == FieldType.Time).ToList();

        var primaryMeasure = measures.FirstOrDefault();
        var primaryDimension = dimensions.FirstOrDefault();
        var primaryTime = timeFields.FirstOrDefault();

        var cursor = new LayoutCursor(GridColumns);

        // 1. Executive KPI cards — one per measure, capped at 4.
        foreach (var measure in measures.Take(4))
        {
            AddWidget(dashboard, cursor, new DashboardWidget
            {
                Title = measure.DisplayName,
                VisualType = VisualType.Kpi,
                DataSourceId = dataSource.Id,
                MeasureFields = [measure.Name]
            }, colSpan: 6, rowSpan: 3);
        }
        cursor.NewRow();

        // 2. Monthly trend — measure over time, only if a time field exists.
        if (primaryTime is not null && primaryMeasure is not null)
        {
            AddWidget(dashboard, cursor, new DashboardWidget
            {
                Title = $"{primaryMeasure.DisplayName} Trend",
                VisualType = VisualType.Line,
                DataSourceId = dataSource.Id,
                MeasureFields = [primaryMeasure.Name],
                DimensionFields = [primaryTime.Name]
            }, colSpan: GridColumns, rowSpan: 5);
            cursor.NewRow();
        }

        // 3. Category analysis — measure by primary dimension.
        if (primaryDimension is not null && primaryMeasure is not null)
        {
            AddWidget(dashboard, cursor, new DashboardWidget
            {
                Title = $"{primaryMeasure.DisplayName} by {primaryDimension.DisplayName}",
                VisualType = VisualType.Bar,
                DataSourceId = dataSource.Id,
                MeasureFields = [primaryMeasure.Name],
                DimensionFields = [primaryDimension.Name]
            }, colSpan: 12, rowSpan: 5);

            // 4/5. Top 10 / Bottom 10 — same field mapping, opposite TopN sort direction.
            AddWidget(dashboard, cursor, new DashboardWidget
            {
                Title = $"Top 10 {primaryDimension.DisplayName}",
                VisualType = VisualType.Bar,
                DataSourceId = dataSource.Id,
                MeasureFields = [primaryMeasure.Name],
                DimensionFields = [primaryDimension.Name],
                TopN = 10,
                TopNDescending = true
            }, colSpan: 6, rowSpan: 5);

            AddWidget(dashboard, cursor, new DashboardWidget
            {
                Title = $"Bottom 10 {primaryDimension.DisplayName}",
                VisualType = VisualType.Bar,
                DataSourceId = dataSource.Id,
                MeasureFields = [primaryMeasure.Name],
                DimensionFields = [primaryDimension.Name],
                TopN = 10,
                TopNDescending = false
            }, colSpan: 6, rowSpan: 5);
            cursor.NewRow();
        }

        // 6. Pie chart — measure split by a secondary dimension if one exists, else the primary.
        var pieDimension = dimensions.ElementAtOrDefault(1) ?? primaryDimension;
        if (pieDimension is not null && primaryMeasure is not null)
        {
            AddWidget(dashboard, cursor, new DashboardWidget
            {
                Title = $"{primaryMeasure.DisplayName} Distribution",
                VisualType = VisualType.Pie,
                DataSourceId = dataSource.Id,
                MeasureFields = [primaryMeasure.Name],
                DimensionFields = [pieDimension.Name]
            }, colSpan: 8, rowSpan: 6);
        }

        // 7. Summary table — every dimension + every measure, for full detail.
        AddWidget(dashboard, cursor, new DashboardWidget
        {
            Title = "Summary",
            VisualType = VisualType.Table,
            DataSourceId = dataSource.Id,
            MeasureFields = measures.Select(m => m.Name).ToList(),
            DimensionFields = dimensions.Take(3).Select(d => d.Name).ToList()
        }, colSpan: GridColumns, rowSpan: 6);

        _logger.LogInformation(
            "One-click dashboard generated from '{Source}': {Count} widget(s).",
            dataSource.Name, dashboard.Widgets.Count);

        return dashboard;
    }

    private static void AddWidget(Dashboard dashboard, LayoutCursor cursor, DashboardWidget widget, int colSpan, int rowSpan)
    {
        var (col, row) = cursor.Place(colSpan, rowSpan);
        widget.Layout = new WidgetLayout { Column = col, Row = row, ColumnSpan = colSpan, RowSpan = rowSpan };
        dashboard.Widgets.Add(widget);
    }

    /// <summary>Simple left-to-right, wrapping grid cursor — responsive layout without any manual positioning.</summary>
    private sealed class LayoutCursor(int gridColumns)
    {
        private int _col;
        private int _row;
        private int _rowMaxHeight;

        public (int Col, int Row) Place(int colSpan, int rowSpan)
        {
            if (_col + colSpan > gridColumns)
                NewRow();

            var result = (_col, _row);
            _col += colSpan;
            _rowMaxHeight = Math.Max(_rowMaxHeight, rowSpan);
            return result;
        }

        public void NewRow()
        {
            _col = 0;
            _row += _rowMaxHeight;
            _rowMaxHeight = 0;
        }
    }
}
