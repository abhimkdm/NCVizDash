using System.IO;
using Microsoft.Extensions.Configuration;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;

namespace NCVizDash.Infrastructure.Configuration;

/// <summary>
/// Loads <see cref="AppSettings"/> from a JSON file stored in the user's
/// %LOCALAPPDATA%\NCVizDash folder.  Falls back to defaults if the file is absent.
/// </summary>
public sealed class JsonAppSettingsProvider : IAppSettingsProvider
{
    private readonly string _settingsPath;
    private AppSettings _settings;

    /// <inheritdoc/>
    public AppSettings Settings => _settings;

    /// <summary>Initialises the provider and performs the first load.</summary>
    public JsonAppSettingsProvider()
    {
        var appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NCVizDash");
        Directory.CreateDirectory(appDir);

        _settingsPath = Path.Combine(appDir, "ncvizdash.json");
        _settings = new AppSettings();

        EnsureDefaultFileExists();
        Reload();
    }

    
   /// <inheritdoc/>
    public void Reload()
    {
        try
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(_settingsPath, optional: true, reloadOnChange: false)
                .Build();

            var loaded = new AppSettings();
            config.Bind(loaded);
            _settings = loaded;
        }
        catch
        {
            // Keep the existing / default settings if the file is malformed.
        }
    }

    /// <inheritdoc/>
    public void Save()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(_settings,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }

    // ── private ───────────────────────────────────────────────────────────

    private void EnsureDefaultFileExists()
    {
        if (File.Exists(_settingsPath)) return;

        // Write a commented default settings file so users can discover options.
        const string defaultJson = """
{
  "DefaultTheme": "Light",
  "LogLevel": "Information",
  "LogDirectory": "%LOCALAPPDATA%\\NCVizDash\\Logs",
  "RecentDashboardsMax": 10,
  "AutoRefreshSeconds": 0,
  "GridSnapColumns": 1,
  "ShowAlignmentGuides": true,
  "MaxIngestRows": 1000000,
  "TelemetryEnabled": false,
  "PluginDirectory": "%LOCALAPPDATA%\\NCVizDash\\Plugins"
}
""";
        File.WriteAllText(_settingsPath, defaultJson);
    }
}
