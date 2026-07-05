using Microsoft.Extensions.Logging.Abstractions;
using NCVizDash.ChartEngine;
using NCVizDash.Models;
using System.Text.Json;
using Xunit;

namespace NCVizDash.Tests.Core;

/// <summary>Unit tests for <see cref="EChartsChartEngine"/> across every <see cref="VisualType"/>.</summary>
public sealed class EChartsChartEngineTests
{
    private static EChartsChartEngine Engine => new(NullLogger<EChartsChartEngine>.Instance);

    private static DashboardWidget MakeWidget(VisualType type, params string[] measureFields)
    {
        var w = new DashboardWidget { Title = "Test Widget", VisualType = type };
        w.DimensionFields.Add("Category");
        foreach (var m in measureFields)
            w.MeasureFields.Add(m);
        return w;
    }

    private static List<IReadOnlyDictionary<string, object?>> MakeRows() =>
    [
        new Dictionary<string, object?> { ["Category"] = "A", ["Revenue"] = 100.0, ["Cost"] = 40.0 },
        new Dictionary<string, object?> { ["Category"] = "B", ["Revenue"] = 200.0, ["Cost"] = 90.0 },
        new Dictionary<string, object?> { ["Category"] = "C", ["Revenue"] = 150.0, ["Cost"] = 60.0 }
    ];

    // ── Envelope shape ────────────────────────────────────────────────────────

    [Fact]
    public void BuildChartOption_Bar_ReturnsEchartsEnvelope()
    {
        var widget = MakeWidget(VisualType.Bar, "Revenue");
        var json = Engine.BuildChartOption(widget, MakeRows());

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("echarts", doc.RootElement.GetProperty("kind").GetString());
        Assert.True(doc.RootElement.TryGetProperty("option", out _));
    }

    [Fact]
    public void BuildChartOption_Kpi_ReturnsHtmlEnvelope()
    {
        var widget = MakeWidget(VisualType.Kpi, "Revenue");
        var json = Engine.BuildChartOption(widget, MakeRows());

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("html", doc.RootElement.GetProperty("kind").GetString());
        Assert.Contains("kpi-value", doc.RootElement.GetProperty("html").GetString());
    }

    [Fact]
    public void BuildChartOption_Table_ReturnsHtmlEnvelope()
    {
        var widget = MakeWidget(VisualType.Table, "Revenue");
        var json = Engine.BuildChartOption(widget, MakeRows());

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("html", doc.RootElement.GetProperty("kind").GetString());
        Assert.Contains("nc-table", doc.RootElement.GetProperty("html").GetString());
    }

    // ── Per-visual-type dispatch ──────────────────────────────────────────────

    [Theory]
    [InlineData(VisualType.Bar)]
    [InlineData(VisualType.Line)]
    [InlineData(VisualType.Area)]
    [InlineData(VisualType.Pie)]
    [InlineData(VisualType.Donut)]
    [InlineData(VisualType.Gauge)]
    [InlineData(VisualType.Radar)]
    [InlineData(VisualType.Scatter)]
    [InlineData(VisualType.Bubble)]
    [InlineData(VisualType.Heatmap)]
    [InlineData(VisualType.Treemap)]
    public void BuildChartOption_EveryChartType_ProducesValidJson_NoException(VisualType type)
    {
        var widget = MakeWidget(type, "Revenue", "Cost");
        var json = Engine.BuildChartOption(widget, MakeRows());

        var exception = Record.Exception(() => JsonDocument.Parse(json));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(VisualType.Bar)]
    [InlineData(VisualType.Line)]
    [InlineData(VisualType.Area)]
    [InlineData(VisualType.Pie)]
    [InlineData(VisualType.Donut)]
    [InlineData(VisualType.Gauge)]
    [InlineData(VisualType.Radar)]
    [InlineData(VisualType.Scatter)]
    [InlineData(VisualType.Bubble)]
    [InlineData(VisualType.Heatmap)]
    [InlineData(VisualType.Treemap)]
    public void BuildChartOption_EveryChartType_IncludesAnimationConfig(VisualType type)
    {
        var widget = MakeWidget(type, "Revenue", "Cost");
        var json = Engine.BuildChartOption(widget, MakeRows());

        using var doc = JsonDocument.Parse(json);
        var option = doc.RootElement.GetProperty("option");

        Assert.True(option.TryGetProperty("animation", out var anim));
        Assert.True(anim.GetBoolean());
        Assert.True(option.TryGetProperty("animationDuration", out _));
    }

    // ── Theme sensitivity ─────────────────────────────────────────────────────

    [Fact]
    public void BuildChartOption_DarkTheme_UsesDarkPaletteColors()
    {
        var widget = MakeWidget(VisualType.Bar, "Revenue");
        var lightJson = Engine.BuildChartOption(widget, MakeRows(), "Light");
        var darkJson = Engine.BuildChartOption(widget, MakeRows(), "Dark");

        Assert.NotEqual(lightJson, darkJson);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public void BuildChartOption_NullWidget_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Engine.BuildChartOption(null!, MakeRows()));
    }

    [Fact]
    public void BuildChartOption_EmptyRows_DoesNotThrow_ReturnsValidEnvelope()
    {
        var widget = MakeWidget(VisualType.Bar, "Revenue");
        var json = Engine.BuildChartOption(widget, []);

        var exception = Record.Exception(() => JsonDocument.Parse(json));
        Assert.Null(exception);
    }

    // ── KPI specifics ─────────────────────────────────────────────────────────

    [Fact]
    public void BuildChartOption_Kpi_SumsAllRows()
    {
        var widget = MakeWidget(VisualType.Kpi, "Revenue");
        var json = Engine.BuildChartOption(widget, MakeRows());

        using var doc = JsonDocument.Parse(json);
        var html = doc.RootElement.GetProperty("html").GetString()!;

        // 100 + 200 + 150 = 450
        Assert.Contains("450", html);
    }

    [Fact]
    public void BuildChartOption_Kpi_MultiRow_ShowsTrendIndicator()
    {
        var widget = MakeWidget(VisualType.Kpi, "Revenue");
        var json = Engine.BuildChartOption(widget, MakeRows());

        using var doc = JsonDocument.Parse(json);
        var html = doc.RootElement.GetProperty("html").GetString()!;

        Assert.Contains("kpi-trend", html);
    }

    // ── Table specifics ───────────────────────────────────────────────────────

    [Fact]
    public void BuildChartOption_Table_IncludesAllRowValues()
    {
        var widget = MakeWidget(VisualType.Table, "Revenue");
        var json = Engine.BuildChartOption(widget, MakeRows());

        using var doc = JsonDocument.Parse(json);
        var html = doc.RootElement.GetProperty("html").GetString()!;

        Assert.Contains("A", html);
        Assert.Contains("B", html);
        Assert.Contains("C", html);
    }
}
