using NCVizDash.Models;

namespace NCVizDash.ChartEngine;

/// <summary>
/// Per-visual-type ECharts animation configuration.
/// Each preset is merged into the final option object before serialisation.
/// All durations in milliseconds.
/// </summary>
public sealed class AnimationPreset
{
    public bool   Animation               { get; init; } = true;
    public int    AnimationDuration       { get; init; } = 800;
    public string AnimationEasing         { get; init; } = "cubicOut";
    public int    AnimationDurationUpdate { get; init; } = 500;
    public string AnimationEasingUpdate   { get; init; } = "cubicInOut";
    public int    AnimationThreshold      { get; init; } = 2000;
    public int    AnimationDelay          { get; init; } = 0;
}

/// <summary>Factory that returns the appropriate animation preset for each visual type.</summary>
public static class AnimationPresets
{
    private static readonly AnimationPreset Default = new();

    /// <summary>Returns the animation preset for <paramref name="visualType"/>.</summary>
    public static AnimationPreset For(VisualType visualType) => visualType switch
    {
        // Gauge: elastic snap feels satisfying for a progress indicator.
        VisualType.Gauge => new AnimationPreset
        {
            AnimationDuration = 1200,
            AnimationEasing = "elasticOut",
            AnimationDurationUpdate = 700,
            AnimationEasingUpdate = "elasticOut"
        },

        // Pie / Donut: staggered entry with a slight delay per slice.
        VisualType.Pie or VisualType.Donut => new AnimationPreset
        {
            AnimationDuration = 1000,
            AnimationEasing = "cubicOut",
            AnimationDurationUpdate = 600,
            AnimationEasingUpdate = "cubicInOut",
            AnimationDelay = 80   // each series item staggered by 80ms
        },

        // Line / Area: fast left-to-right draw.
        VisualType.Line or VisualType.Area => new AnimationPreset
        {
            AnimationDuration = 600,
            AnimationEasing = "linear",
            AnimationDurationUpdate = 400,
            AnimationEasingUpdate = "linear"
        },

        // Bar: bounce gives energy to comparisons.
        VisualType.Bar => new AnimationPreset
        {
            AnimationDuration = 700,
            AnimationEasing = "bounceOut",
            AnimationDurationUpdate = 400,
            AnimationEasingUpdate = "cubicInOut"
        },

        // Treemap: smooth morphing between hierarchy levels.
        VisualType.Treemap => new AnimationPreset
        {
            AnimationDuration = 800,
            AnimationEasing = "cubicInOut",
            AnimationDurationUpdate = 600,
            AnimationEasingUpdate = "cubicInOut"
        },

        // Radar: circular unfurl.
        VisualType.Radar => new AnimationPreset
        {
            AnimationDuration = 700,
            AnimationEasing = "cubicOut",
            AnimationDurationUpdate = 400,
            AnimationEasingUpdate = "cubicOut"
        },

        // Scatter / Bubble: quick fade-in so points don't distract from the correlation.
        VisualType.Scatter or VisualType.Bubble => new AnimationPreset
        {
            AnimationDuration = 500,
            AnimationEasing = "cubicOut",
            AnimationDurationUpdate = 300,
            AnimationEasingUpdate = "cubicOut"
        },

        // Heatmap: fast fill.
        VisualType.Heatmap => new AnimationPreset
        {
            AnimationDuration = 600,
            AnimationEasing = "cubicOut",
            AnimationDurationUpdate = 400,
            AnimationEasingUpdate = "cubicInOut"
        },

        // KPI and Table are rendered as HTML, not ECharts — animation config
        // is consumed by the HTML host's CSS animations instead.
        VisualType.Kpi or VisualType.Table => Default,

        _ => Default
    };

    /// <summary>Converts a preset to a flat dictionary for merging into ECharts option JSON.</summary>
    public static Dictionary<string, object?> ToOptionDict(AnimationPreset p) => new()
    {
        ["animation"]               = p.Animation,
        ["animationDuration"]       = p.AnimationDuration,
        ["animationEasing"]         = p.AnimationEasing,
        ["animationDurationUpdate"] = p.AnimationDurationUpdate,
        ["animationEasingUpdate"]   = p.AnimationEasingUpdate,
        ["animationThreshold"]      = p.AnimationThreshold
    };
}
