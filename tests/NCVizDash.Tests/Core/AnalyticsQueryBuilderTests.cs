using NCVizDash.Core.Analytics;
using NCVizDash.DuckDB;
using NCVizDash.Models;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>
/// Comprehensive unit tests for <see cref="AnalyticsQueryBuilder"/> — every
/// aggregation function, every <see cref="FilterOperator"/>, sorting, Top N,
/// every window function, and pivot query generation.
/// </summary>
public sealed class AnalyticsQueryBuilderTests
{
    // ── Basic aggregate query shape ──────────────────────────────────────────

    [Fact]
    public void Build_SimpleGroupBy_ProducesExpectedShape()
    {
        var spec = new QuerySpec
        {
            TableName = "sales",
            Dimensions = ["department"],
            Measures = [new MeasureSpec { Field = "revenue", Aggregate = AggregateFunction.Sum }]
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("SELECT \"department\", SUM(\"revenue\") AS \"revenue\"", sql);
        Assert.Contains("FROM \"sales\"", sql);
        Assert.Contains("GROUP BY \"department\"", sql);
        Assert.EndsWith(";", sql);
    }

    [Theory]
    [InlineData(AggregateFunction.Sum, "SUM(")]
    [InlineData(AggregateFunction.Count, "COUNT(")]
    [InlineData(AggregateFunction.CountDistinct, "COUNT(DISTINCT")]
    [InlineData(AggregateFunction.Avg, "AVG(")]
    [InlineData(AggregateFunction.Min, "MIN(")]
    [InlineData(AggregateFunction.Max, "MAX(")]
    public void Build_EveryAggregateFunction_ProducesCorrectSqlFunction(AggregateFunction agg, string expectedSqlFragment)
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["cat"],
            Measures = [new MeasureSpec { Field = "val", Aggregate = agg }]
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains(expectedSqlFragment, sql);
    }

    [Fact]
    public void Build_NoDimensions_NoGroupBy()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Measures = [new MeasureSpec { Field = "val", Aggregate = AggregateFunction.Sum }]
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.DoesNotContain("GROUP BY", sql);
    }

    [Fact]
    public void Build_RawMeasures_NoAggregation_NoGroupBy()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["cat"],
            Measures =
            [
                new MeasureSpec { Field = "x", Aggregate = AggregateFunction.None },
                new MeasureSpec { Field = "y", Aggregate = AggregateFunction.None }
            ]
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.DoesNotContain("GROUP BY", sql);
        Assert.DoesNotContain("SUM(", sql);
        Assert.Contains("\"x\"", sql);
        Assert.Contains("\"y\"", sql);
    }

    [Fact]
    public void Build_MultipleDimensions_GroupsByAll()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["region", "product"],
            Measures = [new MeasureSpec { Field = "revenue" }]
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("GROUP BY \"region\", \"product\"", sql);
    }

    // ── Sorting ───────────────────────────────────────────────────────────────

    [Fact]
    public void Build_ExplicitSortField_UsedOverFirstDimension()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["cat"],
            Measures = [new MeasureSpec { Field = "val" }],
            SortField = "val",
            SortDescending = true
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("ORDER BY \"val\" DESC", sql);
    }

    [Fact]
    public void Build_NoSortField_DefaultsToFirstDimension()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["cat"],
            Measures = [new MeasureSpec { Field = "val" }]
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("ORDER BY \"cat\" ASC", sql);
    }

    // ── Top N / limit ─────────────────────────────────────────────────────────

    [Fact]
    public void Build_ExplicitLimit_Applied()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["cat"],
            Measures = [new MeasureSpec { Field = "val" }],
            Limit = 10
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("LIMIT 10", sql);
    }

    [Fact]
    public void Build_NoLimit_AppliesSafetyCap()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["cat"],
            Measures = [new MeasureSpec { Field = "val" }]
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("LIMIT 5000", sql);
    }

    [Fact]
    public void Build_LimitAboveSafetyCap_ClampedToCap()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["cat"],
            Measures = [new MeasureSpec { Field = "val" }],
            Limit = 999999
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("LIMIT 5000", sql);
        Assert.DoesNotContain("LIMIT 999999", sql);
    }

    // ── Filters — every operator ─────────────────────────────────────────────

    [Fact]
    public void Build_EqualsFilter_ProducesEqualityClause()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "region", Operator = FilterOperator.Equals, Values = ["EMEA"] });
        Assert.Contains("\"region\" = 'EMEA'", sql);
    }

    [Fact]
    public void Build_NotEqualsFilter_ProducesInequalityClause()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "region", Operator = FilterOperator.NotEquals, Values = ["EMEA"] });
        Assert.Contains("\"region\" <> 'EMEA'", sql);
    }

    [Fact]
    public void Build_GreaterThanFilter_NumericLiteral_NotQuoted()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "revenue", Operator = FilterOperator.GreaterThan, Values = ["1000"] });
        Assert.Contains("\"revenue\" > 1000", sql);
    }

    [Fact]
    public void Build_GreaterThanOrEqualFilter()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "revenue", Operator = FilterOperator.GreaterThanOrEqual, Values = ["500"] });
        Assert.Contains("\"revenue\" >= 500", sql);
    }

    [Fact]
    public void Build_LessThanFilter()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "revenue", Operator = FilterOperator.LessThan, Values = ["500"] });
        Assert.Contains("\"revenue\" < 500", sql);
    }

    [Fact]
    public void Build_LessThanOrEqualFilter()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "revenue", Operator = FilterOperator.LessThanOrEqual, Values = ["500"] });
        Assert.Contains("\"revenue\" <= 500", sql);
    }

    [Fact]
    public void Build_ContainsFilter_UsesIlikeWithWildcards()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "name", Operator = FilterOperator.Contains, Values = ["acme"] });
        Assert.Contains("\"name\" ILIKE '%acme%'", sql);
    }

    [Fact]
    public void Build_InFilter_ProducesInClause()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "region", Operator = FilterOperator.In, Values = ["EMEA", "APAC"] });
        Assert.Contains("\"region\" IN ('EMEA', 'APAC')", sql);
    }

    [Fact]
    public void Build_NotInFilter_ProducesNotInClause()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "region", Operator = FilterOperator.NotIn, Values = ["EMEA"] });
        Assert.Contains("\"region\" NOT IN ('EMEA')", sql);
    }

    [Fact]
    public void Build_BetweenFilter_ProducesBetweenClause()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "revenue", Operator = FilterOperator.Between, Values = ["100", "500"] });
        Assert.Contains("\"revenue\" BETWEEN 100 AND 500", sql);
    }

    [Fact]
    public void Build_BetweenFilter_MissingSecondValue_Skipped()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "revenue", Operator = FilterOperator.Between, Values = ["100"] });
        Assert.DoesNotContain("WHERE", sql);
    }

    [Fact]
    public void Build_DisabledFilter_Excluded()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "region", Operator = FilterOperator.Equals, Values = ["EMEA"], IsEnabled = false });
        Assert.DoesNotContain("WHERE", sql);
    }

    [Fact]
    public void Build_MultipleFilters_JoinedWithAnd()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["cat"],
            Measures = [new MeasureSpec { Field = "val" }],
            Filters =
            [
                new WidgetFilter { FieldName = "region", Operator = FilterOperator.Equals, Values = ["EMEA"] },
                new WidgetFilter { FieldName = "revenue", Operator = FilterOperator.GreaterThan, Values = ["100"] }
            ]
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("WHERE \"region\" = 'EMEA' AND \"revenue\" > 100", sql);
    }

    [Fact]
    public void Build_FilterValueWithSingleQuote_Escaped()
    {
        var sql = BuildWithFilter(new WidgetFilter { FieldName = "name", Operator = FilterOperator.Equals, Values = ["O'Brien"] });
        Assert.Contains("'O''Brien'", sql);
    }

    // ── Window functions ──────────────────────────────────────────────────────

    [Fact]
    public void Build_RowNumberWindow_ProducesRowNumberOver()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["cat"],
            Measures = [new MeasureSpec { Field = "val", Aggregate = AggregateFunction.None }],
            WindowFunction = new WindowFunctionSpec { Type = WindowFunctionType.RowNumber, OrderByField = "val" }
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("ROW_NUMBER() OVER (ORDER BY \"val\")", sql);
    }

    [Fact]
    public void Build_RankWindow_ProducesRankOverDescending()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Measures = [new MeasureSpec { Field = "val", Aggregate = AggregateFunction.None }],
            WindowFunction = new WindowFunctionSpec { Type = WindowFunctionType.Rank, OrderByField = "val" }
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("RANK() OVER (ORDER BY \"val\" DESC)", sql);
    }

    [Fact]
    public void Build_RunningTotalWindow_ProducesUnboundedPreceding()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Measures = [new MeasureSpec { Field = "val", Aggregate = AggregateFunction.None }],
            WindowFunction = new WindowFunctionSpec { Type = WindowFunctionType.RunningTotal, OrderByField = "val" }
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW", sql);
    }

    [Fact]
    public void Build_MovingAverageWindow_UsesConfiguredWindowSize()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Measures = [new MeasureSpec { Field = "val", Aggregate = AggregateFunction.None }],
            WindowFunction = new WindowFunctionSpec { Type = WindowFunctionType.MovingAverage, OrderByField = "val", WindowSize = 5 }
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("ROWS BETWEEN 4 PRECEDING AND CURRENT ROW", sql);
    }

    [Fact]
    public void Build_PercentOfTotalWindow_ProducesDivisionExpression()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Measures = [new MeasureSpec { Field = "val", Aggregate = AggregateFunction.None }],
            WindowFunction = new WindowFunctionSpec { Type = WindowFunctionType.PercentOfTotal, OrderByField = "val" }
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("/ SUM(\"val\") OVER", sql);
    }

    [Fact]
    public void Build_WindowFunction_WithPartitionBy_IncludesPartitionClause()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Measures = [new MeasureSpec { Field = "val", Aggregate = AggregateFunction.None }],
            WindowFunction = new WindowFunctionSpec
            {
                Type = WindowFunctionType.RowNumber,
                OrderByField = "val",
                PartitionByFields = ["department"]
            }
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("PARTITION BY \"department\" ORDER BY", sql);
    }

    [Fact]
    public void Build_WindowFunction_UsesConfiguredAlias()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Measures = [new MeasureSpec { Field = "val", Aggregate = AggregateFunction.None }],
            WindowFunction = new WindowFunctionSpec { Type = WindowFunctionType.RowNumber, OrderByField = "val", Alias = "rn" }
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("AS \"rn\"", sql);
    }

    // ── Pivot ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_PivotSpec_ProducesPivotStatement()
    {
        var spec = new QuerySpec
        {
            TableName = "sales",
            Dimensions = ["region"],
            Pivot = new PivotSpec { PivotField = "quarter", ValueField = "revenue", Aggregate = AggregateFunction.Sum }
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.StartsWith("PIVOT \"sales\"", sql);
        Assert.Contains("ON \"quarter\"", sql);
        Assert.Contains("USING sum(\"revenue\")", sql);
        Assert.Contains("GROUP BY \"region\"", sql);
    }

    [Fact]
    public void Build_PivotSpec_CountAggregate_UsesCountStar()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Pivot = new PivotSpec { PivotField = "status", ValueField = "id", Aggregate = AggregateFunction.Count }
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("USING count(*)", sql);
    }

    // ── Column name sanitisation ──────────────────────────────────────────────

    [Fact]
    public void Build_FieldNamesWithSpacesAndSymbols_Sanitised()
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["Sub Category!"],
            Measures = [new MeasureSpec { Field = "Total $ Revenue" }]
        };

        var sql = AnalyticsQueryBuilder.Build(spec);

        Assert.Contains("\"sub_category_\"", sql);
        Assert.Contains("\"total___revenue\"", sql);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public void Build_NullSpec_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AnalyticsQueryBuilder.Build(null!));
    }

    [Fact]
    public void Build_EmptyTableName_ThrowsArgumentException()
    {
        var spec = new QuerySpec { TableName = "" };
        Assert.Throws<ArgumentException>(() => AnalyticsQueryBuilder.Build(spec));
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static string BuildWithFilter(WidgetFilter filter)
    {
        var spec = new QuerySpec
        {
            TableName = "t",
            Dimensions = ["cat"],
            Measures = [new MeasureSpec { Field = "val" }],
            Filters = [filter]
        };

        return AnalyticsQueryBuilder.Build(spec);
    }
}
