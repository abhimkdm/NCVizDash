namespace NCVizDash.ChartEngine.Builders;

/// <summary>Builds ECharts options for Pie, Donut, Gauge, and Radar charts.</summary>
public static class PolarBuilder
{
    // ── Pie ───────────────────────────────────────────────────────────────────

    /// <summary>Builds an ECharts Pie option from the widget's first dimension/measure pairing.</summary>
    public static Dictionary<string, object?> BuildPie(ChartOptionContext ctx)
    {
        var dim     = ctx.DimFields.FirstOrDefault() ?? string.Empty;
        var measure = ctx.MeasFields.FirstOrDefault() ?? string.Empty;
        var pairs   = ctx.NameValuePairs(dim, measure);

        return new Dictionary<string, object?>
        {
            ["backgroundColor"] = "transparent",
            ["color"]     = ctx.Palette.ToArray(),
            ["tooltip"]   = PieTooltip(ctx),
            ["legend"]    = ctx.BottomLegend(),
            ["series"]    = new[]
            {
                new Dictionary<string, object?>
                {
                    ["type"]         = "pie",
                    ["name"]         = measure,
                    ["radius"]       = new[] { "0%", "68%" },
                    ["center"]       = new[] { "50%", "48%" },
                    ["data"]         = pairs.Select(p => PieDataItem(p.Name, p.Value, ctx)).ToArray(),
                    ["emphasis"]     = PieEmphasis(),
                    ["label"]        = PieLabel(ctx),
                    ["labelLine"]    = new Dictionary<string, object?> { ["smooth"] = true, ["length"] = 10, ["length2"] = 12 },
                    ["animationType"]= "expansion"
                }
            }
        };
    }

    // ── Donut ─────────────────────────────────────────────────────────────────

    /// <summary>Builds an ECharts Donut (Pie with an inner radius) option, with a centre-total label.</summary>
    public static Dictionary<string, object?> BuildDonut(ChartOptionContext ctx)
    {
        var dim     = ctx.DimFields.FirstOrDefault() ?? string.Empty;
        var measure = ctx.MeasFields.FirstOrDefault() ?? string.Empty;
        var pairs   = ctx.NameValuePairs(dim, measure);
        var total   = pairs.Sum(p => p.Value);

        return new Dictionary<string, object?>
        {
            ["backgroundColor"] = "transparent",
            ["color"]     = ctx.Palette.ToArray(),
            ["tooltip"]   = PieTooltip(ctx),
            ["legend"]    = ctx.BottomLegend(),
            ["graphic"]   = CentreLabel(ctx, total),
            ["series"]    = new[]
            {
                new Dictionary<string, object?>
                {
                    ["type"]         = "pie",
                    ["name"]         = measure,
                    ["radius"]       = new[] { "42%", "68%" },
                    ["center"]       = new[] { "50%", "48%" },
                    ["data"]         = pairs.Select(p => PieDataItem(p.Name, p.Value, ctx)).ToArray(),
                    ["emphasis"]     = PieEmphasis(),
                    ["label"]        = new Dictionary<string, object?> { ["show"] = false },
                    ["animationType"]= "expansion"
                }
            }
        };
    }

    // ── Gauge ─────────────────────────────────────────────────────────────────

    /// <summary>Builds an ECharts Gauge option, clamped to a 0–100 range with a tri-colour band.</summary>
    public static Dictionary<string, object?> BuildGauge(ChartOptionContext ctx)
    {
        var measure = ctx.MeasFields.FirstOrDefault() ?? string.Empty;
        var value   = Math.Min(100d, Math.Max(0d, ctx.ScalarFirst(measure)));
        var primary = ctx.IsDark ? ChartPalette.PrimaryDark : ChartPalette.PrimaryLight;
        var pos     = ctx.IsDark ? ChartPalette.PositiveDark : ChartPalette.PositiveLight;
        var neg     = ctx.IsDark ? ChartPalette.NegativeDark : ChartPalette.NegativeLight;
        var textCol = ctx.IsDark ? "#FFFFFF" : "#212121";
        var subCol  = ctx.IsDark ? "#BDBDBD" : "#757575";

        return new Dictionary<string, object?>
        {
            ["backgroundColor"] = "transparent",
            ["series"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["type"]      = "gauge",
                    ["center"]    = new[] { "50%", "60%" },
                    ["radius"]    = "80%",
                    ["startAngle"]= 220,
                    ["endAngle"]  = -40,
                    ["min"]       = 0,
                    ["max"]       = 100,
                    ["splitNumber"]= 5,
                    ["pointer"]   = new Dictionary<string, object?>
                    {
                        ["length"] = "60%", ["width"] = 6,
                        ["itemStyle"] = new Dictionary<string, object?> { ["color"] = primary }
                    },
                    ["axisLine"]  = new Dictionary<string, object?>
                    {
                        ["lineStyle"] = new Dictionary<string, object?>
                        {
                            ["width"]     = 18,
                            ["color"]     = new object[]
                            {
                                new object[] { 0.3, neg },
                                new object[] { 0.7, "#FFC107" },
                                new object[] { 1.0, pos }
                            }
                        }
                    },
                    ["splitLine"] = new Dictionary<string, object?>
                    {
                        ["length"] = 20,
                        ["lineStyle"] = new Dictionary<string, object?> { ["color"] = "auto", ["width"] = 2 }
                    },
                    ["axisTick"]  = new Dictionary<string, object?>
                    {
                        ["length"] = 10,
                        ["lineStyle"] = new Dictionary<string, object?> { ["color"] = "auto" }
                    },
                    ["axisLabel"] = new Dictionary<string, object?>
                    {
                        ["color"] = subCol, ["fontSize"] = 11, ["distance"] = 24
                    },
                    ["detail"]    = new Dictionary<string, object?>
                    {
                        ["valueAnimation"] = true,
                        ["formatter"]      = "{value}%",
                        ["color"]          = textCol,
                        ["fontSize"]       = 26,
                        ["fontWeight"]     = "bold",
                        ["offsetCenter"]   = new object[] { 0, "20%" }
                    },
                    ["title"]     = new Dictionary<string, object?>
                    {
                        ["offsetCenter"] = new object[] { 0, "38%" },
                        ["color"]        = subCol,
                        ["fontSize"]     = 12
                    },
                    ["data"]      = new[]
                    {
                        new Dictionary<string, object?> { ["value"] = Math.Round(value, 1), ["name"] = measure }
                    }
                }
            }
        };
    }

    // ── Radar ─────────────────────────────────────────────────────────────────

    /// <summary>Builds an ECharts Radar option — one series per dimension value if a dimension is present, else a single aggregate series.</summary>
    public static Dictionary<string, object?> BuildRadar(ChartOptionContext ctx)
    {
        // Each measure is an axis on the radar; each dimension value is a series.
        var hasGroups  = ctx.DimFields.Count > 0;
        var indicators = ctx.MeasFields.Select(f => new Dictionary<string, object?> { ["name"] = f }).ToArray();

        List<Dictionary<string, object?>> seriesList;

        if (hasGroups)
        {
            var dim = ctx.DimFields[0];
            var groups = ctx.Rows
                .GroupBy(r => r.TryGetValue(dim, out var v) ? v?.ToString() ?? "" : "")
                .Take(6)
                .ToList();

            seriesList = groups.Select((g, i) => new Dictionary<string, object?>
            {
                ["name"]      = g.Key,
                ["value"]     = ctx.MeasFields
                    .Select(m => (object?)(g.Average(r =>
                        r.TryGetValue(m, out var v) && double.TryParse(v?.ToString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0)))
                    .ToArray(),
                ["itemStyle"] = new Dictionary<string, object?> { ["color"] = ctx.Palette[i % ctx.Palette.Count] },
                ["areaStyle"] = new Dictionary<string, object?> { ["opacity"] = 0.15 }
            }).ToList();
        }
        else
        {
            var vals = ctx.MeasFields.Select(m => (object?)ctx.ScalarSum(m)).ToArray();
            seriesList = [new Dictionary<string, object?>
            {
                ["name"]      = ctx.Widget.Title,
                ["value"]     = vals,
                ["itemStyle"] = new Dictionary<string, object?> { ["color"] = ctx.Palette[0] },
                ["areaStyle"] = new Dictionary<string, object?> { ["opacity"] = 0.25 }
            }];
        }

        var textCol = ctx.IsDark ? ChartPalette.AxisColorDark : ChartPalette.AxisColorLight;
        var lineCol = ctx.IsDark ? ChartPalette.SplitLineDark : ChartPalette.SplitLineLight;

        return new Dictionary<string, object?>
        {
            ["backgroundColor"] = "transparent",
            ["color"]    = ctx.Palette.ToArray(),
            ["tooltip"]  = ctx.ItemTooltip(),
            ["legend"]   = hasGroups ? ctx.BottomLegend() : null,
            ["radar"]    = new Dictionary<string, object?>
            {
                ["indicator"]  = indicators,
                ["shape"]      = "polygon",
                ["splitNumber"]= 4,
                ["axisName"]   = new Dictionary<string, object?> { ["color"] = textCol, ["fontSize"] = 11 },
                ["splitLine"]  = new Dictionary<string, object?> { ["lineStyle"] = new Dictionary<string, object?> { ["color"] = lineCol } },
                ["splitArea"]  = new Dictionary<string, object?> { ["show"] = false }
            },
            ["series"]   = new[]
            {
                new Dictionary<string, object?>
                {
                    ["type"]       = "radar",
                    ["data"]       = seriesList.Cast<object?>().ToArray(),
                    ["emphasis"]   = new Dictionary<string, object?> { ["lineStyle"] = new Dictionary<string, object?> { ["width"] = 3 } }
                }
            }
        };
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private static Dictionary<string, object?> PieTooltip(ChartOptionContext ctx)
    {
        var tt = ctx.ItemTooltip();
        tt["formatter"] = "{a}<br/>{b}: {c} ({d}%)";
        return tt;
    }

    private static Dictionary<string, object?> PieLabel(ChartOptionContext ctx) => new()
    {
        ["show"]       = true,
        ["formatter"]  = "{b}\n{d}%",
        ["fontSize"]   = 11,
        ["color"]      = ctx.IsDark ? ChartPalette.AxisColorDark : ChartPalette.AxisColorLight
    };

    private static Dictionary<string, object?> PieEmphasis() => new()
    {
        ["itemStyle"]  = new Dictionary<string, object?> { ["shadowBlur"] = 16, ["shadowOffsetX"] = 0, ["shadowColor"] = "rgba(0,0,0,0.3)" },
        ["label"]      = new Dictionary<string, object?> { ["show"] = true, ["fontWeight"] = "bold" }
    };

    private static Dictionary<string, object?> PieDataItem(string name, double value, ChartOptionContext ctx) =>
        new()
        {
            ["name"]      = name,
            ["value"]     = value,
            ["emphasis"]  = new Dictionary<string, object?> { ["scale"] = true, ["scaleSize"] = 8 }
        };

    private static object CentreLabel(ChartOptionContext ctx, double total)
    {
        var textCol = ctx.IsDark ? "#FFFFFF" : "#212121";
        var subCol  = ctx.IsDark ? "#BDBDBD" : "#757575";

        return new[]
        {
            new Dictionary<string, object?>
            {
                ["type"]   = "text",
                ["left"]   = "center",
                ["top"]    = "center",
                ["style"]  = new Dictionary<string, object?>
                {
                    ["text"]     = FormatCompact(total),
                    ["fontSize"] = 22,
                    ["fontWeight"] = "bold",
                    ["fill"]     = textCol,
                    ["textAlign"]= "center"
                }
            },
            new Dictionary<string, object?>
            {
                ["type"]  = "text",
                ["left"]  = "center",
                ["top"]   = "55%",
                ["style"] = new Dictionary<string, object?>
                {
                    ["text"]     = ctx.MeasFields.FirstOrDefault() ?? "Total",
                    ["fontSize"] = 11,
                    ["fill"]     = subCol,
                    ["textAlign"]= "center"
                }
            }
        };
    }

    private static string FormatCompact(double value) =>
        value >= 1_000_000 ? $"{value / 1_000_000:F1}M"
      : value >= 1_000     ? $"{value / 1_000:F1}K"
      : value.ToString("N0");
}
