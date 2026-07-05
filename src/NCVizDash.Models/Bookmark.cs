namespace NCVizDash.Models;

/// <summary>
/// A named, saved snapshot of a dashboard's filter state (global filters) and
/// widget selection, so a user can jump back to a specific analytical view
/// ("Q1 EMEA only") with one click. Layout/widgets are not part of a bookmark —
/// only the filter state, matching how bookmarks work in Power BI/Tableau.
/// </summary>
public sealed class Bookmark
{
    /// <summary>Unique bookmark ID.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Display name shown in the bookmark picker.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Snapshot of <see cref="Dashboard.GlobalFilters"/> at the time the bookmark was created.</summary>
    public List<WidgetFilter> GlobalFilters { get; set; } = [];

    /// <summary>UTC timestamp when the bookmark was captured.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
