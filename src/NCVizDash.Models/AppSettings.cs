namespace NCVizDash.Models;

/// <summary>Top-level application settings loaded from ncvizdash.json.</summary>
public sealed class AppSettings
{
    /// <summary>Default colour theme ("Light" or "Dark").</summary>
    public string DefaultTheme { get; set; } = "Light";

    /// <summary>Log level ("Verbose", "Debug", "Information", "Warning", "Error").</summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>Path to write Serilog rolling log files.</summary>
    public string LogDirectory { get; set; } = "%LOCALAPPDATA%\\NCVizDash\\Logs";

    /// <summary>Maximum number of dashboards kept in recent history.</summary>
    public int RecentDashboardsMax { get; set; } = 10;

    /// <summary>Auto-refresh interval in seconds (0 = disabled).</summary>
    public int AutoRefreshSeconds { get; set; } = 0;

    /// <summary>Grid snap size in columns.</summary>
    public int GridSnapColumns { get; set; } = 1;

    /// <summary>Whether alignment guides are shown while dragging.</summary>
    public bool ShowAlignmentGuides { get; set; } = true;

    /// <summary>Maximum rows DuckDB will ingest in a single load.</summary>
    public long MaxIngestRows { get; set; } = 1_000_000;

    /// <summary>Whether telemetry / anonymous usage stats are enabled.</summary>
    public bool TelemetryEnabled { get; set; } = false;

    /// <summary>Plugin directory scanned at startup.</summary>
    public string PluginDirectory { get; set; } = "%LOCALAPPDATA%\\NCVizDash\\Plugins";

    // ── Optional AI (Phase 18) ────────────────────────────────────────────────
    // Defaults are deliberately OFF/empty: "AI must always remain optional" per
    // the product vision. No code path in this application calls IAiProvider
    // unless AiEnabled is explicitly set to true AND AiProvider names a configured provider.

    /// <summary>Master switch for every AI feature. Defaults to false — AI is opt-in, never required.</summary>
    public bool AiEnabled { get; set; } = false;

    /// <summary>Which provider to use when AI is enabled: "azure-openai", "openai", "anthropic", or "local".</summary>
    public string AiProvider { get; set; } = string.Empty;

    /// <summary>API endpoint for the selected provider (required for azure-openai and local; ignored for openai/anthropic's default endpoints).</summary>
    public string AiEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// API key for the selected provider. Stored in the same user-scoped settings
    /// file as everything else in <see cref="AppSettings"/> (%LOCALAPPDATA%) —
    /// consider OS credential-store storage instead before shipping this to
    /// non-developer users; plain-JSON API key storage is a reasonable placeholder
    /// for this phase but not a production-grade secret-storage choice.
    /// </summary>
    public string AiApiKey { get; set; } = string.Empty;
}
