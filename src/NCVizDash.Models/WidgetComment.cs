namespace NCVizDash.Models;

/// <summary>A single comment thread entry attached to a widget.</summary>
public sealed class WidgetComment
{
    /// <summary>Unique comment ID.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Name or identifier of whoever wrote the comment.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>The comment body.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the comment was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Whether this comment has been marked resolved.</summary>
    public bool IsResolved { get; set; }
}
