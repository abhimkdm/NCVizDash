using Microsoft.Extensions.Logging.Abstractions;
using NCVizDash.Models;
using NCVizDash.RuleEngine;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>
/// Comprehensive unit tests for the Phase 5 rule engine:
/// <see cref="FieldComposition"/>, <see cref="RuleRegistry"/>, and
/// <see cref="DeterministicRuleEngine"/>.
/// One test per named rule, plus composition building and auxiliary entry-points.
/// </summary>
public sealed class Phase5RuleEngineTests
{
    // ── Test fixture ──────────────────────────────────────────────────────────

    private static DeterministicRuleEngine Engine =>
        new(NullLogger<DeterministicRuleEngine>.Instance);

    private static FieldDescriptor M(string name) =>
        new() { Name = name, DisplayName = name, FieldType = FieldType.Measure };

    private static FieldDescriptor D(string name) =>
        new() { Name = name, DisplayName = name, FieldType = FieldType.Dimension };

    private static FieldDescriptor T(string name) =>
        new() { Name = name, DisplayName = name, FieldType = FieldType.Time };

    private static FieldDescriptor F(string name) =>
        new() { Name = name, DisplayName = name, FieldType = FieldType.Filter };

    // ── FieldComposition ──────────────────────────────────────────────────────

    [Fact]
    public void FieldComposition_CountsFieldTypes_Correctly()
    {
        var c = FieldComposition.From([M("Revenue"), D("Dept"), T("Date"), F("Active")]);

        Assert.Equal(1, c.Measures);
        Assert.Equal(1, c.Dimensions);
        Assert.Equal(1, c.Times);
        Assert.Equal(1, c.Filters);
        Assert.Equal(4, c.Total);
    }

    [Fact]
    public void FieldComposition_RateHint_DetectedFromMeasureName()
    {
        var c = FieldComposition.From([M("completion_rate")]);
        Assert.True(c.HasRateHint);
    }

    [Fact]
    public void FieldComposition_FinancialHint_DetectedFromMeasureName()
    {
        var c = FieldComposition.From([M("total_revenue")]);
        Assert.True(c.HasFinancialHint);
    }

    [Fact]
    public void FieldComposition_BudgetHint_DetectedFromMeasureName()
    {
        var c = FieldComposition.From([M("budget_allocated")]);
        Assert.True(c.HasBudgetHint);
    }

    [Fact]
    public void FieldComposition_GeoHint_DetectedFromDimensionName()
    {
        var c = FieldComposition.From([M("Sales"), D("region")]);
        Assert.True(c.HasGeoHint);
    }

    [Fact]
    public void FieldComposition_PeopleHint_DetectedFromDimensionName()
    {
        var c = FieldComposition.From([M("Hours"), D("employee_name")]);
        Assert.True(c.HasPeopleHint);
    }

    [Fact]
    public void FieldComposition_NoHints_AllFalse()
    {
        var c = FieldComposition.From([M("Value"), D("Category")]);

        Assert.False(c.HasRateHint);
        Assert.False(c.HasFinancialHint);
        Assert.False(c.HasBudgetHint);
        Assert.False(c.HasGeoHint);
        Assert.False(c.HasPeopleHint);
    }

    // ── Rule: BUBBLE_3M ──────────────────────────────────────────────────────

    [Fact]
    public void Rule_Bubble_ThreeMeasures_NoDimension_NoTime()
    {
        var result = Engine.Recommend([M("X"), M("Y"), M("Size")]);
        Assert.Equal(VisualType.Bubble, result);
    }

    [Fact]
    public void Rule_Bubble_ThreeMeasures_OneDimension()
    {
        var result = Engine.Recommend([M("X"), M("Y"), M("Size"), D("Category")]);
        Assert.Equal(VisualType.Bubble, result);
    }

    // ── Rule: SCATTER_2M ─────────────────────────────────────────────────────

    [Fact]
    public void Rule_Scatter_TwoMeasures_NoDimension_NoTime()
    {
        var result = Engine.Recommend([M("PriceX"), M("ValueY")]);
        Assert.Equal(VisualType.Scatter, result);
    }

    // ── Rule: RADAR_MULTI_M_NO_DIM ───────────────────────────────────────────

    [Fact]
    public void Rule_Radar_FourPlusMeasures_NoDimension()
    {
        var result = Engine.Recommend([M("A"), M("B"), M("C"), M("D")]);
        Assert.Equal(VisualType.Radar, result);
    }

    // ── Rule: HEATMAP_2D_1M ──────────────────────────────────────────────────

    [Fact]
    public void Rule_Heatmap_TwoDimensions_OneMeasure()
    {
        var result = Engine.Recommend([D("Month"), D("Product"), M("Sales")]);
        Assert.Equal(VisualType.Heatmap, result);
    }

    // ── Rule: GAUGE_RATE_1M ──────────────────────────────────────────────────

    [Fact]
    public void Rule_Gauge_SingleRateMeasure()
    {
        var result = Engine.Recommend([M("completion_rate")]);
        Assert.Equal(VisualType.Gauge, result);
    }

    [Fact]
    public void Rule_Gauge_PercentageMeasure()
    {
        var result = Engine.Recommend([M("utilisation_pct")]);
        Assert.Equal(VisualType.Gauge, result);
    }

    // ── Rule: LINE_1T_MULTI_M ────────────────────────────────────────────────

    [Fact]
    public void Rule_Line_MultiMeasures_OneTime()
    {
        var result = Engine.Recommend([T("Month"), M("Revenue"), M("Cost")]);
        Assert.Equal(VisualType.Line, result);
    }

    // ── Rule: LINE_1T_1M ────────────────────────────────────────────────────

    [Fact]
    public void Rule_Line_SingleMeasure_OneTime()
    {
        var result = Engine.Recommend([T("Date"), M("Count")]);
        Assert.Equal(VisualType.Line, result);
    }

    // ── Rule: LINE_1T_1M_1D ─────────────────────────────────────────────────

    [Fact]
    public void Rule_Line_SingleMeasure_OneTime_OneDimension()
    {
        var result = Engine.Recommend([T("Date"), M("Count"), D("Region")]);
        Assert.Equal(VisualType.Line, result);
    }

    // ── Rule: AREA_1T_1M_FINANCIAL ───────────────────────────────────────────

    [Fact]
    public void Rule_Area_FinancialMeasure_OverTime()
    {
        var result = Engine.Recommend([T("Month"), M("total_revenue")]);
        Assert.Equal(VisualType.Area, result);
    }

    // ── Rule: PIE_1M_2D ─────────────────────────────────────────────────────

    [Fact]
    public void Rule_Pie_OneMeasure_TwoDimensions()
    {
        var result = Engine.Recommend([M("Cost"), D("Category"), D("SubCategory")]);
        Assert.Equal(VisualType.Pie, result);
    }

    // ── Rule: DONUT_1M_1D_FINANCIAL ─────────────────────────────────────────

    [Fact]
    public void Rule_Donut_FinancialMeasure_OneDimension()
    {
        var result = Engine.Recommend([M("profit_margin"), D("Department")]);
        Assert.Equal(VisualType.Donut, result);
    }

    // ── Rule: TREEMAP_1M_1D (budget hint) ───────────────────────────────────

    [Fact]
    public void Rule_Treemap_BudgetMeasure_OneDimension()
    {
        var result = Engine.Recommend([M("budget_plan"), D("Team")]);
        Assert.Equal(VisualType.Treemap, result);
    }

    // ── Rule: BAR_1M_1D ─────────────────────────────────────────────────────

    [Fact]
    public void Rule_Bar_SingleMeasure_OneDimension()
    {
        var result = Engine.Recommend([M("Headcount"), D("Department")]);
        Assert.Equal(VisualType.Bar, result);
    }

    // ── Rule: SCATTER_2M_1D ──────────────────────────────────────────────────

    [Fact]
    public void Rule_Scatter_TwoMeasures_OneDimension()
    {
        var result = Engine.Recommend([M("Price"), M("Quantity"), D("Product")]);
        Assert.Equal(VisualType.Scatter, result);
    }

    // ── Rule: BAR_MULTI_M_1D ─────────────────────────────────────────────────

    [Fact]
    public void Rule_Bar_MultiMeasures_OneDimension()
    {
        var result = Engine.Recommend([M("Revenue"), M("Cost"), M("Profit"), D("Region")]);
        Assert.Equal(VisualType.Bar, result);
    }

    // ── Rule: BAR_MULTI_M_NO_DIM ─────────────────────────────────────────────

    [Fact]
    public void Rule_Bar_MultiMeasures_NoDimension()
    {
        var result = Engine.Recommend([M("Revenue"), M("Cost")]);
        Assert.Equal(VisualType.Bar, result);
    }

    // ── Rule: KPI_1M_FINANCIAL ───────────────────────────────────────────────

    [Fact]
    public void Rule_Kpi_SingleFinancialMeasure()
    {
        var result = Engine.Recommend([M("total_revenue")]);
        Assert.Equal(VisualType.Kpi, result);
    }

    // ── Rule: KPI_1M ────────────────────────────────────────────────────────

    [Fact]
    public void Rule_Kpi_SingleMeasure_NoDimension_NoTime()
    {
        var result = Engine.Recommend([M("Headcount")]);
        Assert.Equal(VisualType.Kpi, result);
    }

    // ── Rule: TABLE_FALLBACK ─────────────────────────────────────────────────

    [Fact]
    public void Rule_Table_EmptyFieldList_ReturnsFallback()
    {
        var result = Engine.Recommend([]);
        Assert.Equal(VisualType.Table, result);
    }

    [Fact]
    public void Rule_Table_FiltersOnly_ReturnsFallback()
    {
        var result = Engine.Recommend([F("IsActive"), F("IsCompleted")]);
        Assert.Equal(VisualType.Table, result);
    }

    // ── RecommendWithExplanation ─────────────────────────────────────────────

    [Fact]
    public void RecommendWithExplanation_ReturnsRuleName_And_NonEmptyExplanation()
    {
        var (visual, ruleName, explanation) =
            Engine.RecommendWithExplanation([M("Revenue")]);

        Assert.Equal(VisualType.Kpi, visual);
        Assert.False(string.IsNullOrWhiteSpace(ruleName));
        Assert.False(string.IsNullOrWhiteSpace(explanation));
    }

    [Fact]
    public void RecommendWithExplanation_GaugeRule_ContainsProgressText()
    {
        var (_, _, explanation) =
            Engine.RecommendWithExplanation([M("completion_rate")]);

        Assert.Contains("Gauge", explanation);
    }

    // ── AllMatches ────────────────────────────────────────────────────────────

    [Fact]
    public void AllMatches_AlwaysIncludesTableFallback()
    {
        var matches = Engine.AllMatches([M("Revenue")]);

        Assert.Contains(matches, m => m.Visual == VisualType.Table);
    }

    [Fact]
    public void AllMatches_FirstItemIsTopRecommendation()
    {
        var recommended = Engine.Recommend([M("Revenue")]);
        var matches = Engine.AllMatches([M("Revenue")]);

        Assert.Equal(recommended, matches[0].Visual);
    }

    [Fact]
    public void AllMatches_MultiMeasureOneTime_IncludesLineAndArea()
    {
        var fields = new[] { T("Month"), M("total_revenue"), M("Cost") };
        var matches = Engine.AllMatches(fields);
        var visuals = matches.Select(m => m.Visual).ToList();

        Assert.Contains(VisualType.Line, visuals);
        Assert.Contains(VisualType.Area, visuals);
    }

    // ── RuleRegistry ordering ─────────────────────────────────────────────────

    [Fact]
    public void RuleRegistry_AllRules_AreSortedByPriorityAscending()
    {
        var priorities = RuleRegistry.All.Select(r => r.Priority).ToList();
        Assert.Equal(priorities, priorities.OrderBy(p => p).ToList());
    }

    [Fact]
    public void RuleRegistry_LastRule_IsTableFallback()
    {
        var last = RuleRegistry.All[RuleRegistry.All.Count - 1];
        Assert.Equal("TABLE_FALLBACK", last.Name);
        Assert.Equal(VisualType.Table, last.RecommendedVisual);
    }

    [Fact]
    public void RuleRegistry_AllRuleNames_AreUnique()
    {
        var names = RuleRegistry.All.Select(r => r.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}
