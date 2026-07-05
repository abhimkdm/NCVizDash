using NCVizDash.Models;

namespace NCVizDash.RuleEngine;

/// <summary>
/// Summarises a user's field selection into counts and name-hint flags so
/// the rule registry can match against it without inspecting raw field lists.
/// Immutable value object — safe to cache and pass around freely.
/// </summary>
public sealed class FieldComposition
{
    /// <summary>Number of <see cref="FieldType.Measure"/> fields selected.</summary>
    public int Measures { get; }

    /// <summary>Number of <see cref="FieldType.Dimension"/> fields selected.</summary>
    public int Dimensions { get; }

    /// <summary>Number of <see cref="FieldType.Time"/> fields selected.</summary>
    public int Times { get; }

    /// <summary>Number of <see cref="FieldType.Filter"/> fields selected.</summary>
    public int Filters { get; }

    /// <summary>Total fields selected.</summary>
    public int Total { get; }

    // ── Name-hint flags ───────────────────────────────────────────────────────
    // Derived from column names of the selected fields; used to break ties between
    // visuals that have the same field-type signature (e.g. KPI vs Gauge).

    /// <summary>True when any measure name contains a percentage/rate/score hint.</summary>
    public bool HasRateHint { get; }

    /// <summary>True when any measure name contains a financial/revenue/cost hint.</summary>
    public bool HasFinancialHint { get; }

    /// <summary>True when any measure name contains a budget/target/goal hint.</summary>
    public bool HasBudgetHint { get; }

    /// <summary>True when any dimension name contains a geographic hint (region/country/city).</summary>
    public bool HasGeoHint { get; }

    /// <summary>True when any dimension name contains a person/employee/team hint.</summary>
    public bool HasPeopleHint { get; }

    /// <summary>True when more than one time field is selected (multi-series time chart).</summary>
    public bool HasMultiTime { get; }

    // ── Private name-hint term tables ──────────────────────────────────────────

    private static readonly string[] RateTerms =
        ["rate", "percent", "pct", "%", "ratio", "score", "index",
         "utilisation", "utilization", "efficiency", "completion"];

    private static readonly string[] FinancialTerms =
        ["revenue", "cost", "profit", "margin", "sales", "income",
         "expense", "budget", "spend", "earnings", "ebitda", "arr", "mrr"];

    private static readonly string[] BudgetTerms =
        ["budget", "target", "forecast", "goal", "plan", "quota", "allocation"];

    private static readonly string[] GeoTerms =
        ["region", "country", "city", "state", "territory", "location",
         "site", "branch", "office", "zone", "area"];

    private static readonly string[] PeopleTerms =
        ["employee", "person", "team", "manager", "owner", "assignee",
         "user", "member", "staff", "headcount"];

    /// <summary>Builds a composition from a raw field list.</summary>
    public static FieldComposition From(IReadOnlyList<FieldDescriptor> fields)
    {
        if (fields is null) throw new ArgumentNullException(nameof(fields));
        return new FieldComposition(fields);
    }

    private FieldComposition(IReadOnlyList<FieldDescriptor> fields)
    {
        Measures   = fields.Count(f => f.FieldType == FieldType.Measure);
        Dimensions = fields.Count(f => f.FieldType == FieldType.Dimension);
        Times      = fields.Count(f => f.FieldType == FieldType.Time);
        Filters    = fields.Count(f => f.FieldType == FieldType.Filter);
        Total      = fields.Count;
        HasMultiTime = Times > 1;

        var measureNames   = fields.Where(f => f.FieldType == FieldType.Measure)
                                   .Select(f => f.Name.ToLowerInvariant()).ToList();
        var dimensionNames = fields.Where(f => f.FieldType == FieldType.Dimension)
                                   .Select(f => f.Name.ToLowerInvariant()).ToList();

        HasRateHint      = measureNames.Any(n => RateTerms.Any(t => n.Contains(t)));
        HasFinancialHint = measureNames.Any(n => FinancialTerms.Any(t => n.Contains(t)));
        HasBudgetHint    = measureNames.Any(n => BudgetTerms.Any(t => n.Contains(t)));
        HasGeoHint       = dimensionNames.Any(n => GeoTerms.Any(t => n.Contains(t)));
        HasPeopleHint    = dimensionNames.Any(n => PeopleTerms.Any(t => n.Contains(t)));
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"M={Measures} D={Dimensions} T={Times} F={Filters}" +
        (HasRateHint      ? " [rate]"      : "") +
        (HasFinancialHint ? " [financial]"  : "") +
        (HasBudgetHint    ? " [budget]"     : "") +
        (HasGeoHint       ? " [geo]"        : "") +
        (HasPeopleHint    ? " [people]"     : "");
}
