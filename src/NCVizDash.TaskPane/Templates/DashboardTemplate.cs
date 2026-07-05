using NCVizDash.Models;

namespace NCVizDash.TaskPane.Templates;

/// <summary>
/// Describes one "slot" in a template — a widget the template wants, expressed as
/// a visual type plus how many Measure/Dimension/Time fields it needs. Slots are
/// filled with whatever matching fields exist in the user's actual data at
/// instantiation time; templates never hardcode real field names, since the whole
/// point is to apply the same template to any workbook.
/// </summary>
public sealed class TemplateWidgetSlot
{
    /// <summary>Display title for the widget created from this slot.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>Chart or visual type for this slot.</summary>
    public VisualType VisualType { get; init; }
    /// <summary>Number of measure fields required to fill this slot.</summary>
    public int MeasuresNeeded { get; init; } = 1;
    /// <summary>Number of dimension fields required to fill this slot.</summary>
    public int DimensionsNeeded { get; init; }
    /// <summary>When true, prefers a time dimension when matching fields.</summary>
    public bool PreferTimeDimension { get; init; }
    /// <summary>Default column span on the dashboard grid.</summary>
    public int ColumnSpan { get; init; } = 6;
    /// <summary>Default row span on the dashboard grid.</summary>
    public int RowSpan { get; init; } = 4;
}

/// <summary>A named, pre-built dashboard layout — a list of widget slots plus a display name/description.</summary>
public sealed class DashboardTemplate
{
    /// <summary>Template display name.</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>Short description shown in the template picker.</summary>
    public string Description { get; init; } = string.Empty;
    /// <summary>Category used to group templates in the UI.</summary>
    public string Category { get; init; } = string.Empty;
    /// <summary>Widget slots that make up this template layout.</summary>
    public IReadOnlyList<TemplateWidgetSlot> Slots { get; init; } = [];
}

/// <summary>
/// The ten built-in dashboard templates. Every slot is generic (measure/dimension
/// counts, not field names), so <see cref="TemplateInstantiationService"/> can fill
/// them from any data source regardless of business domain.
/// </summary>
public static class TemplateRegistry
{
    /// <summary>All built-in dashboard templates.</summary>
    public static IReadOnlyList<DashboardTemplate> All { get; } =
    [
        new DashboardTemplate
        {
            Name = "Executive Dashboard", Category = "Leadership",
            Description = "High-level KPIs and trends for leadership review.",
            Slots =
            [
                new() { Title = "Headline KPI", VisualType = VisualType.Kpi, MeasuresNeeded = 1, ColumnSpan = 4, RowSpan = 3 },
                new() { Title = "Trend Over Time", VisualType = VisualType.Line, MeasuresNeeded = 1, PreferTimeDimension = true, ColumnSpan = 12, RowSpan = 5 },
                new() { Title = "Breakdown by Category", VisualType = VisualType.Pie, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 8, RowSpan = 5 }
            ]
        },
        new DashboardTemplate
        {
            Name = "Engineering Dashboard", Category = "Engineering",
            Description = "Delivery velocity and workload by team.",
            Slots =
            [
                new() { Title = "Throughput Over Time", VisualType = VisualType.Line, MeasuresNeeded = 1, PreferTimeDimension = true, ColumnSpan = 12, RowSpan = 5 },
                new() { Title = "Workload by Team", VisualType = VisualType.Bar, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 8, RowSpan = 5 },
                new() { Title = "Total", VisualType = VisualType.Kpi, MeasuresNeeded = 1, ColumnSpan = 4, RowSpan = 3 }
            ]
        },
        new DashboardTemplate
        {
            Name = "Sprint Dashboard", Category = "Engineering",
            Description = "Sprint burndown and status distribution.",
            Slots =
            [
                new() { Title = "Burndown", VisualType = VisualType.Area, MeasuresNeeded = 1, PreferTimeDimension = true, ColumnSpan = 12, RowSpan = 5 },
                new() { Title = "By Status", VisualType = VisualType.Donut, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 6, RowSpan = 5 },
                new() { Title = "By Assignee", VisualType = VisualType.Bar, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 6, RowSpan = 5 }
            ]
        },
        new DashboardTemplate
        {
            Name = "QA Dashboard", Category = "Quality",
            Description = "Defect trends and severity breakdown.",
            Slots =
            [
                new() { Title = "Defects Over Time", VisualType = VisualType.Line, MeasuresNeeded = 1, PreferTimeDimension = true, ColumnSpan = 12, RowSpan = 5 },
                new() { Title = "By Severity", VisualType = VisualType.Pie, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 6, RowSpan = 5 },
                new() { Title = "By Module", VisualType = VisualType.Treemap, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 6, RowSpan = 5 }
            ]
        },
        new DashboardTemplate
        {
            Name = "Finance Dashboard", Category = "Finance",
            Description = "Revenue, cost, and margin overview.",
            Slots =
            [
                new() { Title = "Revenue", VisualType = VisualType.Kpi, MeasuresNeeded = 1, ColumnSpan = 4, RowSpan = 3 },
                new() { Title = "Cost", VisualType = VisualType.Kpi, MeasuresNeeded = 1, ColumnSpan = 4, RowSpan = 3 },
                new() { Title = "Trend", VisualType = VisualType.Area, MeasuresNeeded = 1, PreferTimeDimension = true, ColumnSpan = 12, RowSpan = 5 },
                new() { Title = "By Department", VisualType = VisualType.Bar, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 8, RowSpan = 5 }
            ]
        },
        new DashboardTemplate
        {
            Name = "HR Dashboard", Category = "HR",
            Description = "Headcount and distribution by department.",
            Slots =
            [
                new() { Title = "Headcount", VisualType = VisualType.Kpi, MeasuresNeeded = 1, ColumnSpan = 4, RowSpan = 3 },
                new() { Title = "By Department", VisualType = VisualType.Bar, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 8, RowSpan = 5 },
                new() { Title = "Distribution", VisualType = VisualType.Donut, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 6, RowSpan = 5 }
            ]
        },
        new DashboardTemplate
        {
            Name = "Project Dashboard", Category = "PMO",
            Description = "Project status and progress tracking.",
            Slots =
            [
                new() { Title = "Completion", VisualType = VisualType.Gauge, MeasuresNeeded = 1, ColumnSpan = 4, RowSpan = 4 },
                new() { Title = "Progress Over Time", VisualType = VisualType.Line, MeasuresNeeded = 1, PreferTimeDimension = true, ColumnSpan = 12, RowSpan = 5 },
                new() { Title = "By Project", VisualType = VisualType.Bar, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 8, RowSpan = 5 }
            ]
        },
        new DashboardTemplate
        {
            Name = "PMO Dashboard", Category = "PMO",
            Description = "Portfolio-level rollup across projects.",
            Slots =
            [
                new() { Title = "Portfolio Total", VisualType = VisualType.Kpi, MeasuresNeeded = 1, ColumnSpan = 4, RowSpan = 3 },
                new() { Title = "By Project", VisualType = VisualType.Treemap, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 12, RowSpan = 6 },
                new() { Title = "Comparison", VisualType = VisualType.Radar, MeasuresNeeded = 3, ColumnSpan = 8, RowSpan = 6 }
            ]
        },
        new DashboardTemplate
        {
            Name = "Inventory Dashboard", Category = "Operations",
            Description = "Stock levels and turnover by location.",
            Slots =
            [
                new() { Title = "Total Stock", VisualType = VisualType.Kpi, MeasuresNeeded = 1, ColumnSpan = 4, RowSpan = 3 },
                new() { Title = "By Warehouse", VisualType = VisualType.Bar, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 8, RowSpan = 5 },
                new() { Title = "Heatmap", VisualType = VisualType.Heatmap, MeasuresNeeded = 1, DimensionsNeeded = 2, ColumnSpan = 12, RowSpan = 6 }
            ]
        },
        new DashboardTemplate
        {
            Name = "Delivery Dashboard", Category = "Engineering",
            Description = "Release cadence and delivery predictability.",
            Slots =
            [
                new() { Title = "Releases Delivered", VisualType = VisualType.Kpi, MeasuresNeeded = 1, ColumnSpan = 4, RowSpan = 3 },
                new() { Title = "Delivery Over Time", VisualType = VisualType.Area, MeasuresNeeded = 1, PreferTimeDimension = true, ColumnSpan = 12, RowSpan = 5 },
                new() { Title = "By Team", VisualType = VisualType.Bar, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 8, RowSpan = 5 }
            ]
        },
        new DashboardTemplate
        {
            Name = "Sales Dashboard", Category = "Sales",
            Description = "Revenue trends and rep performance.",
            Slots =
            [
                new() { Title = "Total Revenue", VisualType = VisualType.Kpi, MeasuresNeeded = 1, ColumnSpan = 4, RowSpan = 3 },
                new() { Title = "Trend", VisualType = VisualType.Area, MeasuresNeeded = 1, PreferTimeDimension = true, ColumnSpan = 12, RowSpan = 5 },
                new() { Title = "By Rep", VisualType = VisualType.Bar, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 8, RowSpan = 5 },
                new() { Title = "By Region", VisualType = VisualType.Pie, MeasuresNeeded = 1, DimensionsNeeded = 1, ColumnSpan = 6, RowSpan = 5 }
            ]
        }
    ];
}
