using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Office.Core;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.DependencyInjection;
using NCVizDash.Infrastructure.Logging;
using NCVizDash.Models;
using NCVizDash.Ribbon;
using NCVizDash.TaskPane.Theming;
using NCVizDash.TaskPane.ViewModels;
using NCVizDash.TaskPane.Views;
using Serilog;
using System.Runtime.InteropServices;

namespace NCVizDash.ExcelAddIn;

/// <summary>
/// VSTO add-in entry point.
/// Responsible for:
/// 1. Bootstrapping Serilog before anything else touches logging.
/// 2. Building the DI container.
/// 3. Registering the Excel ribbon.
/// 4. Creating and managing the WPF task pane.
/// 5. Wiring Excel workbook events.
/// </summary>
public sealed partial class ThisAddIn
{
    // ── Fields ──────────────────────────────────────────────────────────────
    private ServiceProvider? _serviceProvider;
    private NCVizDashRibbon? _ribbon;
    private Microsoft.Office.Tools.CustomTaskPane? _taskPane;
    private Microsoft.Office.Tools.CustomTaskPane? _aiReportPane;
    private ShellView? _shellView;
    private AiReportPaneView? _aiReportView;
    private ILogger<ThisAddIn>? _logger;
    private System.Timers.Timer? _autoRefreshDebounceTimer;

    // ── VSTO lifecycle ───────────────────────────────────────────────────────

    private void ThisAddIn_Startup(object sender, EventArgs e)
    {
        try
        {
            // ── 1. Bootstrap logging first (before DI so early errors are captured) ──
            var earlySettings = new AppSettings(); // defaults
            SerilogBootstrapper.CreateLogger(earlySettings);
            Log.Information("NC VizDash add-in startup initiated.");

            // ── 2. Build DI container ──
            _serviceProvider = BuildServiceProvider();
            _logger = _serviceProvider.GetRequiredService<ILogger<ThisAddIn>>();

            // Reload settings via DI-resolved provider, then reconfigure Serilog
            var settingsProvider = _serviceProvider.GetRequiredService<IAppSettingsProvider>();
            SerilogBootstrapper.CreateLogger(settingsProvider.Settings);
            _logger.LogInformation("NC VizDash DI container built successfully.");

            // ── 3. Wire ribbon events on the instance Excel already hosts ──
            _ribbon ??= _serviceProvider.GetRequiredService<NCVizDashRibbon>();
            WireRibbonEvents(_ribbon);

            // ── 4. Create the WPF task pane wrapped in an ElementHost ──
            InitialiseTaskPane();
            InitialiseAiReportPane();

            // ── 5. Wire Excel application events ──
            Application.WorkbookActivate         += OnWorkbookActivate;
            Application.WorkbookDeactivate       += OnWorkbookDeactivate;
            Application.SheetChange               += OnSheetChange;
            Application.SheetActivate             += OnSheetActivate;

            _logger.LogInformation("NC VizDash startup complete.");
        }
        catch (Exception ex)
        {
            // Last-resort catch: surface to the user before Excel swallows the error.
            Log.Fatal(ex, "NC VizDash failed to start.");
            System.Windows.MessageBox.Show(
                $"NC VizDash failed to start:\n\n{ex.Message}\n\n" +
                $"Check the log at %LOCALAPPDATA%\\NCVizDash\\Logs for details.",
                "NC VizDash – Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void ThisAddIn_Shutdown(object sender, EventArgs e)
    {
        try
        {
            _logger?.LogInformation("NC VizDash shutting down.");

           Application.WorkbookActivate         -= OnWorkbookActivate;
            Application.WorkbookDeactivate       -= OnWorkbookDeactivate;
            Application.SheetChange               -= OnSheetChange;
            Application.SheetActivate             -= OnSheetActivate;

            _autoRefreshDebounceTimer?.Stop();
            _autoRefreshDebounceTimer?.Dispose();

            _serviceProvider?.Dispose();
        }
        finally
        {
            SerilogBootstrapper.CloseAndFlush();
        }
    }

    // ── IRibbonExtensibility (VSTO callback) ─────────────────────────────────

    /// <summary>
    /// VSTO calls this to get the ribbon XML.
    /// Delegates to the DI-resolved ribbon instance.
    /// </summary>
    protected override IRibbonExtensibility CreateRibbonExtensibilityObject()
    {
        // Called before Startup on most Office builds — keep one instance for Excel + DI.
        _ribbon ??= new NCVizDashRibbon(
            Microsoft.Extensions.Logging.Abstractions
                      .NullLogger<NCVizDashRibbon>.Instance);
        return _ribbon;
    }

    private void WireRibbonEvents(NCVizDashRibbon ribbon)
    {
        ribbon.DataRefreshRequested     -= OnDataRefreshRequested;
        ribbon.TaskPaneToggleRequested  -= OnTaskPaneToggleRequested;
        ribbon.ThemeChangeRequested     -= OnThemeChangeRequested;
        ribbon.NewDashboardRequested    -= OnNewDashboardRequested;
        ribbon.OpenDashboardRequested   -= OnOpenDashboardRequested;
        ribbon.SaveDashboardRequested   -= OnSaveDashboardRequested;
        ribbon.TemplatesRequested       -= OnTemplatesRequested;
        ribbon.GenerateDashboardRequested -= OnGenerateDashboardRequested;
        ribbon.PresentRequested         -= OnPresentRequested;
        ribbon.PopOutRequested          -= OnPopOutRequested;
        ribbon.AiSettingsRequested      -= OnAiSettingsRequested;
        ribbon.AiReportPaneToggleRequested -= OnAiReportPaneToggleRequested;

        ribbon.DataRefreshRequested     += OnDataRefreshRequested;
        ribbon.TaskPaneToggleRequested  += OnTaskPaneToggleRequested;
        ribbon.ThemeChangeRequested     += OnThemeChangeRequested;
        ribbon.NewDashboardRequested   += OnNewDashboardRequested;
        ribbon.OpenDashboardRequested  += OnOpenDashboardRequested;
        ribbon.SaveDashboardRequested  += OnSaveDashboardRequested;
        ribbon.TemplatesRequested      += OnTemplatesRequested;
        ribbon.GenerateDashboardRequested += OnGenerateDashboardRequested;
        ribbon.PresentRequested        += OnPresentRequested;
        ribbon.AiSettingsRequested     += OnAiSettingsRequested;
        ribbon.PopOutRequested         += OnPopOutRequested;
        ribbon.AiReportPaneToggleRequested += OnAiReportPaneToggleRequested;
    }

    // ── DI composition root ──────────────────────────────────────────────────

    private ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Core
        services.AddNCVizDashCore();

        // Infrastructure (Serilog logging + JSON settings)
        services.AddNCVizDashInfrastructure();

        // Ribbon (singleton – Excel receives the same instance from CreateRibbonExtensibilityObject)
        if (_ribbon is not null)
            services.AddSingleton(_ribbon);
        else
            services.AddSingleton<NCVizDashRibbon>();

        // Theme service (Material Design runtime switching)
        services.AddSingleton<NCVizDash.TaskPane.Services.ThemeService>();

        // Task Pane ViewModels
        services.AddSingleton<ExplorerPanelViewModel>();
        services.AddSingleton<CanvasPanelViewModel>();
        services.AddSingleton<VisualLibraryViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<GlobalFilterBarViewModel>();

        // AI Report Generator (standalone assistant pane — separate from the dashboard shell)
        services.AddSingleton<AiReportPaneViewModel>();

        // Task pane shell (constructed manually — not via DI — so ElementHost owns the sole visual tree).

        // Phase 2 — Excel Data Engine
        services.AddSingleton<IExcelDataReader>(_ =>
            new NCVizDash.ExcelAddIn.DataAccess.ExcelDataReader(
                Application,
                _.GetRequiredService<ILogger<NCVizDash.ExcelAddIn.DataAccess.ExcelDataReader>>()));

        // Phase 7 — DuckDB engine, wrapped by the Phase 16 caching decorator.
        services.AddSingleton<NCVizDash.DuckDB.DuckDbAnalyticsEngine>();
        services.AddSingleton<IAnalyticsEngine>(sp =>
            new NCVizDash.TaskPane.Services.CachingAnalyticsEngine(
                sp.GetRequiredService<NCVizDash.DuckDB.DuckDbAnalyticsEngine>(),
                sp.GetRequiredService<ILogger<NCVizDash.TaskPane.Services.CachingAnalyticsEngine>>()));

        services.AddSingleton<IVisualizationRuleEngine, NCVizDash.RuleEngine.DeterministicRuleEngine>();
        services.AddSingleton<IChartEngine, NCVizDash.ChartEngine.EChartsChartEngine>();
        services.AddSingleton<IFilterManager, NCVizDash.TaskPane.Services.CrossFilterManager>();
        services.AddSingleton<IGlobalFilterManager, NCVizDash.TaskPane.Services.GlobalFilterManager>();
        services.AddSingleton<NCVizDash.TaskPane.Services.DistinctValueService>();
        services.AddSingleton<NCVizDash.TaskPane.Services.WidgetRenderCoordinator>();

        // Phase 10 — Dashboard Storage
        services.AddSingleton<IDashboardRepository>(sp =>
            new NCVizDash.Persistence.WorkbookDashboardRepository(
                Application,
                sp.GetRequiredService<ILogger<NCVizDash.Persistence.WorkbookDashboardRepository>>()));

        // Phase 11 — Templates
        services.AddSingleton<NCVizDash.TaskPane.Templates.TemplateInstantiationService>();

        // Phase 12 — Advanced Features
        services.AddSingleton<NCVizDash.TaskPane.Services.UndoRedoManager>();
        services.AddSingleton<NCVizDash.TaskPane.Services.BookmarkManager>();
        services.AddSingleton<NCVizDash.TaskPane.Services.DrillManager>();
        services.AddSingleton<NCVizDash.TaskPane.Presentation.PresentationController>();
        services.AddSingleton<NCVizDash.TaskPane.Generation.OneClickDashboardGenerator>();

        // Phase 13 — Export
        services.AddSingleton<NCVizDash.TaskPane.Export.ExportService>();
        services.AddSingleton<NCVizDash.ExcelAddIn.DataAccess.ExcelSnapshotExporter>(sp =>
            new NCVizDash.ExcelAddIn.DataAccess.ExcelSnapshotExporter(
                Application,
                sp.GetRequiredService<ILogger<NCVizDash.ExcelAddIn.DataAccess.ExcelSnapshotExporter>>()));

        // Phase 14 — Data Connectors (registered for programmatic use; no ribbon UI wired to them yet)
        services.AddSingleton<NCVizDash.Connectors.Csv.CsvFileConnector>();
        services.AddSingleton<NCVizDash.Connectors.Json.JsonFileConnector>();
        services.AddSingleton<NCVizDash.Connectors.Sql.SqlServerConnector>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<NCVizDash.Connectors.Rest.RestApiConnector>();
        services.AddSingleton<NCVizDash.Connectors.SharePoint.SharePointListConnector>();

        // v2.0 Feature 5 — Jira Enterprise Connector
        services.AddSingleton<NCVizDash.Connectors.Jira.JiraConnectionProfileStore>();
        services.AddSingleton<NCVizDash.Connectors.Jira.JiraConnector>();
        services.AddSingleton<JqlEditorViewModel>();

        // Phase 15 — Collaboration
        services.AddSingleton<NCVizDash.TaskPane.Services.DashboardShareService>();

        // Phase 17 — Plugin SDK
        services.AddSingleton<NCVizDash.TaskPane.Plugins.PluginLoader>();

        // Phase 18 — Optional AI (all disabled unless AppSettings.AiEnabled = true; see AiFeatureGate)
        services.AddSingleton<IAiProvider, NCVizDash.TaskPane.Ai.OpenAiProvider>();
        services.AddSingleton<IAiProvider, NCVizDash.TaskPane.Ai.AzureOpenAiProvider>();
        services.AddSingleton<IAiProvider, NCVizDash.TaskPane.Ai.AnthropicProvider>();
        services.AddSingleton<IAiProvider, NCVizDash.TaskPane.Ai.LocalLlmProvider>();
        services.AddSingleton<IAiProvider, NCVizDash.TaskPane.Ai.KimiProvider>();
        services.AddSingleton<IAiProvider, NCVizDash.TaskPane.Ai.CustomOpenAiProvider>();
        services.AddSingleton<NCVizDash.TaskPane.Ai.AiFeatureGate>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    // ── Task pane initialisation ─────────────────────────────────────────────

    private void InitialiseTaskPane()
    {
        if (_serviceProvider is null || _taskPane is not null)
            return;

        WpfResourceBootstrap.EnsureApplicationResources();

        var viewModel = _serviceProvider.GetRequiredService<ShellViewModel>();
        var themeService = _serviceProvider.GetRequiredService<NCVizDash.TaskPane.Services.ThemeService>();

        // ShellView is a UserControl (not a Window). Hosting Window.Content in ElementHost
        // fails because InitializeComponent() parents the root grid to the Window; detaching
        // with shellWindow.Content = null breaks resource lookup. UserControl avoids that.
        _shellView = new ShellView(themeService);

        var elementHost = new System.Windows.Forms.Integration.ElementHost
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            AutoSize = false,
        };

        var hostControl = new System.Windows.Forms.UserControl
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
        };
        hostControl.Controls.Add(elementHost);

        // Host first, then bind — expanding DataTemplates before parenting can confuse ElementHost.
        elementHost.Child = _shellView;
        _shellView.BindViewModel(viewModel);

        _taskPane = CustomTaskPanes.Add(hostControl, "NC VizDash");
        _taskPane.Width          = 1200;
        _taskPane.DockPosition   = Microsoft.Office.Core.MsoCTPDockPosition.msoCTPDockPositionRight;
        _taskPane.Visible        = false;

        _logger?.LogInformation("Task pane created (hidden).");
    }

    private void InitialiseAiReportPane()
    {
        if (_serviceProvider is null || _aiReportPane is not null)
            return;

        WpfResourceBootstrap.EnsureApplicationResources();

        var viewModel = _serviceProvider.GetRequiredService<AiReportPaneViewModel>();

        _aiReportView = new AiReportPaneView();

        var elementHost = new System.Windows.Forms.Integration.ElementHost
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            AutoSize = false,
        };

        var hostControl = new System.Windows.Forms.UserControl
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
        };
        hostControl.Controls.Add(elementHost);

        // Host first, then bind — same ElementHost caveat as the dashboard shell.
        elementHost.Child = _aiReportView;
        _aiReportView.BindViewModel(viewModel);

        _aiReportPane = CustomTaskPanes.Add(hostControl, "VizDash — AI Report");
        _aiReportPane.Width        = 380;
        _aiReportPane.DockPosition = Microsoft.Office.Core.MsoCTPDockPosition.msoCTPDockPositionRight;
        _aiReportPane.Visible      = false;

        // Keep the ribbon toggle in sync when the user closes the pane via its own ✕.
        _aiReportPane.VisibleChanged += (_, _) =>
            _ribbon?.SetAiReportPaneVisible(_aiReportPane?.Visible ?? false);
        viewModel.CloseRequested += (_, _) => SetAiReportPaneVisible(false);
        viewModel.InsertRequested += OnAiReportInsertRequested;

        _logger?.LogInformation("AI Report pane created (hidden).");
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnTaskPaneToggleRequested(object? sender, bool visible)
    {
        SetTaskPaneVisible(visible);

        if (!visible || _serviceProvider is null) return;

        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();
        if (shell.ExplorerPanel.DataSources.Count == 0)
            _ = shell.RefreshDataAsync();
    }

    private void SetTaskPaneVisible(bool visible)
    {
        if (_taskPane is not null)
            _taskPane.Visible = visible;

        _ribbon?.SetTaskPaneVisible(visible);
    }

    private void OnAiReportPaneToggleRequested(object? sender, bool visible) =>
        SetAiReportPaneVisible(visible);

    private void SetAiReportPaneVisible(bool visible)
    {
        if (_aiReportPane is not null)
            _aiReportPane.Visible = visible;

        _ribbon?.SetAiReportPaneVisible(visible);
    }

    private void OnAiReportInsertRequested(object? sender, string prompt)
    {
        // Phase 1: land the draft on a new worksheet stub. Later phases will
        // materialise the previewed chart + insight table via the chart engine.
        try
        {
            var wb = Application.ActiveWorkbook;
            if (wb is null) return;

            var sheet = (Microsoft.Office.Interop.Excel.Worksheet)wb.Worksheets.Add();
            sheet.Name = GetUniqueSheetName(wb, "AI Report");
            sheet.Cells[1, 1] = "VizDash AI Report";
            sheet.Cells[2, 1] = $"Prompt: {prompt}";
            _logger?.LogInformation("AI report inserted into sheet '{Sheet}'.", sheet.Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to insert AI report into workbook.");
        }
    }

    private static string GetUniqueSheetName(
        Microsoft.Office.Interop.Excel.Workbook wb, string baseName)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Microsoft.Office.Interop.Excel.Worksheet ws in wb.Worksheets)
            existing.Add(ws.Name);

        if (!existing.Contains(baseName)) return baseName;
        for (var i = 2; ; i++)
            if (!existing.Contains($"{baseName} {i}"))
                return $"{baseName} {i}";
    }

    private void OnDataRefreshRequested(object? sender, EventArgs e)
    {
        if (_serviceProvider is null) return;
        SetTaskPaneVisible(true);
        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();
        _ = shell.RefreshDataAsync(); // fire-and-forget; ViewModel owns error handling
    }

    private async void OnNewDashboardRequested(object? sender, EventArgs e)
    {
        if (_serviceProvider is null) return;

        SetTaskPaneVisible(true);

        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();

        if (shell.ExplorerPanel.DataSources.Count == 0)
            await shell.RefreshDataAsync();

        if (shell.ExplorerPanel.DataSources.Count > 0)
            shell.GenerateDashboardCommand.Execute(null);
        else
            shell.NewDashboardCommand.Execute(null);
    }

    private void OnOpenDashboardRequested(object? sender, EventArgs e)
    {
        if (_serviceProvider is null) return;
        SetTaskPaneVisible(true);
        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();
        _ = shell.LoadSavedDashboardsCommand.ExecuteAsync(null);
        // The task pane's Open picker (bound to ShellViewModel.SavedDashboards) becomes
        // visible/populated once this completes; the ribbon just triggers the load.
    }

    private void OnSaveDashboardRequested(object? sender, EventArgs e)
    {
        if (_serviceProvider is null) return;
        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();
        _ = shell.SaveDashboardCommand.ExecuteAsync(null);
    }

    private void OnTemplatesRequested(object? sender, EventArgs e)
    {
        if (_serviceProvider is null) return;
        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();
        new NCVizDash.TaskPane.Views.TemplatePickerWindow(shell).ShowDialog();
    }

    private async void OnGenerateDashboardRequested(object? sender, EventArgs e)
    {
        if (_serviceProvider is null) return;

        SetTaskPaneVisible(true);
        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();

        try
        {
            if (shell.ExplorerPanel.DataSources.Count == 0)
                await shell.RefreshDataAsync();

            if (shell.ExplorerPanel.DataSources.Count == 0)
            {
                shell.StatusMessage = "No data sources found — add an Excel table or named range, then click Refresh.";
                return;
            }

            await shell.GenerateDashboardCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Generate Dashboard failed.");
            shell.StatusMessage = $"Generate failed: {ex.Message}";
        }
    }

    private void OnPresentRequested(object? sender, EventArgs e)
    {
        if (_serviceProvider is null) return;
        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();
        shell.RequestPresentCommand.Execute(null);
    }

     private void OnPopOutRequested(object? sender, EventArgs e)
    {
        if (_serviceProvider is null) return;
        SetTaskPaneVisible(true);
        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();
        shell.RequestPopOutCommand.Execute(null);
    }

    private void OnAiSettingsRequested(object? sender, EventArgs e)
    {
        if (_serviceProvider is null) return;
        var settings = _serviceProvider.GetRequiredService<IAppSettingsProvider>();
        new NCVizDash.TaskPane.Views.AiSettingsWindow(settings).ShowDialog();
    }

    private void OnThemeChangeRequested(object? sender, string theme)
    {
        if (_serviceProvider is null) return;
        var shell = _serviceProvider.GetRequiredService<ShellViewModel>();
        shell.ApplyThemeCommand.Execute(theme);
    }

    // ── Excel event handlers ─────────────────────────────────────────────────

    private void OnWorkbookActivate(Microsoft.Office.Interop.Excel.Workbook wb)
    {
        _logger?.LogDebug("Workbook activated: {Name}", wb.Name);
    }

    private void OnWorkbookDeactivate(Microsoft.Office.Interop.Excel.Workbook wb)
    {
        _logger?.LogDebug("Workbook deactivated: {Name}", wb.Name);
    }

    private void OnSheetChange(object sheet, Microsoft.Office.Interop.Excel.Range target)
    {
        var sheetName = (sheet as Microsoft.Office.Interop.Excel.Worksheet)?.Name ?? "unknown";
        _logger?.LogDebug("Sheet change detected in '{Sheet}'.", sheetName);

        if (_serviceProvider is null) return;

        var settings = _serviceProvider.GetRequiredService<IAppSettingsProvider>().Settings;
        if (settings.AutoRefreshSeconds <= 0)
            return; // auto-refresh disabled by user settings

        // Debounce rapid successive edits (e.g. typing, paste, fill-down) so we don't
        // re-run a full workbook scan + DuckDB reload on every single cell change.
        _autoRefreshDebounceTimer?.Stop();
        _autoRefreshDebounceTimer?.Dispose();

        _autoRefreshDebounceTimer = new System.Timers.Timer(settings.AutoRefreshSeconds * 1000)
        {
            AutoReset = false
        };
        _autoRefreshDebounceTimer.Elapsed += (_, _) =>
        {
            _logger?.LogInformation("Live refresh triggered for sheet '{Sheet}' after debounce window.", sheetName);
            _ = OnLiveRefreshDebounceElapsedAsync(sheetName);
        };
        _autoRefreshDebounceTimer.Start();
    }

    /// <summary>
    /// Fires whenever the user switches to a different sheet. Covers the case
    /// Live Refresh's debounced edit-based path misses entirely: a brand-new
    /// sheet/table that was just pasted in and switched to, with no cell edit
    /// (and therefore no SheetChange event) having happened yet.
    /// </summary>
    private void OnSheetActivate(object sheet)
    {
        var sheetName = (sheet as Microsoft.Office.Interop.Excel.Worksheet)?.Name ?? "unknown";
        _logger?.LogDebug("Sheet activated: '{Sheet}'.", sheetName);

        if (_serviceProvider is null) return;
        _ = OnLiveRefreshDebounceElapsedAsync(sheetName); // reuses the same rescan-if-unknown logic as edits
    }

    /// <summary>
    /// Live Refresh (v2.0 Feature 4): reloads only the data source(s) on the
    /// changed sheet, then notifies the canvas to re-render only the widgets
    /// bound to those specific data sources. Filters, layout, and selection are
    /// untouched since nothing about the dashboard/widget structure changes —
    /// only the underlying rows for the affected source(s) are refreshed.
    /// </summary>
    private async Task OnLiveRefreshDebounceElapsedAsync(string sheetName)
    {
        if (_serviceProvider is null) return;

        try
        {
            var shell = _serviceProvider.GetRequiredService<ShellViewModel>();
            var refreshedIds = await shell.ExplorerPanel.RefreshSheetAsync(sheetName);

            foreach (var id in refreshedIds)
                shell.CanvasPanel.NotifyDataSourceRefreshed(id);

            if (refreshedIds.Count > 0)
                _logger?.LogInformation("Live refresh complete for sheet '{Sheet}': {Count} data source(s), affected widgets re-rendered.",
                    sheetName, refreshedIds.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Live refresh failed for sheet '{Sheet}'.", sheetName);
        }
    }

    // ── VSTO generated code placeholder ──────────────────────────────────────
    // (Visual Studio generates the partial class below; included here for
    //  completeness when scaffolding manually.)
    private void InternalStartup()
    {
        Startup  += ThisAddIn_Startup;
        Shutdown += ThisAddIn_Shutdown;
    }
}
