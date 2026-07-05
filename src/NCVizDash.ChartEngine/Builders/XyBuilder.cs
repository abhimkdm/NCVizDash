namespace NCVizDash.ChartEngine.Builders;

/// <summary>Builds ECharts options for Scatter, Bubble, Heatmap, and Treemap charts.</summary>
public static class XyBuilder
{
    // ── Scatter ───────────────────────────────────────────────────────────────

    /// <summary>Builds an ECharts option for a scatter chart.</summary>
    public static Dictionary<string, object?> BuildScatter(ChartOptionContext ctx)
    {
        var xField = ctx.MeasFields.ElementAtOrDefault(0) ?? string.Empty;
        var yField = ctx.MeasFields.ElementAtOrDefault(1) ?? string.Empty;
        var hasGroups = ctx.DimFields.Count > 0;

        List<Dictionary<string, object?>> series;

        if (hasGroups)
        {
            var dim = ctx.DimFields[0];
            var groups = ctx.Rows.GroupBy(r => r.TryGetValue(dim, out var v) ? v?.ToString() ?? "" : "");

            series = groups.Select((g, i) => new Dictionary<string, object?>
            {
                ["type"]      = "scatter",
                ["name"]      = g.Key,
                ["symbolSize"]= 12,
                ["data"]      = g.Select(r => new object?[] { NumericOf(r, xField), NumericOf(r, yField) }).ToArray(),
                ["itemStyle"] = new Dictionary<string, object?> { ["color"] = ctx.Palette[i % ctx.Palette.Count], ["opacity"] = 0.8 },
                ["emphasis"]  = new Dictionary<string, object?> { ["itemStyle"] = new Dictionary<string, object?> { ["opacity"] = 1, ["shadowBlur"] = 10 } }
            }).ToList();
        }
        else
        {
            series =
            [
                new Dictionary<string, object?>
                {
                    ["type"]      = "scatter",
                    ["name"]      = ctx.Widget.Title,
                    ["symbolSize"]= 12,
                    ["data"]      = ctx.Rows.Select(r => new object?[] { NumericOf(r, xField), NumericOf(r, yField) }).ToArray(),
                    ["itemStyle"] = new Dictionary<string, object?> { ["color"] = ctx.Palette[0], ["opacity"] = 0.8 },
                    ["emphasis"]  = new Dictionary<string, object?> { ["itemStyle"] = new Dictionary<string, object?> { ["opacity"] = 1, ["shadowBlur"] = 10 } }
                }
            ];
        }

        return new Dictionary<string, object?>
        {
            ["backgroundColor"] = "transparent",
            ["color"]   = ctx.Palette.ToArray(),
            ["tooltip"] = ScatterTooltip(ctx, xField, yField),
            ["legend"]  = hasGroups ? ctx.BottomLegend() : null,
            ["grid"]    = ctx.DefaultGrid(),
            ["xAxis"]   = new Dictionary<string, object?> { ["type"] = "value", ["name"] = xField, ["axisLabel"] = AxisLabelDict(ctx), ["splitLine"] = SplitLineDict(ctx) },
            ["yAxis"]   = new Dictionary<string, object?> { ["type"] = "value", ["name"] = yField, ["axisLabel"] = AxisLabelDict(ctx), ["splitLine"] = SplitLineDict(ctx) },
            ["series"]  = series.Cast<object?>().ToArray()
        };
    }

    // ── Bubble ────────────────────────────────────────────────────────────────

    /// <summary>Builds an ECharts option for a bubble chart.</summary>
    public static Dictionary<string, object?> BuildBubble(ChartOptionContext ctx)
    {
        var xField    = ctx.MeasFields.ElementAtOrDefault(0) ?? string.Empty;
        var yField    = ctx.MeasFields.ElementAtOrDefault(1) ?? string.Empty;
        var sizeField = ctx.MeasFields.ElementAtOrDefault(2) ?? string.Empty;

        var sizeValues = ctx.Rows.Select(r => NumericOf(r, sizeField) ?? 0d).ToList();
        var maxSize = sizeValues.Count > 0 ? Math.Max(sizeValues.Max(), 0.0001) : 1d;

        var dimField = ctx.DimFields.FirstOrDefault();
        var data = ctx.Rows.Select(r => new object?[]
        {
            NumericOf(r, xField),
            NumericOf(r, yField),
            NumericOf(r, sizeField),
            dimField is not null && r.TryGetValue(dimField, out var v) ? v?.ToString() : null
        }).ToArray();

        return new Dictionary<string, object?>
        {
            ["backgroundColor"] = "transparent",
            ["color"]   = ctx.Palette.ToArray(),
            ["tooltip"] = BubbleTooltip(ctx, xField, yField, sizeField),
            ["grid"]    = ctx.DefaultGrid(),
            ["xAxis"]   = new Dictionary<string, object?> { ["type"] = "value", ["name"] = xField, ["axisLabel"] = AxisLabelDict(ctx), ["splitLine"] = SplitLineDict(ctx) },
            ["yAxis"]   = new Dictionary<string, object?> { ["type"] = "value", ["name"] = yField, ["axisLabel"] = AxisLabelDict(ctx), ["splitLine"] = SplitLineDict(ctx) },
            ["series"]  = new[]
            {
                new Dictionary<string, object?>
                {
                    ["type"]         = "scatter",
                    ["name"]         = ctx.Widget.Title,
                    ["data"]         = data,
                    ["symbolSize"]   = $"function(val) {{ return Math.sqrt(Math.abs(val[2]) / {maxSize.ToString(System.Globalization.CultureInfo.InvariantCulture)}) * 50 + 8; }}",
                    ["itemStyle"]    = new Dictionary<string, object?> { ["color"] = ctx.Palette[0], ["opacity"] = 0.7 },
                    ["emphasis"]     = new Dictionary<string, object?> { ["itemStyle"] = new Dictionary<string, object?> { ["opacity"] = 1, ["shadowBlur"] = 14 } }
                }
            }
        };
    }

    // ── Heatmap ───────────────────────────────────────────────────────────────

    /// <summary>Builds an ECharts option for a heatmap chart.</summary>
    public static Dictionary<string, object?> BuildHeatmap(ChartOptionContext ctx)
    {
        var dim1 = ctx.DimFields.ElementAtOrDefault(0) ?? string.Empty;
        var dim2 = ctx.DimFields.ElementAtOrDefault(1) ?? string.Empty;
        var measure = ctx.MeasFields.FirstOrDefault() ?? string.Empty;

        var xLabels = ctx.Rows.Select(r => StrOf(r, dim1)).Distinct().ToList();
        var yLabels = ctx.Rows.Select(r => StrOf(r, dim2)).Distinct().ToList();

        var data = new List<object?[]>();
        var values = new List<double>();

        foreach (var row in ctx.Rows)
        {
            var xi = xLabels.IndexOf(StrOf(row, dim1));
            var yi = yLabels.IndexOf(StrOf(row, dim2));
            var val = NumericOf(row, measure) ?? 0d;
            if (xi < 0 || yi < 0) continue;

            data.Add([xi, yi, val]);
            values.Add(val);
        }

        var min = values.Count > 0 ? values.Min() : 0d;
        var max = values.Count > 0 ? values.Max() : 1d;
        var primary = ctx.IsDark ? ChartPalette.PrimaryDark : ChartPalette.PrimaryLight;

        return new Dictionary<string, object?>
        {
            ["backgroundColor"] = "transparent",
            ["tooltip"] = new Dictionary<string, object?> { ["position"] = "top", ["trigger"] = "item" },
            ["grid"]    = ctx.DefaultGrid(),
            ["xAxis"]   = new Dictionary<string, object?> { ["type"] = "category", ["data"] = xLabels, ["splitArea"] = new Dictionary<string, object?> { ["show"] = true }, ["axisLabel"] = AxisLabelDict(ctx) },
            ["yAxis"]   = new Dictionary<string, object?> { ["type"] = "category", ["data"] = yLabels, ["splitArea"] = new Dictionary<string, object?> { ["show"] = true }, ["axisLabel"] = AxisLabelDict(ctx) },
            ["visualMap"] = new Dictionary<string, object?>
            {
                ["min"] = min, ["max"] = max,
                ["calculable"] = true,
                ["orient"] = "horizontal",
                ["left"] = "center",
                ["bottom"] = 0,
                ["inRange"] = new Dictionary<string, object?> { ["color"] = new[] { "#FFFFFF", primary } },
                ["textStyle"] = new Dictionary<string, object?> { ["color"] = ctx.IsDark ? "#FFFFFF" : "#212121" }
            },
            ["series"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["type"]     = "heatmap",
                    ["name"]     = measure,
                    ["data"]     = data.Cast<object?>().ToArray(),
                    ["label"]    = new Dictionary<string, object?> { ["show"] = false },
                    ["emphasis"] = new Dictionary<string, object?> { ["itemStyle"] = new Dictionary<string, object?> { ["shadowBlur"] = 10, ["shadowColor"] = "rgba(0,0,0,0.5)" } }
                }
            }
        };
    }

    // ── Treemap ───────────────────────────────────────────────────────────────

    /// <summary>Builds an ECharts option for a treemap chart.</summary>
    public static Dictionary<string, object?> BuildTreemap(ChartOptionContext ctx)
    {
        var measure = ctx.MeasFields.FirstOrDefault() ?? string.Empty;
        var levelFields = ctx.DimFields.Count > 0 ? ctx.DimFields.ToList() : [string.Empty];

        var root = BuildTreemapLevel(ctx.Rows, levelFields, 0, measure, ctx);

        return new Dictionary<string, object?>
        {
            ["backgroundColor"] = "transparent",
            ["color"]   = ctx.Palette.ToArray(),
            ["tooltip"] = new Dictionary<string, object?> { ["formatter"] = "{b}: {c}" },
            ["series"]  = new[]
            {
                new Dictionary<string, object?>
                {
                    ["type"]           = "treemap",
                    ["data"]           = root,
                    ["roam"]           = false,
                    ["nodeClick"]      = "zoomToNode",
                    ["breadcrumb"]     = new Dictionary<string, object?> { ["show"] = levelFields.Count > 1 },
                    ["label"]          = new Dictionary<string, object?> { ["show"] = true, ["fontSize"] = 11 },
                    ["upperLabel"]     = new Dictionary<string, object?> { ["show"] = true, ["height"] = 22 },
                    ["itemStyle"]      = new Dictionary<string, object?> { ["borderColor"] = ctx.IsDark ? "#1E1E1E" : "#FFFFFF", ["borderWidth"] = 2, ["gapWidth"] = 2 },
                    ["levels"]         = TreemapLevels(ctx)
                }
            }
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static object[] BuildTreemapLevel(
        IEnumerable<IReadOnlyDictionary<string, object?>> rows,
        List<string> levelFields, int depth, string measure, ChartOptionContext ctx)
    {
        if (depth >= levelFields.Count || string.IsNullOrEmpty(levelFields[depth]))
        {
            return rows.Select(r => (object)new Dictionary<string, object?>
            {
                ["name"]  = "Value",
                ["value"] = NumericOf(r, measure) ?? 0d
            }).ToArray();
        }

        var field = levelFields[depth];
        var groups = rows.GroupBy(r => StrOf(r, field));

        return groups.Select(g => (object)new Dictionary<string, object?>
        {
            ["name"]     = g.Key,
            ["value"]    = g.Sum(r => NumericOf(r, measure) ?? 0d),
            ["children"] = depth + 1 < levelFields.Count
                ? BuildTreemapLevel(g, levelFields, depth + 1, measure, ctx)
                : null
        }).ToArray();
    }

    private static object[] TreemapLevels(ChartOptionContext ctx) =>
    [
        new Dictionary<string, object?>
        {
            ["itemStyle"] = new Dictionary<string, object?> { ["borderWidth"] = 0, ["gapWidth"] = 1 }
        },
        new Dictionary<string, object?>
        {
            ["colorSaturation"] = new[] { 0.35, 0.6 },
            ["itemStyle"] = new Dictionary<string, object?>
            {
                ["borderColorSaturation"] = 0.6, ["gapWidth"] = 1, ["borderWidth"] = 2
            }
        }
    ];

    private static Dictionary<string, object?> AxisLabelDict(ChartOptionContext ctx) => new()
    {
        ["color"] = ctx.IsDark ? ChartPalette.AxisColorDark : ChartPalette.AxisColorLight, ["fontSize"] = 11
    };

    private static Dictionary<string, object?> SplitLineDict(ChartOptionContext ctx) => new()
    {
        ["lineStyle"] = new Dictionary<string, object?> { ["color"] = ctx.IsDark ? ChartPalette.SplitLineDark : ChartPalette.SplitLineLight }
    };

    private static Dictionary<string, object?> ScatterTooltip(ChartOptionContext ctx, string xField, string yField)
    {
        var tt = ctx.ItemTooltip();
        tt["formatter"] = $"function(p) {{ return p.seriesName + '<br/>{xField}: ' + p.value[0] + '<br/>{yField}: ' + p.value[1]; }}";
        return tt;
    }

    private static Dictionary<string, object?> BubbleTooltip(ChartOptionContext ctx, string xField, string yField, string sizeField)
    {
        var tt = ctx.ItemTooltip();
        tt["formatter"] =
            $"function(p) {{ return (p.value[3] || p.seriesName) + '<br/>{xField}: ' + p.value[0] + " +
            $"'<br/>{yField}: ' + p.value[1] + '<br/>{sizeField}: ' + p.value[2]; }}";
        return tt;
    }

    private static double? NumericOf(IReadOnlyDictionary<string, object?> row, string field)
    {
        if (string.IsNullOrEmpty(field) || !row.TryGetValue(field, out var v) || v is null) return null;
        return v is double d ? d
             : double.TryParse(v.ToString(), System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
               ? parsed : null;
    }

    private static string StrOf(IReadOnlyDictionary<string, object?> row, string field) =>
        !string.IsNullOrEmpty(field) && row.TryGetValue(field, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
}
