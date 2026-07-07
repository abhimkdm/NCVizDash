using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;
using NCVizDash.TaskPane.Services;
using System.Collections.ObjectModel;

namespace NCVizDash.TaskPane.ViewModels;

/// <summary>
/// Root ViewModel for the task pane shell.
/// Hosts the three main panels: Explorer, Canvas, Visual Library. Owns dashboard
/// lifecycle commands (New/Open/Save/Delete) backed by <see cref="IDashboardRepository"/>.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    // ── Services ─────────────────────────────────────────────────────────────
    private readonly ILogger<ShellViewModel> _logger;
    private readonly IAppSettingsProvider _settings;
    private readonly ThemeService _themeService;
    private readonly IDashboardRepository _dashboardRepository;
    private readonly NCVizDash.TaskPane.Templates.TemplateInstantiationService _templateService;
    private readonly NCVizDash.TaskPane.Generation.OneClickDashboardGenerator _dashboardGenerator;

    // ── Observable properties ─────────────────────────────────────────────────

    [ObservableProperty]
    private string _activeTheme = "Light";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private ExplorerPanelViewModel _explorerPanel;

    [ObservableProperty]
    private CanvasPanelViewModel _canvasPanel;

    [ObservableProperty]
    private VisualLibraryViewModel _visualLibrary;

    /// <summary>Dashboards currently saved in the workbook, most-recently-modified first.</summary>
    public ObservableCollection<Dashboard> SavedDashboards { get; } = [];

    /// <summary>All available dashboard templates (Phase 11), for a picker UI to bind against.</summary>
    public IReadOnlyList<NCVizDash.TaskPane.Templates.DashboardTemplate> AvailableTemplates =>
        NCVizDash.TaskPane.Templates.TemplateRegistry.All;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Initialises the shell with all child ViewModels.</summary>
    public ShellViewModel(
        ILogger<ShellViewModel> logger,
        IAppSettingsProvider settings,
        ThemeService themeService,
        IDashboardRepository dashboardRepository,
        NCVizDash.TaskPane.Templates.TemplateInstantiationService templateService,
        NCVizDash.TaskPane.Generation.OneClickDashboardGenerator dashboardGenerator,
        ExplorerPanelViewModel explorerPanel,
        CanvasPanelViewModel canvasPanel,
        VisualLibraryViewModel visualLibrary)
    {
        _logger = logger;
        _settings = settings;
        _themeService = themeService;
        _dashboardRepository = dashboardRepository;
        _templateService = templateService;
        _dashboardGenerator = dashboardGenerator;
        _explorerPanel = explorerPanel;
        _canvasPanel = canvasPanel;
        _visualLibrary = visualLibrary;
        _explorerPanel.GenerateDashboardForSource = GenerateDashboardAsync;
        _canvasPanel.ExplorerPanel = _explorerPanel;
        _canvasPanel.ResolveDataSourceId = id => id != Guid.Empty
            ? id
            : _explorerPanel.DataSources.FirstOrDefault()?.Id ?? Guid.Empty;

        ActiveTheme = settings.Settings.DefaultTheme;
        CanvasPanel.ActiveTheme = ActiveTheme;
        _themeService.ApplyTheme(ActiveTheme);
        _logger.LogInformation("ShellViewModel initialised.");
    }

    // ── Theme / data refresh ──────────────────────────────────────────────────

    /// <summary>Applies a new theme across the entire task pane.</summary>
    [RelayCommand]
    public void ApplyTheme(string theme)
    {
        ActiveTheme = theme;
        _themeService.ApplyTheme(theme);
        CanvasPanel.ActiveTheme = theme;
        _logger.LogInformation("Theme switched to '{Theme}'.", theme);
    }

    /// <summary>Triggers a full data reload from Excel into the analytics engine.</summary>
    [RelayCommand]
    public async Task RefreshDataAsync()
    {
        IsLoading = true;
        StatusMessage = "Refreshing data…";
        _logger.LogInformation("Data refresh requested from ShellViewModel.");

        try
        {
            await ExplorerPanel.LoadDataSourcesAsync();
            CanvasPanel.GlobalFilterBar.RefreshAvailableFields(ExplorerPanel.DataSources);
            CanvasPanel.RequestRenderAllWidgets();
            StatusMessage = $"Refreshed at {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data refresh failed.");
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── One-Click Dashboard Generator (v2.0 Feature 1) ───────────────────────

    /// <summary>
    /// Scans the given (or first loaded) data source and deterministically builds
    /// a complete dashboard: KPI cards, trend, category analysis, Top 10/Bottom 10,
    /// pie, and summary table — no AI, no configuration.
    /// </summary>
    [RelayCommand]
    public async Task GenerateDashboardAsync(DataSourceDescriptor? source = null)
    {
        var target = source ?? ExplorerPanel.DataSources.FirstOrDefault();
        if (target is null)
        {
            StatusMessage = "No data source loaded — refresh data before generating a dashboard.";
            return;
        }

        IsLoading = true;
        StatusMessage = $"Generating dashboard from '{target.Name}'…";
        _logger.LogInformation("One-click dashboard generation started for '{Source}'.", target.Name);

        try
        {
            await ExplorerPanel.EnsureDataSourceLoadedAsync(target);

            var dashboard = _dashboardGenerator.Generate(target);
            CanvasPanel.OpenDashboard(dashboard);
            StatusMessage = $"Generated dashboard with {dashboard.Widgets.Count} widget(s) from '{target.Name}'.";
            _logger.LogInformation("One-click dashboard generated from '{Source}'.", target.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard generation failed for '{Source}'.", target.Name);
            StatusMessage = $"Generate failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Templates (Phase 11) ──────────────────────────────────────────────────

    /// <summary>
    /// Instantiates the given template against a data source — defaults to the
    /// first loaded data source if none is specified — and opens the resulting
    /// dashboard on the canvas.
    /// </summary>
    [RelayCommand]
    public void ApplyTemplate((NCVizDash.TaskPane.Templates.DashboardTemplate Template, DataSourceDescriptor? Source) args)
    {
        var source = args.Source ?? ExplorerPanel.DataSources.FirstOrDefault();
        if (source is null)
        {
            StatusMessage = "No data source loaded — refresh data before applying a template.";
            return;
        }

        var dashboard = _templateService.Instantiate(args.Template, source);
        CanvasPanel.OpenDashboard(dashboard);
        StatusMessage = $"Applied template '{args.Template.Name}' ({dashboard.Widgets.Count} widget(s) created).";
        _logger.LogInformation("Template '{Template}' applied against source '{Source}'.", args.Template.Name, source.Name);
    }

    // ── Dashboard lifecycle (Phase 10) ───────────────────────────────────────

    /// <summary>Starts a brand-new, empty dashboard on the canvas.</summary>
    [RelayCommand]
    public void NewDashboard()
    {
        var dashboard = new Dashboard { Name = "Untitled Dashboard" };
        CanvasPanel.OpenDashboard(dashboard);
        StatusMessage = "New dashboard created (not yet saved).";
        _logger.LogInformation("New dashboard created: {Id}.", dashboard.Id);
    }

    /// <summary>Loads the list of dashboards saved in the workbook, for the Open picker.</summary>
    [RelayCommand]
    public async Task LoadSavedDashboardsAsync()
    {
        try
        {
            var all = await _dashboardRepository.GetAllAsync();
            SavedDashboards.Clear();
            foreach (var d in all.OrderByDescending(d => d.ModifiedAt))
                SavedDashboards.Add(d);

            _logger.LogInformation("Loaded {Count} saved dashboard(s) for the Open picker.", SavedDashboards.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load saved dashboards.");
            StatusMessage = $"Failed to list dashboards: {ex.Message}";
        }
    }

    /// <summary>Opens a previously-saved dashboard onto the canvas.</summary>
    [RelayCommand]
    public void OpenDashboard(Dashboard dashboard)
    {
        CanvasPanel.OpenDashboard(dashboard);
        StatusMessage = $"Opened '{dashboard.Name}'.";
        _logger.LogInformation("Dashboard '{Name}' ({Id}) opened.", dashboard.Name, dashboard.Id);
    }

    /// <summary>Saves the currently active dashboard to the workbook.</summary>
    [RelayCommand]
    public async Task SaveDashboardAsync()
    {
        var dashboard = CanvasPanel.ActiveDashboard;
        if (dashboard is null)
        {
            StatusMessage = "Nothing to save — no dashboard is open.";
            return;
        }

        IsLoading = true;
        try
        {
            dashboard.ModifiedAt = DateTimeOffset.UtcNow;
            await _dashboardRepository.SaveAsync(dashboard);
            StatusMessage = $"Saved '{dashboard.Name}' at {DateTime.Now:HH:mm:ss}.";
            _logger.LogInformation("Dashboard '{Name}' ({Id}) saved.", dashboard.Name, dashboard.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save dashboard '{Name}'.", dashboard.Name);
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Deletes a saved dashboard from the workbook.</summary>
    [RelayCommand]
    public async Task DeleteDashboardAsync(Dashboard dashboard)
    {
        try
        {
            await _dashboardRepository.DeleteAsync(dashboard.Id);
            SavedDashboards.Remove(dashboard);

            if (CanvasPanel.ActiveDashboard?.Id == dashboard.Id)
                CanvasPanel.OpenDashboard(new Dashboard { Name = "Untitled Dashboard" });

            StatusMessage = $"Deleted '{dashboard.Name}'.";
            _logger.LogInformation("Dashboard '{Name}' ({Id}) deleted.", dashboard.Name, dashboard.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete dashboard '{Name}'.", dashboard.Name);
            StatusMessage = $"Delete failed: {ex.Message}";
        }
    }
}
