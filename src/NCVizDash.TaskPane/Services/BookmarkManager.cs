using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Services;

/// <summary>
/// Captures and restores <see cref="Bookmark"/>s — named snapshots of the active
/// dashboard's global filter state. Bookmarks are stored on the dashboard itself
/// (<see cref="Dashboard.GlobalFilters"/> is what gets snapshotted; the bookmark
/// list is stored via <see cref="BookmarksOf"/>, a small in-memory index keyed by
/// dashboard — Phase 10 persistence naturally covers bookmarks too if they're
/// added to the Dashboard model's serialized shape in a later revision).
/// </summary>
public sealed class BookmarkManager
{
    private readonly ILogger<BookmarkManager> _logger;
    private readonly IGlobalFilterManager _globalFilterManager;
    private readonly Dictionary<Guid, List<Bookmark>> _bookmarksByDashboard = new();

    public BookmarkManager(ILogger<BookmarkManager> logger, IGlobalFilterManager globalFilterManager)
    {
        _logger = logger;
        _globalFilterManager = globalFilterManager;
    }

    /// <summary>All bookmarks saved for the given dashboard.</summary>
    public IReadOnlyList<Bookmark> BookmarksOf(Guid dashboardId) =>
        _bookmarksByDashboard.TryGetValue(dashboardId, out var list) ? list : [];

    /// <summary>Captures the active dashboard's current global filter state as a new named bookmark.</summary>
    public Bookmark Capture(Guid dashboardId, string name)
    {
        var bookmark = new Bookmark
        {
            Name = name,
            GlobalFilters = _globalFilterManager.GetFilters()
                .Select(f => new WidgetFilter
                {
                    FieldName = f.FieldName, Operator = f.Operator,
                    Values = [.. f.Values], IsEnabled = f.IsEnabled
                })
                .ToList()
        };

        if (!_bookmarksByDashboard.TryGetValue(dashboardId, out var list))
        {
            list = [];
            _bookmarksByDashboard[dashboardId] = list;
        }
        list.Add(bookmark);

        _logger.LogInformation("Bookmark '{Name}' captured for dashboard {DashboardId}.", name, dashboardId);
        return bookmark;
    }

    /// <summary>Restores a bookmark's filter state onto the active dashboard.</summary>
    public void Restore(Bookmark bookmark)
    {
        _globalFilterManager.ClearAll();
        foreach (var filter in bookmark.GlobalFilters)
            _globalFilterManager.AddOrUpdateFilter(filter);

        _logger.LogInformation("Bookmark '{Name}' restored.", bookmark.Name);
    }

    /// <summary>Removes a bookmark.</summary>
    public void Remove(Guid dashboardId, Guid bookmarkId)
    {
        if (_bookmarksByDashboard.TryGetValue(dashboardId, out var list))
            list.RemoveAll(b => b.Id == bookmarkId);
    }
}
