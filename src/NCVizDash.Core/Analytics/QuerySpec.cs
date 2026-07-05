using System.Text.Json.Serialization;
using NCVizDash.Models;

namespace NCVizDash.Core.Analytics;

/// <summary>Aggregate function applied to a measure column.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AggregateFunction
{
    /// <summary>SUM aggregate.</summary>
    Sum,
    /// <summary>COUNT aggregate.</summary>
    Count,
    /// <summary>COUNT DISTINCT aggregate.</summary>
    CountDistinct,
    /// <summary>AVG aggregate.</summary>
    Avg,
    /// <summary>MIN aggregate.</summary>
    Min,
    /// <summary>MAX aggregate.</summary>
    Max,
    /// <summary>Raw, non-aggregated column (used by Scatter/Bubble, which plot individual points).</summary>
    None
}

/// <summary>Window function applied over the query's result set.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WindowFunctionType
{
    /// <summary>No window function.</summary>
    None,
    /// <summary>Sequential row number within the (optionally partitioned) result set.</summary>
    RowNumber,
    /// <summary>Rank with gaps (ties share a rank, next rank skips).</summary>
    Rank,
    /// <summary>Rank without gaps (ties share a rank, next rank does not skip).</summary>
    DenseRank,
    /// <summary>Cumulative sum: SUM(...) OVER (ORDER BY ... ROWS UNBOUNDED PRECEDING).</summary>
    RunningTotal,
    /// <summary>Trailing moving average: AVG(...) OVER (ORDER BY ... ROWS BETWEEN N PRECEDING AND CURRENT ROW).</summary>
    MovingAverage,
    /// <summary>Each row's value as a percentage of the partition total: value / SUM(value) OVER ().</summary>
    PercentOfTotal
}

/// <summary>
/// A calculated measure: a raw SQL scalar expression (e.g. "revenue - cost") evaluated
/// per row/group and exposed under <see cref="Alias"/>. Unlike <see cref="MeasureSpec"/>,
/// this carries an author-written expression rather than a single column + aggregate
/// function — Phase 12's "Calculated Measures" feature. The expression is NOT
/// sanitised beyond basic guard checks (see <c>AnalyticsQueryBuilder.BuildCalculatedColumn</c>)
/// since it is user-authored formula text, not untrusted external input; it still runs
/// inside the same sandboxed in-memory DuckDB instance as everything else.
/// </summary>
public sealed class CalculatedMeasureSpec
{
    /// <summary>Display name for the calculated measure (used as its output column alias).</summary>
    public string Alias { get; init; } = string.Empty;

    /// <summary>
    /// A SQL scalar expression referencing other (sanitised) column names, e.g.
    /// <c>"revenue - cost"</c> or <c>"revenue / NULLIF(units, 0)"</c>. Column names
    /// in the expression must already be sanitised (lowercase, underscores) to match
    /// what's actually in the DuckDB table — the UI layer is responsible for building
    /// this from field pickers rather than free-text SQL entry, to keep it safe.
    /// </summary>
    public string Expression { get; init; } = string.Empty;
}

/// <summary>A single measure column with its aggregation.</summary>
public sealed class MeasureSpec
{
    /// <summary>The (sanitised) column name to aggregate.</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>The aggregate function to apply.</summary>
    public AggregateFunction Aggregate { get; init; } = AggregateFunction.Sum;

    /// <summary>Optional output alias; defaults to the sanitised field name if omitted.</summary>
    public string? Alias { get; init; }
}

/// <summary>Window function configuration, applied after aggregation.</summary>
public sealed class WindowFunctionSpec
{
    /// <summary>Which window function to apply.</summary>
    public WindowFunctionType Type { get; init; } = WindowFunctionType.None;

    /// <summary>Field the window function orders/partitions by (typically a time or measure field).</summary>
    public string OrderByField { get; init; } = string.Empty;

    /// <summary>Fields to partition the window by (e.g. one series per department).</summary>
    public List<string> PartitionByFields { get; init; } = [];

    /// <summary>Window size for <see cref="WindowFunctionType.MovingAverage"/> (rows preceding, inclusive of current).</summary>
    public int WindowSize { get; init; } = 3;

    /// <summary>Output column alias for the window function's result.</summary>
    public string Alias { get; init; } = "window_result";
}

/// <summary>Pivot configuration — turns distinct values of one dimension into columns.</summary>
public sealed class PivotSpec
{
    /// <summary>The dimension whose distinct values become new columns.</summary>
    public string PivotField { get; init; } = string.Empty;

    /// <summary>The measure aggregated into each pivoted column.</summary>
    public string ValueField { get; init; } = string.Empty;

    /// <summary>The aggregate function applied when collapsing rows into each pivoted column.</summary>
    public AggregateFunction Aggregate { get; init; } = AggregateFunction.Sum;
}

/// <summary>
/// Complete, engine-agnostic description of an analytics query. Translated into SQL
/// by <c>AnalyticsQueryBuilder</c> (DuckDB-flavoured), kept separate from the widget
/// model so the same spec shape could target a different backend in the future.
/// </summary>
public sealed class QuerySpec
{
    /// <summary>The DuckDB table to query.</summary>
    public string TableName { get; init; } = string.Empty;

    /// <summary>Dimension (GROUP BY) fields, in order.</summary>
    public List<string> Dimensions { get; init; } = [];

    /// <summary>Measure fields with their aggregation.</summary>
    public List<MeasureSpec> Measures { get; init; } = [];

    /// <summary>Calculated measures — author-written expressions evaluated alongside the regular measures (Phase 12).</summary>
    public List<CalculatedMeasureSpec> CalculatedMeasures { get; init; } = [];

    /// <summary>Widget-scoped filters to apply as WHERE clauses.</summary>
    public List<WidgetFilter> Filters { get; init; } = [];

    /// <summary>Field to sort by. Defaults to the first dimension if null.</summary>
    public string? SortField { get; init; }

    /// <summary>Sort direction.</summary>
    public bool SortDescending { get; init; }

    /// <summary>Maximum rows to return (Top N). Null = no limit beyond the engine's safety cap.</summary>
    public int? Limit { get; init; }

    /// <summary>Optional window function applied over the result set.</summary>
    public WindowFunctionSpec? WindowFunction { get; init; }

    /// <summary>Optional pivot — mutually exclusive with GROUP BY aggregation on the same measure.</summary>
    public PivotSpec? Pivot { get; init; }
}
