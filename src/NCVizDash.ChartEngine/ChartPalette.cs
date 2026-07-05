namespace NCVizDash.ChartEngine;

/// <summary>
/// NC VizDash branded colour palettes for ECharts.
/// Primary hue: DeepPurple (#673AB7). Secondary hue: Teal (#009688).
/// Each palette is 10 colours chosen for accessibility contrast on both
/// light and dark backgrounds.
/// </summary>
public static class ChartPalette
{
    /// <summary>10-colour brand palette for light themes.</summary>
    public static readonly IReadOnlyList<string> Light =
    [
        "#673AB7",  // DeepPurple 500
        "#009688",  // Teal 500
        "#2196F3",  // Blue 500
        "#FF9800",  // Orange 500
        "#E91E63",  // Pink 500
        "#4CAF50",  // Green 500
        "#F44336",  // Red 500
        "#9C27B0",  // Purple 500
        "#00BCD4",  // Cyan 500
        "#FF5722"   // DeepOrange 500
    ];

    /// <summary>10-colour brand palette for dark themes (slightly lighter for contrast).</summary>
    public static readonly IReadOnlyList<string> Dark =
    [
        "#B39DDB",  // DeepPurple 200
        "#80CBC4",  // Teal 200
        "#90CAF9",  // Blue 200
        "#FFCC80",  // Orange 200
        "#F48FB1",  // Pink 200
        "#A5D6A7",  // Green 200
        "#EF9A9A",  // Red 200
        "#CE93D8",  // Purple 200
        "#80DEEA",  // Cyan 200
        "#FFAB91"   // DeepOrange 200
    ];

    // ── Semantic colours ──────────────────────────────────────────────────────

    /// <summary>Brand primary colour for light themes.</summary>
    public const string PrimaryLight  = "#673AB7";

    /// <summary>Brand primary colour for dark themes.</summary>
    public const string PrimaryDark   = "#B39DDB";

    /// <summary>Brand secondary colour for light themes.</summary>
    public const string SecondaryLight = "#009688";

    /// <summary>Brand secondary colour for dark themes.</summary>
    public const string SecondaryDark  = "#80CBC4";

    /// <summary>Colour indicating a positive trend/value on light themes.</summary>
    public const string PositiveLight = "#4CAF50";

    /// <summary>Colour indicating a positive trend/value on dark themes.</summary>
    public const string PositiveDark  = "#A5D6A7";

    /// <summary>Colour indicating a negative trend/value on light themes.</summary>
    public const string NegativeLight = "#F44336";

    /// <summary>Colour indicating a negative trend/value on dark themes.</summary>
    public const string NegativeDark  = "#EF9A9A";

    /// <summary>Neutral/no-change indicator colour on light themes.</summary>
    public const string NeutralLight  = "#9E9E9E";

    /// <summary>Neutral/no-change indicator colour on dark themes.</summary>
    public const string NeutralDark   = "#BDBDBD";

    // ── Axis / grid colours ───────────────────────────────────────────────────

    /// <summary>Axis label/line colour on light themes.</summary>
    public const string AxisColorLight     = "#424242";

    /// <summary>Axis label/line colour on dark themes.</summary>
    public const string AxisColorDark      = "#BDBDBD";

    /// <summary>Grid split-line colour on light themes.</summary>
    public const string SplitLineLight     = "#EEEEEE";

    /// <summary>Grid split-line colour on dark themes.</summary>
    public const string SplitLineDark      = "#424242";

    /// <summary>Chart background colour on light themes.</summary>
    public const string BackgroundLight    = "#FFFFFF";

    /// <summary>Chart background colour on dark themes.</summary>
    public const string BackgroundDark     = "#1E1E1E";

    /// <summary>Tooltip background colour on light themes.</summary>
    public const string TooltipBgLight     = "#FFFFFF";

    /// <summary>Tooltip background colour on dark themes.</summary>
    public const string TooltipBgDark      = "#2D2D2D";

    /// <summary>Tooltip border colour on light themes.</summary>
    public const string TooltipBorderLight = "#E0E0E0";

    /// <summary>Tooltip border colour on dark themes.</summary>
    public const string TooltipBorderDark  = "#424242";

    /// <summary>Returns the primary colour for the given theme name.</summary>
    public static string Primary(string theme) =>
        theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? PrimaryDark : PrimaryLight;

    /// <summary>Returns the 10-colour brand palette for the given theme name.</summary>
    public static IReadOnlyList<string> Palette(string theme) =>
        theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ? Dark : Light;
}
