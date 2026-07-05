namespace NCVizDash.ChartEngine.Builders;

/// <summary>Builds ECharts options for Bar, Line, and Area charts.</summary>
public static class CartesianBuilder
{
    // ── Bar ───────────────────────────────────────────────────────────────────

    public static Dictionary<string, object?> BuildBar(ChartOptionContext ctx)
    {
        var categories = ctx.CategoryLabels();
        var series     = ctx.MeasFields.Select((field, i) => new Dictionary<string, object?>
        {
            ["type"]      = "bar",
            ["name"]      = field,
            ["data"]      = ctx.NumericValues(field).Cast<object?>().ToArray(),
            ["itemStyle"] = new Dictionary<string, object?> { ["color"] = ctx.Palette[i % ctx.Palette.Count] },
            ["emphasis"]  = Emphasis(),
            ["barMaxWidth"] = 60,
            ["label"]     = new Dictionary<string, object?> { ["show"] = false }
        }).ToList();

        return BaseCartesian(ctx, categories, series);
    }

    // ── Line ──────────────────────────────────────────────────────────────────

    public static Dictionary<string, object?> BuildLine(ChartOptionContext ctx)
    {
        var categories = ctx.CategoryLabels();
        var series     = ctx.MeasFields.Select((field, i) => new Dictionary<string, object?>
        {
            ["type"]        = "line",
            ["name"]        = field,
            ["data"]        = ctx.NumericValues(field).Cast<object?>().ToArray(),
            ["smooth"]      = true,
            ["symbol"]      = "circle",
            ["symbolSize"]  = 6,
            ["lineStyle"]   = new Dictionary<string, object?> { ["width"] = 2.5, ["color"] = ctx.Palette[i % ctx.Palette.Count] },
            ["itemStyle"]   = new Dictionary<string, object?> { ["color"] = ctx.Palette[i % ctx.Palette.Count] },
            ["emphasis"]    = Emphasis()
        }).ToList();

        return BaseCartesian(ctx, categories, series);
    }

    // ── Area ─────────────────────────────────────────────────────────────────

    public static Dictionary<string, object?> BuildArea(ChartOptionContext ctx)
    {
        var categories = ctx.CategoryLabels();
        var series     = ctx.MeasFields.Select((field, i) =>
        {
            var color = ctx.Palette[i % ctx.Palette.Count];
            return new Dictionary<string, object?>
            {
                ["type"]       = "line",
                ["name"]       = field,
                ["data"]       = ctx.NumericValues(field).Cast<object?>().ToArray(),
                ["smooth"]     = true,
                ["symbol"]     = "none",
                ["lineStyle"]  = new Dictionary<string, object?> { ["width"] = 2, ["color"] = color },
                ["itemStyle"]  = new Dictionary<string, object?> { ["color"] = color },
                ["areaStyle"]  = new Dictionary<string, object?>
                {
                    ["color"] = new Dictionary<string, object?>
                    {
                        ["type"]       = "linear",
                        ["x"]          = 0, ["y"] = 0, ["x2"] = 0, ["y2"] = 1,
                        ["colorStops"] = new[]
                        {
                            new Dictionary<string, object?> { ["offset"] = 0, ["color"] = $"{color}88" },
                            new Dictionary<string, object?> { ["offset"] = 1, ["color"] = $"{color}08" }
                        }
                    }
                },
                ["emphasis"]   = Emphasis(),
                ["stack"]      = ctx.MeasFields.Count > 1 ? "total" : null
            };
        }).ToList();

        return BaseCartesian(ctx, categories, series);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static Dictionary<string, object?> BaseCartesian(
        ChartOptionContext ctx,
        List<string> categories,
        List<Dictionary<string, object?>> series)
    {
        var option = new Dictionary<string, object?>
        {
            ["backgroundColor"] = "transparent",
            ["color"]       = ctx.Palette.ToArray(),
            ["tooltip"]     = ctx.AxisTooltip(),
            ["grid"]        = ctx.DefaultGrid(),
            ["xAxis"]       = ctx.CategoryXAxis(categories),
            ["yAxis"]       = ctx.ValueYAxis(ctx.MeasFields.FirstOrDefault()),
            ["series"]      = series.Cast<object?>().ToArray()
        };

        if (ctx.MeasFields.Count > 1)
            option["legend"] = ctx.BottomLegend();

        return option;
    }

    private static Dictionary<string, object?> Emphasis() => new()
    {
        ["focus"]     = "series",
        ["blurScope"] = "coordinateSystem"
    };
}
