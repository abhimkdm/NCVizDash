using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Services;

/// <summary>
/// Manages dashboard-wide filters for whichever <see cref="Dashboard"/> is currently
/// open. The filter list itself lives on <see cref="Dashboard.GlobalFilters"/> — this
/// class is a thin, testable coordinator around mutating that list and notifying
/// subscribers, not a separate store. That keeps the filters naturally persisted
/// whenever the dashboard itself is saved (Phase 10), with no separate sync step.
/// </summary>
public sealed class GlobalFilterManager : IGlobalFilterManager
{
    private readonly ILogger<GlobalFilterManager> _logger;

    /// <inheritdoc/>
    public event EventHandler? FiltersChanged;

    /// <inheritdoc/>
    public Dashboard? ActiveDashboard { get; private set; }

    /// <summary>Initialises the manager with no dashboard bound.</summary>
    public GlobalFilterManager(ILogger<GlobalFilterManager> logger)
    {
        _logger = logger;
    }

    // ── IGlobalFilterManager ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public void SetDashboard(Dashboard? dashboard)
    {
        if (ReferenceEquals(ActiveDashboard, dashboard)) return;

        ActiveDashboard = dashboard;
        _logger.LogDebug("Global filter manager bound to dashboard '{Name}'.", dashboard?.Name ?? "(none)");
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public IReadOnlyList<WidgetFilter> GetFilters() =>
        ActiveDashboard?.GlobalFilters.AsReadOnly() ?? (IReadOnlyList<WidgetFilter>)Array.Empty<WidgetFilter>();

    /// <inheritdoc/>
    public IReadOnlyList<WidgetFilter> GetEnabledFilters() =>
        ActiveDashboard?.GlobalFilters.Where(f => f.IsEnabled).ToList() ?? [];

    /// <inheritdoc/>
    public void AddOrUpdateFilter(WidgetFilter filter)
    {
        if (ActiveDashboard is null)
        {
            _logger.LogWarning("AddOrUpdateFilter called with no active dashboard; ignored.");
            return;
        }

        var existingIndex = ActiveDashboard.GlobalFilters.FindIndex(f => f.Id == filter.Id);
        if (existingIndex >= 0)
            ActiveDashboard.GlobalFilters[existingIndex] = filter;
        else
            ActiveDashboard.GlobalFilters.Add(filter);

        ActiveDashboard.ModifiedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Global filter on '{Field}' {Action}.", filter.FieldName, existingIndex >= 0 ? "updated" : "added");

        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public void RemoveFilter(Guid filterId)
    {
        if (ActiveDashboard is null) return;

        var removed = ActiveDashboard.GlobalFilters.RemoveAll(f => f.Id == filterId) > 0;
        if (!removed) return;

        ActiveDashboard.ModifiedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Global filter {FilterId} removed.", filterId);
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public void SetFilterEnabled(Guid filterId, bool enabled)
    {
        var filter = ActiveDashboard?.GlobalFilters.FirstOrDefault(f => f.Id == filterId);
        if (filter is null || filter.IsEnabled == enabled) return;

        filter.IsEnabled = enabled;
        ActiveDashboard!.ModifiedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Global filter {FilterId} {State}.", filterId, enabled ? "enabled" : "disabled");
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public void ClearAll()
    {
        if (ActiveDashboard is null || ActiveDashboard.GlobalFilters.Count == 0) return;

        ActiveDashboard.GlobalFilters.Clear();
        ActiveDashboard.ModifiedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("All global filters cleared.");
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }
}
