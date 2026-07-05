using NCVizDash.ChartEngine;
using NCVizDash.ChartEngine.Builders;
using NCVizDash.Models;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>
/// Unit tests exercising the individual builder classes directly (bypassing the
/// engine dispatch layer) to verify structural correctness of each chart's option.
/// </summary>
public sealed class ChartBuildersTests
{
    private static DashboardWidget MakeWidget(VisualType type) => new() { Title = "W", VisualType = type };

    private static List<IReadOnlyDictionary<string, object?>> MakeRows() =>
    [
        new Dictionary<string, object?> { ["Dept"] = "Eng", ["Revenue"] = 100.0 },
        new Dictionary<string, object?> { ["Dept"] = "Sales", ["Revenue"] = 200.0 }
    ];

    // ── CartesianBuilder ──────────────────────────────────────────────────────

    [Fact]
    public void BuildBar_ProducesOneSeriesPerMeasure()
    {
        var widget = MakeWidget(VisualType.Bar);
        widget.DimensionFields.Add("Dept");
        widget.MeasureFields.AddRange(["Revenue", "Cost"]);

        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Dept"] = "Eng", ["Revenue"] = 100.0, ["Cost"] = 40.0 }
        };

        var ctx = new ChartOptionContext(widget, rows);
        var option = CartesianBuilder.BuildBar(ctx);

        var series = (object[])option["series"]!;
        Assert.Equal(2, series.Length);
    }

    [Fact]
    public void BuildBar_SingleMeasure_NoLegend()
    {
        var widget = MakeWidget(VisualType.Bar);
        widget.DimensionFields.Add("Dept");
        widget.MeasureFields.Add("Revenue");

        var ctx = new ChartOptionContext(widget, MakeRows());
        var option = CartesianBuilder.BuildBar(ctx);

        Assert.False(option.ContainsKey("legend"));
    }

    [Fact]
    public void BuildLine_UsesSmoothCurves()
    {
        var widget = MakeWidget(VisualType.Line);
        widget.DimensionFields.Add("Dept");
        widget.MeasureFields.Add("Revenue");

        var ctx = new ChartOptionContext(widget, MakeRows());
        var option = CartesianBuilder.BuildLine(ctx);

        var series = (Dictionary<string, object?>)((object[])option["series"]!)[0];
        Assert.Equal(true, series["smooth"]);
    }

    [Fact]
    public void BuildArea_HasGradientAreaStyle()
    {
        var widget = MakeWidget(VisualType.Area);
        widget.DimensionFields.Add("Dept");
        widget.MeasureFields.Add("Revenue");

        var ctx = new ChartOptionContext(widget, MakeRows());
        var option = CartesianBuilder.BuildArea(ctx);

        var series = (Dictionary<string, object?>)((object[])option["series"]!)[0];
        Assert.True(series.ContainsKey("areaStyle"));
    }

    // ── PolarBuilder ──────────────────────────────────────────────────────────

    [Fact]
    public void BuildPie_DataCountMatchesRows()
    {
        var widget = MakeWidget(VisualType.Pie);
        widget.DimensionFields.Add("Dept");
        widget.MeasureFields.Add("Revenue");

        var ctx = new ChartOptionContext(widget, MakeRows());
        var option = PolarBuilder.BuildPie(ctx);

        var series = (Dictionary<string, object?>)((object[])option["series"]!)[0];
        var data = (object[])series["data"]!;
        Assert.Equal(2, data.Length);
    }

    [Fact]
    public void BuildDonut_HasInnerRadius()
    {
        var widget = MakeWidget(VisualType.Donut);
        widget.DimensionFields.Add("Dept");
        widget.MeasureFields.Add("Revenue");

        var ctx = new ChartOptionContext(widget, MakeRows());
        var option = PolarBuilder.BuildDonut(ctx);

        var series = (Dictionary<string, object?>)((object[])option["series"]!)[0];
        var radius = (string[])series["radius"]!;
        Assert.NotEqual("0%", radius[0]); // inner radius > 0 distinguishes donut from pie
    }

    [Fact]
    public void BuildGauge_ClampsValueToZeroToHundred()
    {
        var widget = MakeWidget(VisualType.Gauge);
        widget.MeasureFields.Add("Rate");

        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Rate"] = 150.0 } // out of range
        };

        var ctx = new ChartOptionContext(widget, rows);
        var option = PolarBuilder.BuildGauge(ctx);

        var series = (Dictionary<string, object?>)((object[])option["series"]!)[0];
        var data = (Dictionary<string, object?>[])series["data"]!;
        Assert.Equal(100.0, data[0]["value"]);
    }

    [Fact]
    public void BuildRadar_IndicatorCountMatchesMeasures()
    {
        var widget = MakeWidget(VisualType.Radar);
        widget.MeasureFields.AddRange(["A", "B", "C", "D"]);

        var ctx = new ChartOptionContext(widget, MakeRows());
        var option = PolarBuilder.BuildRadar(ctx);

        var radar = (Dictionary<string, object?>)option["radar"]!;
        var indicators = (Dictionary<string, object?>[])radar["indicator"]!;
        Assert.Equal(4, indicators.Length);
    }

    // ── XyBuilder ─────────────────────────────────────────────────────────────

    [Fact]
    public void BuildScatter_UsesFirstTwoMeasuresAsXY()
    {
        var widget = MakeWidget(VisualType.Scatter);
        widget.MeasureFields.AddRange(["X", "Y"]);

        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["X"] = 1.0, ["Y"] = 2.0 }
        };

        var ctx = new ChartOptionContext(widget, rows);
        var option = XyBuilder.BuildScatter(ctx);

        Assert.True(option.ContainsKey("xAxis"));
        Assert.True(option.ContainsKey("yAxis"));
    }

    [Fact]
    public void BuildBubble_UsesThreeMeasures()
    {
        var widget = MakeWidget(VisualType.Bubble);
        widget.MeasureFields.AddRange(["X", "Y", "Size"]);

        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["X"] = 1.0, ["Y"] = 2.0, ["Size"] = 30.0 }
        };

        var ctx = new ChartOptionContext(widget, rows);
        var option = XyBuilder.BuildBubble(ctx);

        var series = (Dictionary<string, object?>)((object[])option["series"]!)[0];
        var data = (object?[][])series["data"]!;
        Assert.Equal(3, data[0].Length); // x, y, size (name is 4th, optional)
    }

    [Fact]
    public void BuildHeatmap_HasVisualMap()
    {
        var widget = MakeWidget(VisualType.Heatmap);
        widget.DimensionFields.AddRange(["Month", "Product"]);
        widget.MeasureFields.Add("Sales");

        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Month"] = "Jan", ["Product"] = "A", ["Sales"] = 10.0 },
            new Dictionary<string, object?> { ["Month"] = "Feb", ["Product"] = "B", ["Sales"] = 20.0 }
        };

        var ctx = new ChartOptionContext(widget, rows);
        var option = XyBuilder.BuildHeatmap(ctx);

        Assert.True(option.ContainsKey("visualMap"));
    }

    [Fact]
    public void BuildTreemap_AggregatesValuesByDimension()
    {
        var widget = MakeWidget(VisualType.Treemap);
        widget.DimensionFields.Add("Dept");
        widget.MeasureFields.Add("Revenue");

        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Dept"] = "Eng", ["Revenue"] = 100.0 },
            new Dictionary<string, object?> { ["Dept"] = "Eng", ["Revenue"] = 50.0 },
            new Dictionary<string, object?> { ["Dept"] = "Sales", ["Revenue"] = 200.0 }
        };

        var ctx = new ChartOptionContext(widget, rows);
        var option = XyBuilder.BuildTreemap(ctx);

        var series = (Dictionary<string, object?>)((object[])option["series"]!)[0];
        var data = (object[])series["data"]!;
        Assert.Equal(2, data.Length); // Eng, Sales

        var engNode = data.Cast<Dictionary<string, object?>>().First(d => (string)d["name"]! == "Eng");
        Assert.Equal(150.0, engNode["value"]);
    }

    // ── HtmlBuilder ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildKpiHtml_EscapesHtmlInLabel()
    {
        var widget = MakeWidget(VisualType.Kpi);
        widget.MeasureFields.Add("<script>alert(1)</script>");

        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["<script>alert(1)</script>"] = 5.0 }
        };

        var ctx = new ChartOptionContext(widget, rows);
        var html = HtmlBuilder.BuildKpiHtml(ctx);

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void BuildTableHtml_EscapesHtmlInCellValue()
    {
        var widget = MakeWidget(VisualType.Table);
        widget.DimensionFields.Add("Name");

        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["Name"] = "<b>bold</b>" }
        };

        var ctx = new ChartOptionContext(widget, rows);
        var html = HtmlBuilder.BuildTableHtml(ctx);

        Assert.DoesNotContain("<b>bold</b>", html);
        Assert.Contains("&lt;b&gt;", html);
    }

    [Fact]
    public void BuildTableHtml_RowsHaveStaggeredAnimationDelay()
    {
        var widget = MakeWidget(VisualType.Table);
        widget.DimensionFields.Add("Name");

        var rows = Enumerable.Range(0, 5)
            .Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["Name"] = $"Row{i}" })
            .ToList();

        var ctx = new ChartOptionContext(widget, rows);
        var html = HtmlBuilder.BuildTableHtml(ctx);

        Assert.Contains("animation-delay:0ms", html);
        Assert.Contains("animation-delay:20ms", html);
    }
}
