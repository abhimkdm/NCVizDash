namespace NCVizDash.Connectors.Jira;

/// <summary>
/// A saved Jira connection ("Data Sources → Jira" in the v2.0 spec). Stored as
/// plain JSON in the user's settings directory alongside <c>AppSettings</c> —
/// the same honest caveat as Phase 18's AI API key: this is a reasonable
/// placeholder for a desktop tool, not production-grade secret storage (an OS
/// credential vault would be the real answer before shipping to non-developer
/// users). The API token is the only credential type implemented; OAuth would
/// need a full authorization-code-with-PKCE flow and a redirect listener, which
/// is out of scope here for the same reason SharePoint's OAuth was in Phase 14.
/// </summary>
public sealed class JiraConnectionProfile
{
    /// <summary>Unique identifier for this saved connection.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Display name shown in the connection picker.</summary>
    public string ConnectionName { get; set; } = string.Empty;

    /// <summary>Base URL of the Jira Cloud site (e.g. <c>https://your-domain.atlassian.net</c>).</summary>
    public string JiraUrl { get; set; } = string.Empty;

    /// <summary>Email address of the Jira account used for authentication.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>API token used alongside <see cref="Email"/> for HTTP Basic auth.</summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>User-saved JQL queries for quick reuse.</summary>
    public List<string> FavoriteQueries { get; set; } = [];
}
