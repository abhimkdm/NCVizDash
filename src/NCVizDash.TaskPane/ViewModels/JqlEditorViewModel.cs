using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NCVizDash.Connectors.Jira;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;
using System.Collections.ObjectModel;

namespace NCVizDash.TaskPane.ViewModels;

/// <summary>Import mode for a JQL query result, matching the v2.0 "Query Preview" spec.</summary>
public enum JqlImportMode { NewDataset, ReplaceDataset, AppendDataset }

/// <summary>
/// Drives the Dynamic JQL Editor (v2.0 Feature 5): connection management, JQL
/// validation, execution, a 100-row preview, and import into the same
/// <see cref="IAnalyticsEngine"/>/<see cref="ExplorerPanelViewModel.DataSources"/>
/// pipeline Excel data already uses — after import, a Jira dataset is
/// indistinguishable from an Excel one anywhere else in the app.
/// </summary>
public sealed partial class JqlEditorViewModel : ObservableObject
{
    private readonly ILogger<JqlEditorViewModel> _logger;
    private readonly JiraConnector _jiraConnector;
    private readonly JiraConnectionProfileStore _profileStore;
    private readonly IAnalyticsEngine _analyticsEngine;
    private readonly ExplorerPanelViewModel _explorerPanel;

    public ObservableCollection<JiraConnectionProfile> Connections { get; } = [];

    [ObservableProperty] private JiraConnectionProfile? _selectedConnection;
    [ObservableProperty] private string _jqlText = string.Empty;
    [ObservableProperty] private string? _validationError;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    /// <summary>First 100 preview rows, populated by <see cref="RunPreviewAsync"/>.</summary>
    public ObservableCollection<IReadOnlyDictionary<string, object?>> PreviewRows { get; } = [];

    [ObservableProperty] private IReadOnlyList<string> _previewColumns = [];
    [ObservableProperty] private int _previewRecordCount;

    public JqlEditorViewModel(
        ILogger<JqlEditorViewModel> logger,
        JiraConnector jiraConnector,
        JiraConnectionProfileStore profileStore,
        IAnalyticsEngine analyticsEngine,
        ExplorerPanelViewModel explorerPanel)
    {
        _logger = logger;
        _jiraConnector = jiraConnector;
        _profileStore = profileStore;
        _analyticsEngine = analyticsEngine;
        _explorerPanel = explorerPanel;

        ReloadConnections();
    }

    // ── Connection management ────────────────────────────────────────────────

    public void ReloadConnections()
    {
        Connections.Clear();
        foreach (var profile in _profileStore.LoadAll())
            Connections.Add(profile);
    }

    [RelayCommand]
    public void SaveConnection(JiraConnectionProfile profile)
    {
        var all = _profileStore.LoadAll();
        var existingIndex = all.FindIndex(p => p.Id == profile.Id);
        if (existingIndex >= 0) all[existingIndex] = profile;
        else all.Add(profile);

        _profileStore.SaveAll(all);
        ReloadConnections();
        StatusMessage = $"Connection '{profile.ConnectionName}' saved.";
    }

    [RelayCommand]
    public async Task TestConnectionAsync(JiraConnectionProfile profile)
    {
        IsBusy = true;
        try
        {
            var error = await _jiraConnector.TestConnectionAsync(profile);
            StatusMessage = error is null ? "Connection successful." : error;
        }
        finally { IsBusy = false; }
    }

    // ── JQL validation + execution ────────────────────────────────────────────

    [RelayCommand]
    public async Task ValidateJqlAsync()
    {
        if (SelectedConnection is null || string.IsNullOrWhiteSpace(JqlText)) return;

        IsBusy = true;
        try
        {
            ValidationError = await _jiraConnector.ValidateJqlAsync(SelectedConnection, JqlText);
        }
        finally { IsBusy = false; }
    }

    /// <summary>Runs the JQL query and populates <see cref="PreviewRows"/> with up to 100 rows (v2.0 "Query Preview").</summary>
    [RelayCommand]
    public async Task RunPreviewAsync()
    {
        if (SelectedConnection is null || string.IsNullOrWhiteSpace(JqlText)) return;

        IsBusy = true;
        PreviewRows.Clear();
        try
        {
            var rows = await _jiraConnector.ExecuteJqlAsync(SelectedConnection, JqlText, maxResults: 100);

            foreach (var row in rows)
                PreviewRows.Add(row);

            PreviewColumns = rows.SelectMany(r => r.Keys).Distinct().ToList();
            PreviewRecordCount = rows.Count;
            StatusMessage = $"Preview: {rows.Count} record(s), {PreviewColumns.Count} column(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JQL preview failed.");
            StatusMessage = $"Preview failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    /// <summary>Saves the current JQL text into the connection's favourite-queries list.</summary>
    [RelayCommand]
    public void SaveFavoriteQuery()
    {
        if (SelectedConnection is null || string.IsNullOrWhiteSpace(JqlText)) return;
        if (SelectedConnection.FavoriteQueries.Contains(JqlText)) return;

        SelectedConnection.FavoriteQueries.Add(JqlText);
        SaveConnection(SelectedConnection);
    }

    // ── Import (v2.0 "Dashboard Integration") ────────────────────────────────

    /// <summary>
    /// Imports the full query result (not just the preview) into the analytics
    /// engine and the Explorer's data source list, exactly as Excel data is —
    /// from this point on, the dashboard engine cannot tell it apart from an
    /// Excel Table.
    /// </summary>
    [RelayCommand]
    public async Task ImportAsync(JqlImportMode mode)
    {
        if (SelectedConnection is null || string.IsNullOrWhiteSpace(JqlText)) return;

        IsBusy = true;
        try
        {
            var connectionInfo = $"{SelectedConnection.Id}||{JqlText}";
            var descriptors = await _jiraConnector.DiscoverAsync(connectionInfo);
            var descriptor = descriptors.FirstOrDefault();
            if (descriptor is null)
            {
                StatusMessage = "No data returned — nothing to import.";
                return;
            }

            var rows = await _jiraConnector.ReadRowsAsync(descriptor, connectionInfo);

            switch (mode)
            {
                case JqlImportMode.NewDataset:
                    await _analyticsEngine.LoadDataSourceAsync(descriptor, rows);
                    _explorerPanel.DataSources.Add(descriptor);
                    break;

                case JqlImportMode.ReplaceDataset:
                    var existing = _explorerPanel.DataSources.FirstOrDefault(d => d.SourceType == "Jira" && d.Name == descriptor.Name);
                    if (existing is not null)
                    {
                        await _analyticsEngine.UnloadDataSourceAsync(existing.Id);
                        _explorerPanel.DataSources.Remove(existing);
                    }
                    await _analyticsEngine.LoadDataSourceAsync(descriptor, rows);
                    _explorerPanel.DataSources.Add(descriptor);
                    break;

                case JqlImportMode.AppendDataset:
                    // Appending to an in-memory DuckDB table isn't exposed by
                    // IAnalyticsEngine today (LoadDataSourceAsync always
                    // replaces); a true append would need a new
                    // IAnalyticsEngine.AppendRowsAsync method. Documented gap —
                    // falls back to Replace semantics rather than silently
                    // dropping the request.
                    _logger.LogWarning("AppendDataset requested but not supported by IAnalyticsEngine yet; falling back to Replace semantics.");
                    goto case JqlImportMode.ReplaceDataset;
            }

            StatusMessage = $"Imported {rows.Count} row(s) as '{descriptor.Name}'.";
            _logger.LogInformation("Jira import complete: '{Name}', {Count} row(s), mode={Mode}.", descriptor.Name, rows.Count, mode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jira import failed.");
            StatusMessage = $"Import failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }
}
