using NCVizDash.Models;
using NCVizDash.Core.Analytics;

namespace NCVizDash.ChartEngine.Builders;

/// <summary>
/// Carries the parsed inputs that every builder needs — widget definition,
/// raw data rows, and resolved theme — and provides shared helper methods
/// for extracting series values, building axis configs, and generating the
/// standard tooltip/legend/colour blocks.
/// </summary>
public sealed class ChartOptionContext
{
    // ── Resolved inputs ───────────────────────────────────────────────────────

    /// <summary>The widget definition this context was built for.</summary>
    public DashboardWidget Widget { get; }

    /// <summary>Raw data rows backing the widget.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; }

    /// <summary>Resolved theme name (e.g. "Light" or "Dark").</summary>
    public string Theme { get; }

    /// <summary>Whether <see cref="Theme"/> is the dark theme.</summary>
    public bool IsDark => Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);

    // ── Derived field lists ───────────────────────────────────────────────────

    /// <summary>Dimension (category) fields configured on the widget.</summary>
    public IReadOnlyList<string> DimFields  { get; }

    /// <summary>Measure (numeric) fields configured on the widget.</summary>
    public IReadOnlyList<string> MeasFields { get; }

    /// <summary>Series-breakout fields configured on the widget.</summary>
    public IReadOnlyList<string> SerFields  { get; }

    // ── Palette ───────────────────────────────────────────────────────────────

    /// <summary>Theme-appropriate colour palette for series/markers.</summary>
    public IReadOnlyList<string> Palette => ChartPalette.Palette(Theme);

    /// <summary>Initialises the context from a widget + data + theme triple.</summary>
    public ChartOptionContext(
        DashboardWidget widget,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string theme = "Light")
    {
        Widget    = widget;
        Rows      = rows;
        Theme     = theme;
        DimFields  = widget.DimensionFields.AsReadOnly();
        MeasFields = widget.MeasureFields.AsReadOnly();
        SerFields  = widget.SeriesFields.AsReadOnly();
    }

    // ── Data extraction helpers ───────────────────────────────────────────────

    /// <summary>Extracts the distinct values of the first dimension / time field as category labels.</summary>
    public List<string> CategoryLabels()
    {
        var field = DimFields.FirstOrDefault() ?? MeasFields.FirstOrDefault() ?? string.Empty;
        return Rows.Select(r => StringValue(r, field)).ToList();
    }

    /// <summary>Extracts numeric values for a measure field across all rows.</summary>
    public List<double?> NumericValues(string measureField) =>
        Rows.Select(r => NumericValue(r, measureField)).ToList();

    /// <summary>Extracts a flat list of (category, value) pairs for pie/donut/treemap.</summary>
    public List<(string Name, double Value)> NameValuePairs(string dimField, string measureField) =>
        Rows.Select(r => (StringValue(r, dimField), NumericValue(r, measureField) ?? 0d)).ToList();

    /// <summary>Returns the scalar sum of a measure column (used by KPI).</summary>
    public double ScalarSum(string measureField) =>
        Rows.Sum(r => NumericValue(r, measureField) ?? 0d);

    /// <summary>Returns the first scalar value of a measure column (used by Gauge).</summary>
    public double ScalarFirst(string measureField) =>
        NumericValue(Rows.FirstOrDefault() ?? new Dictionary<string, object?>(), measureField) ?? 0d;

    // ── Standard ECharts config blocks ────────────────────────────────────────

    /// <summary>Standard axis-style tooltip.</summary>
    public Dictionary<string, object?> AxisTooltip() => new()
    {
        ["trigger"]          = "axis",
        ["backgroundColor"]  = IsDark ? ChartPalette.TooltipBgDark  : ChartPalette.TooltipBgLight,
        ["borderColor"]      = IsDark ? ChartPalette.TooltipBorderDark : ChartPalette.TooltipBorderLight,
        ["borderWidth"]      = 1,
        ["textStyle"]        = new Dictionary<string, object?> { ["color"] = IsDark ? "#FFFFFF" : "#212121" }
    };

    /// <summary>Standard item-style tooltip (used by Pie, Scatter).</summary>
    public Dictionary<string, object?> ItemTooltip() => new()
    {
        ["trigger"]          = "item",
        ["backgroundColor"]  = IsDark ? ChartPalette.TooltipBgDark  : ChartPalette.TooltipBgLight,
        ["borderColor"]      = IsDark ? ChartPalette.TooltipBorderDark : ChartPalette.TooltipBorderLight,
        ["borderWidth"]      = 1,
        ["textStyle"]        = new Dictionary<string, object?> { ["color"] = IsDark ? "#FFFFFF" : "#212121" }
    };

    /// <summary>Standard bottom legend.</summary>
    public Dictionary<string, object?> BottomLegend() => new()
    {
        ["type"]         = "scroll",
        ["bottom"]       = 4,
        ["textStyle"]    = new Dictionary<string, object?> { ["color"] = IsDark ? ChartPalette.AxisColorDark : ChartPalette.AxisColorLight },
        ["pageTextStyle"]= new Dictionary<string, object?> { ["color"] = IsDark ? ChartPalette.AxisColorDark : ChartPalette.AxisColorLight }
    };

    /// <summary>Standard category X-axis.</summary>
    public Dictionary<string, object?> CategoryXAxis(IEnumerable<string> labels) => new()
    {
        ["type"]         = "category",
        ["data"]         = labels.ToArray(),
        ["axisLabel"]    = AxisLabel(),
        ["axisLine"]     = AxisLine(),
        ["splitLine"]    = new Dictionary<string, object?> { ["show"] = false }
    };

    /// <summary>Standard value Y-axis.</summary>
    public Dictionary<string, object?> ValueYAxis(string? name = null) => new()
    {
        ["type"]         = "value",
        ["name"]         = name,
        ["axisLabel"]    = AxisLabel(),
        ["axisLine"]     = AxisLine(),
        ["splitLine"]    = SplitLine()
    };

    /// <summary>Grid insets that leave room for legend and axis labels.</summary>
    public Dictionary<string, object?> DefaultGrid() => new()
    {
        ["top"]    = MeasFields.Count > 1 ? 36 : 16,
        ["right"]  = 20,
        ["bottom"] = 44,
        ["left"]   = 60,
        ["containLabel"] = true
    };

    // ── Private helpers ───────────────────────────────────────────────────────

    private Dictionary<string, object?> AxisLabel() => new()
    {
        ["color"]      = IsDark ? ChartPalette.AxisColorDark : ChartPalette.AxisColorLight,
        ["fontSize"]   = 11
    };

    private Dictionary<string, object?> AxisLine() => new()
    {
        ["lineStyle"]  = new Dictionary<string, object?> { ["color"] = IsDark ? ChartPalette.SplitLineDark : ChartPalette.SplitLineLight }
    };

    private Dictionary<string, object?> SplitLine() => new()
    {
        ["lineStyle"]  = new Dictionary<string, object?> { ["color"] = IsDark ? ChartPalette.SplitLineDark : ChartPalette.SplitLineLight }
    };

    private static string StringValue(IReadOnlyDictionary<string, object?> row, string field) =>
        TryGetRowValue(row, field, out var v) ? v?.ToString() ?? string.Empty : string.Empty;

    private static double? NumericValue(IReadOnlyDictionary<string, object?> row, string field)
    {
        if (!TryGetRowValue(row, field, out var v) || v is null) return null;
        return v is double d ? d
             : double.TryParse(v.ToString(), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
               ? parsed : null;
    }

    /// <summary>
    /// Resolves a row value by the widget's original field name, falling back to the
    /// sanitised DuckDB column name when the reader returned a lower-cased alias.
    /// </summary>
    private static bool TryGetRowValue(IReadOnlyDictionary<string, object?> row, string field, out object? value)
    {
        if (row.TryGetValue(field, out value))
            return true;

        var sanitized = SqlFilterTranslator.SanitiseColumnName(field);
        if (!string.Equals(sanitized, field, StringComparison.Ordinal) && row.TryGetValue(sanitized, out value))
            return true;

        value = null;
        return false;
    }
}
