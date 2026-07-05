using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NCVizDash.Connectors.Jira;

/// <summary>Persists <see cref="JiraConnectionProfile"/>s to a JSON file under %LOCALAPPDATA%\NCVizDash.</summary>
public sealed class JiraConnectionProfileStore
{
    private readonly ILogger<JiraConnectionProfileStore> _logger;
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Initializes a new instance of the <see cref="JiraConnectionProfileStore"/> class.</summary>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public JiraConnectionProfileStore(ILogger<JiraConnectionProfileStore> logger)
    {
        _logger = logger;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NCVizDash");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "jira-connections.json");
    }

    /// <summary>Loads every saved Jira connection profile, or an empty list if none exist or the file is unreadable.</summary>
    public List<JiraConnectionProfile> LoadAll()
    {
        if (!File.Exists(_filePath)) return [];

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<JiraConnectionProfile>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Jira connection profiles; starting with an empty list.");
            return [];
        }
    }

    /// <summary>Overwrites the saved profile list with <paramref name="profiles"/>.</summary>
    /// <param name="profiles">The complete set of profiles to persist.</param>
    public void SaveAll(IReadOnlyList<JiraConnectionProfile> profiles)
    {
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        File.WriteAllText(_filePath, json);
        _logger.LogInformation("Saved {Count} Jira connection profile(s).", profiles.Count);
    }
}
