using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Services;

/// <summary>
/// Dashboard sharing (Phase 15): export a dashboard to a standalone <c>.ncvdash.json</c>
/// file another user can import into their own workbook. This is file-based sharing
/// (no server/cloud component exists in this offline-first product), and a version
/// history (Phase 15's other requirement) built on top of the same export format —
/// each save can optionally be snapshotted to a version list capped at a max count.
/// </summary>
public sealed class DashboardShareService
{
    private readonly ILogger<DashboardShareService> _logger;
    private readonly Dictionary<Guid, List<(DateTimeOffset SavedAt, string Json)>> _versionHistory = new();
    private const int MaxVersionsPerDashboard = 20;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Initialises the dashboard share service with a logger.</summary>
    public DashboardShareService(ILogger<DashboardShareService> logger)
    {
        _logger = logger;
    }

    /// <summary>Exports a dashboard to a JSON file for sharing with another user.</summary>
    public async Task ExportToFileAsync(Dashboard dashboard, string filePath, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(dashboard, JsonOptions);
        ct.ThrowIfCancellationRequested();
        using (var writer = new StreamWriter(filePath, append: false))
            await writer.WriteAsync(json);
        _logger.LogInformation("Dashboard '{Name}' exported to '{Path}'.", dashboard.Name, filePath);
    }

    /// <summary>
    /// Imports a dashboard from a shared JSON file. The imported dashboard gets a
    /// fresh <see cref="Dashboard.Id"/> (so it doesn't collide with the sender's copy
    /// if both end up in the same workbook) and its <see cref="Dashboard.SharedBy"/>
    /// set from the optional <paramref name="sharedBy"/> parameter.
    /// </summary>
    public async Task<Dashboard?> ImportFromFileAsync(string filePath, string? sharedBy = null, CancellationToken ct = default)
    {
        try
        {
            string json;
            using (var reader = new StreamReader(filePath))
                json = await reader.ReadToEndAsync();
            ct.ThrowIfCancellationRequested();

            var dashboard = JsonSerializer.Deserialize<Dashboard>(json, JsonOptions);
            if (dashboard is null) return null;

            var imported = new Dashboard
            {
                Name = dashboard.Name,
                Description = dashboard.Description,
                TemplateName = dashboard.TemplateName,
                Theme = dashboard.Theme,
                GridColumns = dashboard.GridColumns,
                GridRowHeight = dashboard.GridRowHeight,
                Widgets = dashboard.Widgets,
                GlobalFilters = dashboard.GlobalFilters,
                SharedBy = sharedBy ?? "Imported"
            };

            _logger.LogInformation("Dashboard '{Name}' imported from '{Path}'.", imported.Name, filePath);
            return imported;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import dashboard from '{Path}'.", filePath);
            return null;
        }
    }

    // ── Version history ───────────────────────────────────────────────────────

    /// <summary>Captures the current state of a dashboard as a new version snapshot.</summary>
    public void CaptureVersion(Dashboard dashboard)
    {
        if (!_versionHistory.TryGetValue(dashboard.Id, out var list))
        {
            list = [];
            _versionHistory[dashboard.Id] = list;
        }

        list.Add((DateTimeOffset.UtcNow, JsonSerializer.Serialize(dashboard, JsonOptions)));
        if (list.Count > MaxVersionsPerDashboard)
            list.RemoveAt(0);
    }

    /// <summary>Returns every saved version's timestamp for a dashboard, most recent first.</summary>
    public IReadOnlyList<DateTimeOffset> GetVersionTimestamps(Guid dashboardId) =>
        _versionHistory.TryGetValue(dashboardId, out var list)
            ? list.Select(v => v.SavedAt).OrderByDescending(t => t).ToList()
            : [];

    /// <summary>Restores a dashboard to the state it was in at the given version timestamp.</summary>
    public Dashboard? RestoreVersion(Guid dashboardId, DateTimeOffset versionTimestamp)
    {
        if (!_versionHistory.TryGetValue(dashboardId, out var list)) return null;

        var match = list.FirstOrDefault(v => v.SavedAt == versionTimestamp);
        return match.Json is null ? null : JsonSerializer.Deserialize<Dashboard>(match.Json, JsonOptions);
    }
}
