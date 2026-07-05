using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Services;

/// <summary>
/// Snapshot-based undo/redo for the active dashboard's widget layout. Simpler than
/// a full command pattern: every mutating canvas operation (add/delete/move/resize/
/// duplicate) pushes a JSON snapshot of the widget list *before* the change; Undo
/// restores the previous snapshot and pushes the current state onto the redo stack.
/// Snapshot-based undo trades a little memory for a lot less code — reasonable at
/// dashboard scale (tens of widgets, not thousands).
/// </summary>
public sealed class UndoRedoManager
{
    private readonly ILogger<UndoRedoManager> _logger;
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();
    private const int MaxStackDepth = 50;

    private static readonly JsonSerializerOptions JsonOptions = new();

    /// <summary>Initialises the undo/redo manager with a logger.</summary>
    public UndoRedoManager(ILogger<UndoRedoManager> logger)
    {
        _logger = logger;
    }

    /// <summary>True when at least one undo snapshot is available.</summary>
    public bool CanUndo => _undoStack.Count > 0;
    /// <summary>True when at least one redo snapshot is available.</summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Raised whenever CanUndo/CanRedo may have changed, for toolbar button binding.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Call before making a mutating change to the dashboard's widget list.</summary>
    public void RecordSnapshot(Dashboard dashboard)
    {
        var snapshot = JsonSerializer.Serialize(dashboard.Widgets, JsonOptions);
        _undoStack.Push(snapshot);
        if (_undoStack.Count > MaxStackDepth)
        {
            // Drop the oldest entry — Stack doesn't support trimming from the bottom
            // directly, so rebuild via a temporary array (cheap at this bounded size).
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (var i = items.Length - 2; i >= 0; i--) _undoStack.Push(items[i]);
        }

        _redoStack.Clear(); // a new action invalidates the redo history
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Restores the most recent snapshot, if any. Returns the restored widget list, or null if nothing to undo.</summary>
    public List<DashboardWidget>? Undo(Dashboard dashboard)
    {
        if (_undoStack.Count == 0) return null;

        var current = JsonSerializer.Serialize(dashboard.Widgets, JsonOptions);
        _redoStack.Push(current);

        var previous = _undoStack.Pop();
        var restored = JsonSerializer.Deserialize<List<DashboardWidget>>(previous, JsonOptions);

        _logger.LogInformation("Undo applied; {Remaining} undo step(s) remain.", _undoStack.Count);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return restored;
    }

    /// <summary>Re-applies the most recently undone snapshot, if any.</summary>
    public List<DashboardWidget>? Redo(Dashboard dashboard)
    {
        if (_redoStack.Count == 0) return null;

        var current = JsonSerializer.Serialize(dashboard.Widgets, JsonOptions);
        _undoStack.Push(current);

        var next = _redoStack.Pop();
        var restored = JsonSerializer.Deserialize<List<DashboardWidget>>(next, JsonOptions);

        _logger.LogInformation("Redo applied; {Remaining} redo step(s) remain.", _redoStack.Count);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return restored;
    }

    /// <summary>Clears all undo/redo history — call when switching to a different dashboard.</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
