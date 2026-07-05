using System.Globalization;
using System.Text;
using NCVizDash.Models;

namespace NCVizDash.ChartEngine.Builders;

/// <summary>
/// Builds self-contained HTML fragments for the two visual types that aren't
/// naturally ECharts series: KPI (a big animated number) and Table (a plain grid).
/// These are injected into the widget's WebView2 host alongside a small CSS
/// animation stylesheet rather than going through the ECharts option pipeline.
/// </summary>
public static class HtmlBuilder
{
    // ── KPI ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a KPI card: a large animated number (CSS count-up via JS), a label,
    /// and an optional trend indicator comparing the first vs. last row (if the
    /// widget has more than one row available, e.g. period-over-period).
    /// </summary>
    public static string BuildKpiHtml(ChartOptionContext ctx)
    {
        var measure = ctx.MeasFields.FirstOrDefault() ?? string.Empty;
        var value = ctx.ScalarSum(measure);
        var textCol = ctx.IsDark ? "#FFFFFF" : "#212121";
        var subCol  = ctx.IsDark ? "#BDBDBD" : "#757575";
        var accent  = ResolveConditionalColor(ctx, measure, value) ?? ChartPalette.Primary(ctx.Theme);

        var trendHtml = BuildTrendIndicator(ctx, measure);

        var label = HtmlEncode(measure);
        var formatted = FormatNumber(value);

        return $$"""
        <div class="kpi-card" style="color:{{textCol}}">
          <div class="kpi-value" data-target="{{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}}" style="color:{{accent}}">
            {{formatted}}
          </div>
          <div class="kpi-label" style="color:{{subCol}}">{{label}}</div>
          {{trendHtml}}
        </div>
        <style>
          .kpi-card { display:flex; flex-direction:column; align-items:center; justify-content:center; height:100%; font-family:'Segoe UI',sans-serif; animation: kpi-fade-in 0.5s ease-out; }
          .kpi-value { font-size: 2.4em; font-weight:700; line-height:1; animation: kpi-pop 0.6s cubic-bezier(0.34,1.56,0.64,1); }
          .kpi-label { font-size: 0.85em; margin-top:6px; text-transform:uppercase; letter-spacing:0.05em; }
          .kpi-trend { display:flex; align-items:center; gap:4px; margin-top:8px; font-size:0.8em; font-weight:600; animation: kpi-fade-in 0.8s ease-out; }
          @keyframes kpi-fade-in { from { opacity:0; } to { opacity:1; } }
          @keyframes kpi-pop { 0% { opacity:0; transform:scale(0.7); } 60% { transform:scale(1.05); } 100% { opacity:1; transform:scale(1); } }
        </style>
        """;
    }

    private static string BuildTrendIndicator(ChartOptionContext ctx, string measure)
    {
        if (ctx.Rows.Count < 2) return string.Empty;

        var values = ctx.NumericValues(measure).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (values.Count < 2) return string.Empty;

        var first = values[0];
        var last = values[values.Count - 1];
        if (first == 0) return string.Empty;

        var pctChange = (last - first) / Math.Abs(first) * 100d;
        var isPositive = pctChange >= 0;
        var color = isPositive
            ? (ctx.IsDark ? ChartPalette.PositiveDark : ChartPalette.PositiveLight)
            : (ctx.IsDark ? ChartPalette.NegativeDark : ChartPalette.NegativeLight);
        var arrow = isPositive ? "▲" : "▼";

        return $"""<div class="kpi-trend" style="color:{color}">{arrow} {Math.Abs(pctChange):F1}%</div>""";
    }

    // ── Table ─────────────────────────────────────────────────────────────────

    /// <summary>Builds a plain HTML table with staggered row fade-in animation.</summary>
    public static string BuildTableHtml(ChartOptionContext ctx)
    {
        var allFields = ctx.DimFields.Concat(ctx.MeasFields).Concat(ctx.SerFields).Distinct().ToList();
        if (allFields.Count == 0 && ctx.Rows.Count > 0)
            allFields = ctx.Rows[0].Keys.ToList();

        var headerBg = ctx.IsDark ? "#2D2D2D" : "#F5F5F5";
        var textCol  = ctx.IsDark ? "#E0E0E0" : "#212121";
        var borderCol = ctx.IsDark ? "#424242" : "#E0E0E0";
        var stripeCol = ctx.IsDark ? "#262626" : "#FAFAFA";

        var sb = new StringBuilder();
        sb.Append($"""<table class="nc-table" style="color:{textCol}"><thead><tr>""");
        foreach (var field in allFields)
            sb.Append($"""<th style="background:{headerBg};border-bottom:2px solid {borderCol}">{HtmlEncode(field)}</th>""");
        sb.Append("</tr></thead><tbody>");

        var rowIndex = 0;
        foreach (var row in ctx.Rows.Take(200)) // cap rendered rows; Phase 16 adds virtualisation
        {
            var stripe = rowIndex % 2 == 1 ? stripeCol : "transparent";
            sb.Append($"""<tr style="background:{stripe};animation-delay:{Math.Min(rowIndex * 20, 600)}ms">""");
            foreach (var field in allFields)
            {
                var val = row.TryGetValue(field, out var v) ? v?.ToString() ?? "" : "";
                sb.Append($"""<td style="border-bottom:1px solid {borderCol}">{HtmlEncode(val)}</td>""");
            }
            sb.Append("</tr>");
            rowIndex++;
        }

        sb.Append("</tbody></table>");
        sb.Append($$"""
        <style>
          .nc-table { width:100%; border-collapse:collapse; font-family:'Segoe UI',sans-serif; font-size:0.8em; }
          .nc-table th { padding:8px 10px; text-align:left; font-weight:600; position:sticky; top:0; }
          .nc-table td { padding:6px 10px; }
          .nc-table tr { animation: row-fade-in 0.35s ease-out both; }
          @keyframes row-fade-in { from { opacity:0; transform:translateY(4px); } to { opacity:1; transform:translateY(0); } }
        </style>
        """);

        return sb.ToString();
    }

    // ── Conditional formatting (Phase 12) ────────────────────────────────────

    /// <summary>
    /// Evaluates <see cref="DashboardWidget.ConditionalFormatRules"/> for the given
    /// measure/value; returns the colour of the first enabled matching rule, or null
    /// if none match (caller falls back to the theme's default accent colour).
    /// </summary>
    private static string? ResolveConditionalColor(ChartOptionContext ctx, string measureField, double value)
    {
        foreach (var rule in ctx.Widget.ConditionalFormatRules)
        {
            if (!rule.IsEnabled || rule.FieldName != measureField) continue;
            if (!double.TryParse(rule.Values.ElementAtOrDefault(0), NumberStyles.Any, CultureInfo.InvariantCulture, out var threshold))
                continue;

            var matches = rule.Operator switch
            {
                FilterOperator.Equals             => Math.Abs(value - threshold) < 0.0001,
                FilterOperator.NotEquals          => Math.Abs(value - threshold) >= 0.0001,
                FilterOperator.GreaterThan         => value > threshold,
                FilterOperator.GreaterThanOrEqual  => value >= threshold,
                FilterOperator.LessThan            => value < threshold,
                FilterOperator.LessThanOrEqual     => value <= threshold,
                FilterOperator.Between when rule.Values.Count >= 2 &&
                    double.TryParse(rule.Values[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var hi) =>
                    value >= threshold && value <= hi,
                _ => false
            };

            if (matches) return rule.Color;
        }

        return null;
    }

    // ── Shared ────────────────────────────────────────────────────────────────

    private static string FormatNumber(double value) =>
        value >= 1_000_000 ? $"{value / 1_000_000:F1}M"
      : value >= 1_000     ? $"{value / 1_000:F1}K"
      : value == Math.Floor(value) ? value.ToString("N0")
      : value.ToString("N1");

    /// <summary>Minimal HTML-entity encoder (avoids taking a System.Web dependency for 5 characters).</summary>
    private static string HtmlEncode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
