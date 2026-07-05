using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Analytics;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Services;

/// <summary>
/// Session-scoped (one per open dashboard) cross-filter coordinator.
/// <para>
/// Each entry is keyed by field name: clicking a data point in one widget sets
/// (or, if the same value from the same widget is clicked again, clears — standard
/// click-to-deselect UX) the active filter for that field. <see cref="FiltersChanged"/>
/// tells every widget to re-render; <see cref="GetActiveFilters"/> is what
/// <c>WidgetRenderCoordinator</c> merges into each widget's <c>QuerySpec</c>.
/// </para>
/// </summary>
public sealed class CrossFilterManager : IFilterManager
{
    private readonly ILogger<CrossFilterManager> _logger;
    private readonly Dictionary<string, CrossFilterEntry> _activeFilters = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <inheritdoc/>
    public event EventHandler? FiltersChanged;

    /// <inheritdoc/>
    public int ActiveFilterCount
    {
        get { lock (_lock) return _activeFilters.Count; }
    }

    /// <summary>Initialises the manager.</summary>
    public CrossFilterManager(ILogger<CrossFilterManager> logger)
    {
        _logger = logger;
    }

    // ── IFilterManager ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void ApplyFilter(Guid sourceWidgetId, string fieldName, IReadOnlyList<object?> selectedValues)
    {
        if (string.IsNullOrWhiteSpace(fieldName)) return;

        var stringValues = selectedValues
            .Select(v => v?.ToString() ?? string.Empty)
            .Where(v => v.Length > 0)
            .ToList();

        bool changed;

        lock (_lock)
        {
            if (stringValues.Count == 0)
            {
                changed = _activeFilters.Remove(fieldName);
            }
            else if (_activeFilters.TryGetValue(fieldName, out var existing) &&
                     existing.SourceWidgetId == sourceWidgetId &&
                     existing.Values.SequenceEqual(stringValues, StringComparer.OrdinalIgnoreCase))
            {
                // Same widget clicked the same value(s) again — toggle off.
                _activeFilters.Remove(fieldName);
                changed = true;
            }
            else
            {
                _activeFilters[fieldName] = new CrossFilterEntry(sourceWidgetId, stringValues);
                changed = true;
            }
        }

        if (!changed) return;

        _logger.LogInformation(
            "Cross-filter on '{Field}' updated by widget {WidgetId}: [{Values}]",
            fieldName, sourceWidgetId, string.Join(", ", stringValues));

        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public void ClearAll()
    {
        bool hadAny;
        lock (_lock)
        {
            hadAny = _activeFilters.Count > 0;
            _activeFilters.Clear();
        }

        if (!hadAny) return;

        _logger.LogInformation("All cross-filters cleared.");
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public string BuildWhereClause()
    {
        lock (_lock)
        {
            return SqlFilterTranslator.BuildWhereFragment(GetActiveFiltersUnlocked(excludeSourceWidgetId: null));
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<WidgetFilter> GetActiveFilters(Guid? excludeSourceWidgetId = null)
    {
        lock (_lock)
        {
            return GetActiveFiltersUnlocked(excludeSourceWidgetId);
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private List<WidgetFilter> GetActiveFiltersUnlocked(Guid? excludeSourceWidgetId)
    {
        return _activeFilters
            .Where(kvp => excludeSourceWidgetId is null || kvp.Value.SourceWidgetId != excludeSourceWidgetId)
            .Select(kvp => new WidgetFilter
            {
                FieldName = kvp.Key,
                Operator = kvp.Value.Values.Count > 1 ? FilterOperator.In : FilterOperator.Equals,
                Values = [.. kvp.Value.Values],
                IsEnabled = true
            })
            .ToList();
    }

    private sealed record CrossFilterEntry(Guid SourceWidgetId, List<string> Values);
}
