using NCVizDash.Models;

namespace NCVizDash.RuleEngine;

/// <summary>
/// The complete, ordered registry of deterministic visualization rules.
/// Rules are evaluated in ascending priority order; the first match wins.
/// No AI, no configuration wizard, no randomness.
///
/// Rule authoring conventions:
///   Priority 100-199  — Highly specific combinations (Bubble, Radar, Heatmap, Gauge)
///   Priority 200-299  — Time-series patterns (Line, Area, multi-series Line)
///   Priority 300-399  — Categorical single-measure (Bar, Pie, Donut, Treemap)
///   Priority 400-499  — Multi-measure comparison (Grouped Bar, Scatter)
///   Priority 500-599  — Single-measure KPI variants (Gauge, KPI)
///   Priority 900      — Catch-all Table fallback
/// </summary>
public static class RuleRegistry
{
    /// <summary>All rules, sorted ascending by priority (lowest number = checked first).</summary>
    public static IReadOnlyList<VisualizationRule> All { get; } = BuildRules()
        .OrderBy(r => r.Priority)
        .ToList()
        .AsReadOnly();

    private static IEnumerable<VisualizationRule> BuildRules()
    {
        // ── Priority 100: Highly-specific multi-field combinations ─────────────

        yield return new VisualizationRule(
            name: "BUBBLE_3M",
            priority: 100,
            recommendedVisual: VisualType.Bubble,
            explanation: "3 measures selected — X position, Y position and bubble size map naturally to a Bubble chart.",
            predicate: c => c.Measures == 3 && c.Dimensions <= 1 && c.Times == 0);

        yield return new VisualizationRule(
            name: "SCATTER_2M",
            priority: 110,
            recommendedVisual: VisualType.Scatter,
            explanation: "2 measures selected with no time axis — X/Y Scatter chart shows correlation.",
            predicate: c => c.Measures == 2 && c.Dimensions == 0 && c.Times == 0);

        yield return new VisualizationRule(
            name: "RADAR_MULTI_M_NO_DIM",
            priority: 120,
            recommendedVisual: VisualType.Radar,
            explanation: "Multiple measures with no dimension — Radar chart compares values across axes.",
            predicate: c => c.Measures >= 4 && c.Dimensions == 0 && c.Times == 0);

        yield return new VisualizationRule(
            name: "HEATMAP_2D_1M",
            priority: 130,
            recommendedVisual: VisualType.Heatmap,
            explanation: "2 dimensions + 1 measure — rows and columns of a Heatmap encode the two categories; colour encodes the value.",
            predicate: c => c.Dimensions == 2 && c.Measures == 1 && c.Times == 0);

        yield return new VisualizationRule(
            name: "TREEMAP_2D_1M_FINANCIAL",
            priority: 140,
            recommendedVisual: VisualType.Treemap,
            explanation: "2 dimensions + 1 financial measure — hierarchy + size encoding in a Treemap communicates proportional contribution.",
            predicate: c => c.Dimensions == 2 && c.Measures == 1 && c.Times == 0 && c.HasFinancialHint);

        yield return new VisualizationRule(
            name: "GAUGE_RATE_1M",
            priority: 150,
            recommendedVisual: VisualType.Gauge,
            explanation: "Single rate/percentage measure — Gauge shows progress toward 100%.",
            predicate: c => c.Measures == 1 && c.Dimensions == 0 && c.Times == 0 && c.HasRateHint);

        // ── Priority 200: Time-series ──────────────────────────────────────────

        yield return new VisualizationRule(
            name: "LINE_1T_MULTI_M",
            priority: 200,
            recommendedVisual: VisualType.Line,
            explanation: "Multiple measures over time — multi-series Line chart shows trends and relative movements.",
            predicate: c => c.Times == 1 && c.Measures >= 2 && c.Dimensions == 0);

        yield return new VisualizationRule(
            name: "LINE_1T_1M",
            priority: 210,
            recommendedVisual: VisualType.Line,
            explanation: "Single measure over time — Line chart is the standard for showing a trend.",
            predicate: c => c.Times == 1 && c.Measures == 1 && c.Dimensions == 0);

        yield return new VisualizationRule(
            name: "LINE_1T_1M_1D",
            priority: 220,
            recommendedVisual: VisualType.Line,
            explanation: "Measure over time, split by a dimension — multi-series Line chart.",
            predicate: c => c.Times == 1 && c.Measures == 1 && c.Dimensions == 1);

        yield return new VisualizationRule(
            name: "AREA_1T_1M_FINANCIAL",
            priority: 225,
            recommendedVisual: VisualType.Area,
            explanation: "Financial measure over time — filled Area chart emphasises cumulative volume.",
            predicate: c => c.Times == 1 && c.Measures == 1 && c.Dimensions == 0 && c.HasFinancialHint);

        yield return new VisualizationRule(
            name: "AREA_1T_MULTI_M",
            priority: 230,
            recommendedVisual: VisualType.Area,
            explanation: "Multiple measures over time — stacked Area chart shows composition and trend together.",
            predicate: c => c.Times == 1 && c.Measures >= 2 && c.HasFinancialHint);

        // ── Priority 300: Single-measure categorical ───────────────────────────

        yield return new VisualizationRule(
            name: "PIE_1M_2D",
            priority: 300,
            recommendedVisual: VisualType.Pie,
            explanation: "1 measure + 2 dimensions — nested Pie (or Donut) encodes part-of-whole across two category levels.",
            predicate: c => c.Measures == 1 && c.Dimensions == 2 && c.Times == 0);

        yield return new VisualizationRule(
            name: "DONUT_1M_1D_FINANCIAL",
            priority: 310,
            recommendedVisual: VisualType.Donut,
            explanation: "Financial measure split by one category — Donut chart with a centre total is a common finance layout.",
            predicate: c => c.Measures == 1 && c.Dimensions == 1 && c.Times == 0 && c.HasFinancialHint);

        yield return new VisualizationRule(
            name: "TREEMAP_1M_1D",
            priority: 320,
            recommendedVisual: VisualType.Treemap,
            explanation: "Single measure with one hierarchical dimension — Treemap encodes size as area.",
            predicate: c => c.Measures == 1 && c.Dimensions == 1 && c.Times == 0 && c.HasBudgetHint);

        yield return new VisualizationRule(
            name: "BAR_1M_1D",
            priority: 340,
            recommendedVisual: VisualType.Bar,
            explanation: "Single measure across one dimension — Bar chart is the clearest comparison of discrete categories.",
            predicate: c => c.Measures == 1 && c.Dimensions == 1 && c.Times == 0);

        yield return new VisualizationRule(
            name: "BAR_1M_1D_GEO",
            priority: 330,
            recommendedVisual: VisualType.Bar,
            explanation: "Measure by geographic dimension — horizontal Bar chart handles long region names well.",
            predicate: c => c.Measures == 1 && c.Dimensions == 1 && c.Times == 0 && c.HasGeoHint);

        yield return new VisualizationRule(
            name: "BAR_1M_1D_PEOPLE",
            priority: 335,
            recommendedVisual: VisualType.Bar,
            explanation: "Measure by people/team dimension — horizontal Bar chart for leaderboard-style comparison.",
            predicate: c => c.Measures == 1 && c.Dimensions == 1 && c.Times == 0 && c.HasPeopleHint);

        // ── Priority 400: Multi-measure comparison ────────────────────────────

        yield return new VisualizationRule(
            name: "SCATTER_2M_1D",
            priority: 400,
            recommendedVisual: VisualType.Scatter,
            explanation: "2 measures + 1 category dimension — coloured Scatter chart shows per-category X/Y correlation.",
            predicate: c => c.Measures == 2 && c.Dimensions == 1 && c.Times == 0);

        yield return new VisualizationRule(
            name: "BAR_MULTI_M_1D",
            priority: 410,
            recommendedVisual: VisualType.Bar,
            explanation: "Multiple measures across one dimension — grouped Bar chart compares measures side-by-side.",
            predicate: c => c.Measures >= 2 && c.Dimensions == 1 && c.Times == 0);

        yield return new VisualizationRule(
            name: "BAR_MULTI_M_NO_DIM",
            priority: 420,
            recommendedVisual: VisualType.Bar,
            explanation: "Multiple measures, no dimension — Bar chart with one bar per measure for direct comparison.",
            predicate: c => c.Measures >= 2 && c.Dimensions == 0 && c.Times == 0);

        yield return new VisualizationRule(
            name: "RADAR_MULTI_M_1D",
            priority: 430,
            recommendedVisual: VisualType.Radar,
            explanation: "Multiple measures with one category — Radar chart shows multi-dimensional profile per category value.",
            predicate: c => c.Measures >= 3 && c.Dimensions == 1 && c.Times == 0);

        // ── Priority 500: KPI ─────────────────────────────────────────────────

        yield return new VisualizationRule(
            name: "KPI_1M_FINANCIAL",
            priority: 500,
            recommendedVisual: VisualType.Kpi,
            explanation: "Single financial measure, no axes — KPI card shows the headline number.",
            predicate: c => c.Measures == 1 && c.Dimensions == 0 && c.Times == 0 && c.HasFinancialHint);

        yield return new VisualizationRule(
            name: "KPI_1M",
            priority: 510,
            recommendedVisual: VisualType.Kpi,
            explanation: "Single measure, no grouping or time axis — KPI card.",
            predicate: c => c.Measures == 1 && c.Dimensions == 0 && c.Times == 0);

        // ── Priority 900: Catch-all ───────────────────────────────────────────

        yield return new VisualizationRule(
            name: "TABLE_FALLBACK",
            priority: 900,
            recommendedVisual: VisualType.Table,
            explanation: "No specific pattern matched — Table shows all selected fields in a grid.",
            predicate: _ => true);
    }
}
