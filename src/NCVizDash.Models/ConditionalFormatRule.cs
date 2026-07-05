namespace NCVizDash.Models;

/// <summary>
/// A conditional-formatting rule applied to a widget's rendered value(s):
/// "if Revenue &lt; 1000, colour red". Evaluated client-side against already-fetched
/// data (in <c>HtmlBuilder</c> for KPI/Table), not pushed into SQL, since it only
/// affects presentation, not which rows are returned.
/// </summary>
public sealed class ConditionalFormatRule
{
    /// <summary>Unique rule ID.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>The measure field this rule evaluates.</summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>The comparison operator applied to the field's value.</summary>
    public FilterOperator Operator { get; set; } = FilterOperator.LessThan;

    /// <summary>Comparison value(s) — same shape as <see cref="WidgetFilter.Values"/>.</summary>
    public List<string> Values { get; set; } = [];

    /// <summary>Hex colour applied when the rule matches, e.g. "#F44336".</summary>
    public string Color { get; set; } = "#F44336";

    /// <summary>Whether this rule is currently active. Allows toggling off without deleting it.</summary>
    public bool IsEnabled { get; set; } = true;
}
