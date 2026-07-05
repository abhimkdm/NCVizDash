using NCVizDash.Models;

namespace NCVizDash.RuleEngine;

/// <summary>
/// A single named visualization rule: a predicate over a <see cref="FieldComposition"/>
/// that, when matched, recommends a <see cref="VisualType"/> and provides a human-readable
/// explanation (shown in debug logs and, later, the rule-explanation tooltip).
/// </summary>
public sealed class VisualizationRule
{
    /// <summary>Short identifier for logging and testing (e.g. "GAUGE_RATE").</summary>
    public string Name { get; }

    /// <summary>Lower number = higher priority. First matching rule wins.</summary>
    public int Priority { get; }

    /// <summary>The visual type recommended when this rule matches.</summary>
    public VisualType RecommendedVisual { get; }

    /// <summary>Human-readable explanation shown in the rule tooltip.</summary>
    public string Explanation { get; }

    private readonly Func<FieldComposition, bool> _predicate;

    /// <summary>Initialises a rule.</summary>
    public VisualizationRule(
        string name,
        int priority,
        VisualType recommendedVisual,
        string explanation,
        Func<FieldComposition, bool> predicate)
    {
        Name = name;
        Priority = priority;
        RecommendedVisual = recommendedVisual;
        Explanation = explanation;
        _predicate = predicate;
    }

    /// <summary>Returns true when this rule matches the given composition.</summary>
    public bool Matches(FieldComposition composition) => _predicate(composition);

    /// <inheritdoc/>
    public override string ToString() => $"[{Priority:D3}] {Name} → {RecommendedVisual}";
}
