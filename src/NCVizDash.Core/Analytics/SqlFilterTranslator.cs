using System.Globalization;
using System.Text;
using NCVizDash.Models;

namespace NCVizDash.Core.Analytics;

/// <summary>
/// Translates <see cref="WidgetFilter"/> instances into SQL WHERE-clause fragments.
/// Lives in <c>Core.Analytics</c> (not <c>NCVizDash.DuckDB</c>) so that both
/// <c>AnalyticsQueryBuilder</c> (which builds full queries) and the TaskPane's
/// <c>CrossFilterManager</c> (which only needs a WHERE fragment for display/debug
/// purposes) share one implementation without the TaskPane project taking a
/// dependency on the DuckDB engine project.
/// </summary>
public static class SqlFilterTranslator
{
    /// <summary>
    /// Builds one SQL fragment per enabled, non-empty filter. Disabled filters and
    /// filters with no usable value(s) are silently skipped.
    /// </summary>
    public static List<string> BuildClauses(IReadOnlyList<WidgetFilter> filters)
    {
        var clauses = new List<string>();

        foreach (var filter in filters)
        {
            if (!filter.IsEnabled || string.IsNullOrWhiteSpace(filter.FieldName)) continue;

            var col = Quote(SanitiseColumnName(filter.FieldName));
            var clause = filter.Operator switch
            {
                FilterOperator.Equals             => $"{col} = {LiteralOf(filter.Values.ElementAtOrDefault(0))}",
                FilterOperator.NotEquals          => $"{col} <> {LiteralOf(filter.Values.ElementAtOrDefault(0))}",
                FilterOperator.GreaterThan         => $"{col} > {LiteralOf(filter.Values.ElementAtOrDefault(0))}",
                FilterOperator.GreaterThanOrEqual  => $"{col} >= {LiteralOf(filter.Values.ElementAtOrDefault(0))}",
                FilterOperator.LessThan            => $"{col} < {LiteralOf(filter.Values.ElementAtOrDefault(0))}",
                FilterOperator.LessThanOrEqual     => $"{col} <= {LiteralOf(filter.Values.ElementAtOrDefault(0))}",
                FilterOperator.Contains            => $"{col} ILIKE {LiteralOf("%" + filter.Values.ElementAtOrDefault(0) + "%")}",
                FilterOperator.In                  => $"{col} IN ({string.Join(", ", filter.Values.Select(LiteralOf))})",
                FilterOperator.NotIn               => $"{col} NOT IN ({string.Join(", ", filter.Values.Select(LiteralOf))})",
                FilterOperator.Between when filter.Values.Count >= 2 =>
                    $"{col} BETWEEN {LiteralOf(filter.Values[0])} AND {LiteralOf(filter.Values[1])}",
                _ => null
            };

            if (clause is not null)
                clauses.Add(clause);
        }

        return clauses;
    }

    /// <summary>Joins all clauses with " AND " into a single WHERE fragment (no leading "WHERE").</summary>
    public static string BuildWhereFragment(IReadOnlyList<WidgetFilter> filters) =>
        string.Join(" AND ", BuildClauses(filters));

    /// <summary>
    /// Sanitises a raw field/column name identically to how <c>DuckDbAnalyticsEngine</c>
    /// sanitises column names at load time, so filters built from a widget's original
    /// (pre-sanitisation) Excel header text resolve to the correct DuckDB column.
    /// </summary>
    public static string SanitiseColumnName(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');

        var result = sb.ToString().Trim('_');
        if (result.Length == 0) result = "col";
        if (char.IsDigit(result[0])) result = "_" + result;

        return result.ToLowerInvariant();
    }

    /// <summary>Renders a filter value as a safely-escaped SQL literal (numeric or quoted string).</summary>
    public static string LiteralOf(string? value)
    {
        if (value is null) return "NULL";

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            return num.ToString(CultureInfo.InvariantCulture);

        var escaped = value.Replace("'", "''");
        return $"'{escaped}'";
    }

    private static string Quote(string identifier) => $"\"{identifier}\"";
}
