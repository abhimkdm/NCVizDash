using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NCVizDash.Models;

/// <summary>Supported chart/visual types in the chart engine.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VisualType
{
    /// <summary>Single-value KPI card.</summary>
    Kpi,
    /// <summary>Bar chart.</summary>
    Bar,
    /// <summary>Line chart.</summary>
    Line,
    /// <summary>Pie chart.</summary>
    Pie,
    /// <summary>Donut chart.</summary>
    Donut,
    /// <summary>Area chart.</summary>
    Area,
    /// <summary>Scatter plot.</summary>
    Scatter,
    /// <summary>Bubble chart.</summary>
    Bubble,
    /// <summary>Radar chart.</summary>
    Radar,
    /// <summary>Heatmap.</summary>
    Heatmap,
    /// <summary>Treemap.</summary>
    Treemap,
    /// <summary>Gauge.</summary>
    Gauge,
    /// <summary>Tabular grid.</summary>
    Table
}

/// <summary>Position and size of a widget on the canvas (in grid units).</summary>
public sealed class WidgetLayout : INotifyPropertyChanged
{
    private int _column;
    private int _row;
    private int _columnSpan = 4;
    private int _rowSpan = 3;

    /// <summary>Zero-based grid column where the widget starts.</summary>
    public int Column
    {
        get => _column;
        set => SetField(ref _column, value);
    }

    /// <summary>Zero-based grid row where the widget starts.</summary>
    public int Row
    {
        get => _row;
        set => SetField(ref _row, value);
    }

    /// <summary>Width of the widget, in grid columns.</summary>
    public int ColumnSpan
    {
        get => _columnSpan;
        set => SetField(ref _columnSpan, value);
    }

    /// <summary>Height of the widget, in grid rows.</summary>
    public int RowSpan
    {
        get => _rowSpan;
        set => SetField(ref _rowSpan, value);
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>Comparison operator for a persisted widget-level filter.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FilterOperator
{
    /// <summary>Field equals the given value.</summary>
    Equals,
    /// <summary>Field does not equal the given value.</summary>
    NotEquals,
    /// <summary>Field is greater than the given value.</summary>
    GreaterThan,
    /// <summary>Field is greater than or equal to the given value.</summary>
    GreaterThanOrEqual,
    /// <summary>Field is less than the given value.</summary>
    LessThan,
    /// <summary>Field is less than or equal to the given value.</summary>
    LessThanOrEqual,
    /// <summary>Field contains the given text (case-insensitive substring match).</summary>
    Contains,
    /// <summary>Field's value is one of the given set.</summary>
    In,
    /// <summary>Field's value is not one of the given set.</summary>
    NotIn,
    /// <summary>Field's value falls within the given inclusive range.</summary>
    Between
}

/// <summary>
/// A persisted, widget-scoped filter override. Unlike runtime cross-filters
/// (managed transiently by <c>IFilterManager</c> and never saved), local filters
/// are part of the widget's saved definition and survive a dashboard reload —
/// e.g. "this chart always excludes Q1, regardless of the active global date filter".
/// </summary>
public sealed class WidgetFilter
{
    /// <summary>Unique filter ID (stable across saves so the UI can edit/remove a specific filter).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The field this filter applies to (must exist on the widget's data source).</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>The comparison operator.</summary>
    public FilterOperator Operator { get; set; } = FilterOperator.Equals;

    /// <summary>
    /// Serialised value(s) being compared against. For <see cref="FilterOperator.In"/>,
    /// <see cref="FilterOperator.NotIn"/>, and <see cref="FilterOperator.Between"/> this holds
    /// multiple values; for all other operators it holds exactly one.
    /// </summary>
    public List<string> Values { get; set; } = [];

    /// <summary>Whether this filter is currently active. Allows toggling off without deleting it.</summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>A single visual widget on the dashboard canvas.</summary>
public sealed class DashboardWidget : INotifyPropertyChanged
{
    private bool _isSelected;

    /// <summary>Unique widget ID.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Widget title shown in the card header.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The chart/visual type to render.</summary>
    public VisualType VisualType { get; set; } = VisualType.Bar;

    /// <summary>ID of the data source this widget queries.</summary>
    public Guid DataSourceId { get; set; }

    /// <summary>Fields used for X-axis / category / dimension.</summary>
    public List<string> DimensionFields { get; set; } = [];

    /// <summary>Fields used for Y-axis / value / measure.</summary>
    public List<string> MeasureFields { get; set; } = [];

    /// <summary>Fields used to colour/series-split the visual.</summary>
    public List<string> SeriesFields { get; set; } = [];

    /// <summary>Layout position on the dashboard grid.</summary>
    public WidgetLayout Layout { get; set; } = new();

    /// <summary>Theme overrides specific to this widget.</summary>
    public Dictionary<string, string> StyleOverrides { get; set; } = [];

    /// <summary>
    /// Widget-scoped filter overrides that persist with the dashboard, independent
    /// of the transient runtime cross-filter state managed by <c>IFilterManager</c>.
    /// </summary>
    public List<WidgetFilter> LocalFilters { get; set; } = [];

    /// <summary>
    /// Conditional-formatting rules evaluated against this widget's rendered values
    /// (Phase 12) — e.g. colour a KPI red when its value drops below a threshold.
    /// </summary>
    public List<ConditionalFormatRule> ConditionalFormatRules { get; set; } = [];

    /// <summary>Discussion thread attached to this widget (Phase 15).</summary>
    public List<WidgetComment> Comments { get; set; } = [];

    /// <summary>
    /// When set, limits the widget's query to this many rows, sorted by its
    /// primary measure — powers "Top N" / "Bottom N" widgets (v2.0 One-Click
    /// Dashboard Generator). Null means no row limit beyond the render
    /// coordinator's default safety cap.
    /// </summary>
    public int? TopN { get; set; }

    /// <summary>When <see cref="TopN"/> is set, true = descending (Top N), false = ascending (Bottom N).</summary>
    public bool TopNDescending { get; set; } = true;

    /// <summary>Whether this widget participates in cross-filtering as a source.</summary>
    public bool IsCrossFilterSource { get; set; } = true;

    /// <summary>Whether this widget reacts to cross-filter events.</summary>
    public bool IsCrossFilterTarget { get; set; } = true;

    /// <summary>
    /// Whether this widget is currently selected on the canvas. Transient UI state —
    /// not persisted as part of the dashboard's saved JSON.
    /// </summary>
    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>A complete dashboard – a named collection of widgets.</summary>
public sealed class Dashboard
{
    /// <summary>Unique dashboard ID.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Dashboard display name.</summary>
    public string Name { get; set; } = "New Dashboard";

    /// <summary>Optional description shown in the dashboard picker.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Template this dashboard was created from (null if bespoke).</summary>
    public string? TemplateName { get; set; }

    /// <summary>Active colour theme identifier.</summary>
    public string Theme { get; set; } = "Light";

    /// <summary>Grid column count for the canvas.</summary>
    public int GridColumns { get; set; } = 24;

    /// <summary>Grid row height in pixels.</summary>
    public int GridRowHeight { get; set; } = 40;

    /// <summary>All widgets on this dashboard.</summary>
    public List<DashboardWidget> Widgets { get; set; } = [];

    /// <summary>
    /// Dashboard-wide filters applied to every widget regardless of field mappings —
    /// unlike <see cref="DashboardWidget.LocalFilters"/> (widget-scoped) and runtime
    /// cross-filters (transient, click-driven, self-excluding). Reuses <see cref="WidgetFilter"/>
    /// so both persisted filter lists share one shape and one JSON representation.
    /// </summary>
    public List<WidgetFilter> GlobalFilters { get; set; } = [];

    /// <summary>UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC last-modified timestamp.</summary>
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Schema version for forward-compatibility.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>When true, the canvas UI should disable every mutating command (Phase 15 read-only sharing).</summary>
    public bool IsReadOnly { get; set; }

    /// <summary>Optional attribution when this dashboard was shared/imported from another user (Phase 15).</summary>
    public string? SharedBy { get; set; }
}
