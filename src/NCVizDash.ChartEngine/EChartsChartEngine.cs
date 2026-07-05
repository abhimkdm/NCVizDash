using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCVizDash.ChartEngine.Builders;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;

namespace NCVizDash.ChartEngine;

/// <summary>
/// Generates rendering payloads for every <see cref="VisualType"/>.
/// <para>
/// For chart-shaped visuals (Bar, Line, Area, Pie, Donut, Gauge, Radar, Scatter,
/// Bubble, Heatmap, Treemap) this produces a complete Apache ECharts <c>option</c>
/// object as JSON, with the appropriate <see cref="AnimationPreset"/> merged in.
/// </para>
/// <para>
/// For KPI and Table — which aren't naturally ECharts series — <c>BuildChartOption</c>
/// returns a JSON envelope of the form <c>{ "kind": "html", "html": "..." }</c> so the
/// WebView2 host can tell the two payload kinds apart with a single check.
/// Chart-shaped visuals are returned as <c>{ "kind": "echarts", "option": {...} }</c>.
/// </para>
/// </summary>
public sealed class EChartsChartEngine : IChartEngine
{
    private readonly ILogger<EChartsChartEngine> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Initialises the chart engine.</summary>
    public EChartsChartEngine(ILogger<EChartsChartEngine> logger)
    {
        _logger = logger;
    }

    // ── IChartEngine ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public string BuildChartOption(
        DashboardWidget widget,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> data)
    {
        return BuildChartOption(widget, data, theme: "Light");
    }

    /// <summary>
    /// Theme-aware overload. Produces a fully-themed, fully-animated rendering
    /// payload for the given widget and data.
    /// </summary>
    public string BuildChartOption(
        DashboardWidget widget,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> data,
        string theme)
    {
        if (widget is null) throw new ArgumentNullException(nameof(widget));
        if (data is null) throw new ArgumentNullException(nameof(data));

        var ctx = new ChartOptionContext(widget, data, theme);

        try
        {
            // KPI and Table render as HTML — no ECharts option involved.
            if (widget.VisualType is VisualType.Kpi or VisualType.Table)
            {
                var html = widget.VisualType == VisualType.Kpi
                    ? HtmlBuilder.BuildKpiHtml(ctx)
                    : HtmlBuilder.BuildTableHtml(ctx);

                var htmlEnvelope = new Dictionary<string, object?>
                {
                    ["kind"] = "html",
                    ["html"] = html
                };

                return JsonSerializer.Serialize(htmlEnvelope, JsonOptions);
            }

            var option = BuildEChartsOption(widget.VisualType, ctx);
            MergeAnimationPreset(option, widget.VisualType);

            var envelope = new Dictionary<string, object?>
            {
                ["kind"]   = "echarts",
                ["option"] = option
            };

            return JsonSerializer.Serialize(envelope, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build chart option for widget '{Title}' ({VisualType}).",
                widget.Title, widget.VisualType);

            return JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["kind"]  = "error",
                ["error"] = $"Unable to render {widget.VisualType} for '{widget.Title}': {ex.Message}"
            }, JsonOptions);
        }
    }

    // ── Dispatch ──────────────────────────────────────────────────────────────

    private static Dictionary<string, object?> BuildEChartsOption(VisualType visualType, ChartOptionContext ctx) =>
        visualType switch
        {
            VisualType.Bar     => CartesianBuilder.BuildBar(ctx),
            VisualType.Line    => CartesianBuilder.BuildLine(ctx),
            VisualType.Area    => CartesianBuilder.BuildArea(ctx),
            VisualType.Pie     => PolarBuilder.BuildPie(ctx),
            VisualType.Donut   => PolarBuilder.BuildDonut(ctx),
            VisualType.Gauge   => PolarBuilder.BuildGauge(ctx),
            VisualType.Radar   => PolarBuilder.BuildRadar(ctx),
            VisualType.Scatter => XyBuilder.BuildScatter(ctx),
            VisualType.Bubble  => XyBuilder.BuildBubble(ctx),
            VisualType.Heatmap => XyBuilder.BuildHeatmap(ctx),
            VisualType.Treemap => XyBuilder.BuildTreemap(ctx),
            _ => throw new NotSupportedException($"'{visualType}' has no ECharts builder (should be handled as HTML).")
        };

    /// <summary>Merges the per-visual-type <see cref="AnimationPreset"/> into the top-level option dictionary.</summary>
    private static void MergeAnimationPreset(Dictionary<string, object?> option, VisualType visualType)
    {
        var preset = AnimationPresets.For(visualType);
        foreach (var kvp in AnimationPresets.ToOptionDict(preset))
            option[kvp.Key] = kvp.Value;
    }
}
