using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;

namespace NCVizDash.RuleEngine;

/// <summary>
/// Deterministic, no-AI visualization rule engine.
/// Walks the <see cref="RuleRegistry"/> in priority order and returns the
/// first rule whose predicate matches the supplied <see cref="FieldComposition"/>.
/// </summary>
public sealed class DeterministicRuleEngine : IVisualizationRuleEngine
{
    private readonly ILogger<DeterministicRuleEngine> _logger;

    /// <summary>Initialises the engine (logger is optional — NullLogger is fine in tests).</summary>
    public DeterministicRuleEngine(ILogger<DeterministicRuleEngine> logger)
    {
        _logger = logger;
    }

    // ── IVisualizationRuleEngine ─────────────────────────────────────────────

    /// <inheritdoc/>
    public VisualType Recommend(IReadOnlyList<FieldDescriptor> fields)
    {
        var (visual, _, _) = RecommendWithExplanation(fields);
        return visual;
    }

    /// <inheritdoc/>
    public (VisualType Visual, string RuleName, string Explanation) RecommendWithExplanation(
        IReadOnlyList<FieldDescriptor> fields)
    {
        if (fields is null) throw new ArgumentNullException(nameof(fields));

        var composition = FieldComposition.From(fields);
        _logger.LogDebug("Rule engine evaluating: {Composition}", composition);

        foreach (var rule in RuleRegistry.All)
        {
            if (!rule.Matches(composition)) continue;

            _logger.LogDebug("Rule '{Rule}' matched → {Visual} — {Explanation}",
                rule.Name, rule.RecommendedVisual, rule.Explanation);

            return (rule.RecommendedVisual, rule.Name, rule.Explanation);
        }

        // Should never reach here — TABLE_FALLBACK always matches.
        _logger.LogWarning("No rule matched {Composition}; defaulting to Table.", composition);
        var fallback = RuleRegistry.All[RuleRegistry.All.Count - 1];
        return (VisualType.Table, fallback.Name, fallback.Explanation);
    }

    /// <inheritdoc/>
    public IReadOnlyList<(VisualType Visual, string RuleName)> AllMatches(
        IReadOnlyList<FieldDescriptor> fields)
    {
        if (fields is null) throw new ArgumentNullException(nameof(fields));

        var composition = FieldComposition.From(fields);

        return RuleRegistry.All
            .Where(r => r.Matches(composition))
            .Select(r => (r.RecommendedVisual, r.Name))
            .ToList()
            .AsReadOnly();
    }
}
