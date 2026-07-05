using Microsoft.Extensions.Logging;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Services;

/// <summary>One level of drill state for a single widget — the field drilled into and the value clicked.</summary>
public sealed record DrillLevel(string DimensionField, string ClickedValue);

/// <summary>
/// Tracks per-widget drill-down state: clicking a category can temporarily replace
/// a widget's dimension with a more granular one (drill down) filtered to the
/// clicked value, or navigate to a different, related widget/dashboard (drill
/// through). This implementation covers drill-down (same widget, deeper dimension)
/// via a per-widget undo-able stack; drill-through (cross-dashboard navigation) is
/// intentionally out of scope here since it depends on Phase 10/15 dashboard
/// linking that doesn't exist yet — <see cref="DrillThrough"/> is a documented stub.
/// </summary>
public sealed class DrillManager
{
    private readonly ILogger<DrillManager> _logger;
    private readonly Dictionary<Guid, Stack<DrillLevel>> _stacksByWidget = new();

    /// <summary>Initialises the drill manager with a logger.</summary>
    public DrillManager(ILogger<DrillManager> logger)
    {
        _logger = logger;
    }

    /// <summary>Whether the given widget currently has an active drill-down level.</summary>
    public bool CanDrillUp(Guid widgetId) => _stacksByWidget.TryGetValue(widgetId, out var s) && s.Count > 0;

    /// <summary>
    /// Drills a widget down: pushes the current dimension onto the stack, swaps the
    /// widget's dimension to <paramref name="nextDimensionField"/>, and adds a local
    /// filter pinning it to the clicked value.
    /// </summary>
    public void DrillDown(DashboardWidget widget, string nextDimensionField, string clickedValue)
    {
        var currentDim = widget.DimensionFields.FirstOrDefault() ?? string.Empty;

        if (!_stacksByWidget.TryGetValue(widget.Id, out var stack))
        {
            stack = new Stack<DrillLevel>();
            _stacksByWidget[widget.Id] = stack;
        }
        stack.Push(new DrillLevel(currentDim, clickedValue));

        widget.LocalFilters.Add(new WidgetFilter
        {
            FieldName = currentDim,
            Operator = FilterOperator.Equals,
            Values = [clickedValue]
        });

        widget.DimensionFields.Clear();
        widget.DimensionFields.Add(nextDimensionField);

        _logger.LogInformation("Widget '{Title}' drilled down: {From} = '{Value}' → {To}.",
            widget.Title, currentDim, clickedValue, nextDimensionField);
    }

    /// <summary>Drills a widget back up one level, restoring its previous dimension and removing the pinning filter.</summary>
    public void DrillUp(DashboardWidget widget)
    {
        if (!_stacksByWidget.TryGetValue(widget.Id, out var stack) || stack.Count == 0) return;

        var level = stack.Pop();

        widget.LocalFilters.RemoveAll(f => f.FieldName == level.DimensionField && f.Values.Contains(level.ClickedValue));
        widget.DimensionFields.Clear();
        widget.DimensionFields.Add(level.DimensionField);

        _logger.LogInformation("Widget '{Title}' drilled up to '{Field}'.", widget.Title, level.DimensionField);
    }

    /// <summary>
    /// Drill-through: navigate to a related dashboard/widget carrying the clicked
    /// value as a filter. Not implemented — depends on dashboard-to-dashboard
    /// linking, which doesn't exist in the model yet. Throws to make the gap explicit
    /// rather than silently doing nothing.
    /// </summary>
    public void DrillThrough(DashboardWidget sourceWidget, Guid targetDashboardId, string clickedValue) =>
        throw new NotSupportedException(
            "Drill-through requires dashboard-to-dashboard navigation, which is not yet part of the Dashboard model. " +
            "Add a target-dashboard link concept before implementing this.");

    /// <summary>Clears all drill state for a widget (e.g. when the widget is deleted).</summary>
    public void Reset(Guid widgetId) => _stacksByWidget.Remove(widgetId);
}
