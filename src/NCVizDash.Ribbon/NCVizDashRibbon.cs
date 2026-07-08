using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Office.Core;
using NCVizDash.Core.Abstractions;

// Alias to avoid ambiguity with System.Reflection.Assembly
using OfficeCore = Microsoft.Office.Core;

namespace NCVizDash.Ribbon;

/// <summary>
/// Implements the Excel ribbon callbacks defined in NCVizDashRibbon.xml.
/// Registered as <see cref="IRibbonExtensibility"/> by the add-in host.
/// </summary>
[ComVisible(true)]
public sealed class NCVizDashRibbon : IRibbonExtensibility
{
    // ── Fields ──────────────────────────────────────────────────────────────
    private IRibbonUI? _ribbon;
    private bool _taskPaneVisible;
    private bool _aiReportPaneVisible;

    private readonly ILogger<NCVizDashRibbon> _logger;

    /// <summary>Raised when the user requests the task pane visibility to change.</summary>
    public event EventHandler<bool>? TaskPaneToggleRequested;

    /// <summary>Raised when the user requests a full data refresh.</summary>
    public event EventHandler? DataRefreshRequested;

    /// <summary>Raised when the user requests a brand-new dashboard.</summary>
    public event EventHandler? NewDashboardRequested;

    /// <summary>Raised when the user requests the Open Dashboard picker.</summary>
    public event EventHandler? OpenDashboardRequested;

    /// <summary>Raised when the user requests the active dashboard be saved.</summary>
    public event EventHandler? SaveDashboardRequested;

    /// <summary>Raised when the user requests the template picker.</summary>
    public event EventHandler? TemplatesRequested;

    /// <summary>Raised when the user requests one-click dashboard generation.</summary>
    public event EventHandler? GenerateDashboardRequested;

    /// <summary>Raised when the user requests full-screen presentation mode.</summary>
    public event EventHandler? PresentRequested;

    /// <summary>Raised when the user requests the dashboard be popped out into its own window.</summary>
    public event EventHandler? PopOutRequested;

    /// <summary>Raised when the user requests the AI settings dialog.</summary>
    public event EventHandler? AiSettingsRequested;

    /// <summary>Raised when the user toggles the AI Report Generator pane.</summary>
    public event EventHandler<bool>? AiReportPaneToggleRequested;

    /// <summary>Raised when the user changes the active theme.</summary>
    public event EventHandler<string>? ThemeChangeRequested;

    // ── Constructor ──────────────────────────────────────────────────────────

    /// <summary>Initialises the ribbon with required services.</summary>
    public NCVizDashRibbon(ILogger<NCVizDashRibbon> logger)
    {
        _logger = logger;
    }

    // ── IRibbonExtensibility ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public string GetCustomUI(string ribbonId)
    {
        _logger.LogInformation("GetCustomUI called for ribbon '{RibbonId}'.", ribbonId);

        if (!string.IsNullOrEmpty(ribbonId)
            && !ribbonId.Equals("Microsoft.Excel.Workbook", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var asm = Assembly.GetExecutingAssembly();
        const string resourceName = "NCVizDash.Ribbon.NCVizDashRibbon.xml";

        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? asm.GetManifestResourceNames()
                .Where(name => name.EndsWith("NCVizDashRibbon.xml", StringComparison.OrdinalIgnoreCase))
                .Select(asm.GetManifestResourceStream)
                .FirstOrDefault(s => s is not null);

        if (stream is null)
        {
            var available = string.Join(", ", asm.GetManifestResourceNames());
            _logger.LogError(
                "Ribbon XML resource '{Resource}' not found. Available resources: {Resources}",
                resourceName,
                available);
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        var xml = reader.ReadToEnd();
        _logger.LogInformation("Ribbon XML loaded ({Length} characters).", xml.Length);
        return xml;
    }

    // ── Ribbon lifecycle ──────────────────────────────────────────────────────

    /// <summary>Called by Excel once the ribbon is loaded.</summary>
    public void Ribbon_OnLoad(IRibbonUI ribbon)
    {
        _ribbon = ribbon;
        _logger.LogInformation("NC VizDash ribbon loaded successfully.");
    }

    // ── Button callbacks ──────────────────────────────────────────────────────

    /// <summary>New Dashboard button.</summary>
    public void BtnNewDashboard_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Generate Dashboard.");
        NewDashboardRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Open Dashboard button.</summary>
    public void BtnOpenDashboard_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Open Dashboard.");
        OpenDashboardRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Save Dashboard button.</summary>
    public void BtnSaveDashboard_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Save Dashboard.");
        SaveDashboardRequested?.Invoke(this, EventArgs.Empty);
    }

   /// <summary>Generate Dashboard button (One-Click Dashboard Generator).</summary>
    public void BtnGenerateDashboard_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Generate Dashboard.");
        GenerateDashboardRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Present button (full-screen Story Mode).</summary>
    public void BtnPresent_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Present.");
        PresentRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Pop Out button (dashboard in its own non-modal window).</summary>
    public void BtnPopOut_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Pop Out.");
        PopOutRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Templates button.</summary>
    public void BtnTemplates_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Templates.");
        TemplatesRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>AI Settings button.</summary>
    public void BtnAiSettings_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: AI Settings.");
        AiSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Refresh Data button.</summary>
    public void BtnRefreshData_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Refresh Data.");
        DataRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Data Sources button.</summary>
    public void BtnDataSources_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Data Sources.");
    }

    /// <summary>Toggle Task Pane button.</summary>
    public void BtnToggleTaskPane_Click(IRibbonControl control, bool pressed)
    {
        _taskPaneVisible = pressed;
        _logger.LogInformation("Task pane visibility toggled: {Visible}", pressed);
        TaskPaneToggleRequested?.Invoke(this, pressed);
    }

    /// <summary>Returns whether the task pane is currently visible (drives toggle state).</summary>
    public bool BtnToggleTaskPane_GetPressed(IRibbonControl control) => _taskPaneVisible;

    /// <summary>Toggle AI Report Generator pane button.</summary>
    public void BtnToggleAiReport_Click(IRibbonControl control, bool pressed)
    {
        _aiReportPaneVisible = pressed;
        _logger.LogInformation("AI Report pane visibility toggled: {Visible}", pressed);
        AiReportPaneToggleRequested?.Invoke(this, pressed);
    }

    /// <summary>Returns whether the AI Report pane is visible (drives toggle state).</summary>
    public bool BtnToggleAiReport_GetPressed(IRibbonControl control) => _aiReportPaneVisible;

    /// <summary>Theme combo-box change.</summary>
    public void CmbTheme_Change(IRibbonControl control, string text)
    {
        _logger.LogInformation("Theme changed to '{Theme}'.", text);
        ThemeChangeRequested?.Invoke(this, text);
    }

    /// <summary>Settings → Theme → Light.</summary>
    public void BtnThemeLight_Click(IRibbonControl control)
    {
        _logger.LogInformation("Theme changed to 'Light'.");
        ThemeChangeRequested?.Invoke(this, "Light");
    }

    /// <summary>Settings → Theme → Dark.</summary>
    public void BtnThemeDark_Click(IRibbonControl control)
    {
        _logger.LogInformation("Theme changed to 'Dark'.");
        ThemeChangeRequested?.Invoke(this, "Dark");
    }

    // ── Export callbacks ──────────────────────────────────────────────────────

    /// <summary>Export → PDF.</summary>
    public void BtnExportPdf_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Export PDF.");
    }

    /// <summary>Export → PowerPoint.</summary>
    public void BtnExportPptx_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Export PowerPoint.");
    }

    /// <summary>Export → PNG.</summary>
    public void BtnExportPng_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Export PNG.");
    }

    /// <summary>Export → Excel Snapshot.</summary>
    public void BtnExportSnapshot_Click(IRibbonControl control)
    {
        _logger.LogInformation("User requested: Export Excel Snapshot.");
    }

    // ── Help ──────────────────────────────────────────────────────────────────

    /// <summary>About button.</summary>
    public void BtnAbout_Click(IRibbonControl control)
    {
        _logger.LogInformation("User opened About dialog.");

        // WinForms' MessageBox, not WPF's — this project references
        // System.Windows.Forms (UseWindowsForms) but not System.Windows/WPF
        // (UseWPF), so System.Windows.Forms.MessageBox is what's actually
        // available here. There's no reason to pull WPF into the Ribbon
        // project just for a simple About dialog.
        System.Windows.Forms.MessageBox.Show(
            "NC VizDash\nVersion 1.0.0 – Phase 1\n\n" +
            "Enterprise Business Intelligence for Microsoft Excel.\n\n" +
            "© NC VizDash Contributors",
            "About NC VizDash",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Information);
    }

    /// <summary>Updates ribbon toggle state when the task pane is shown or hidden externally.</summary>
    public void SetTaskPaneVisible(bool visible)
    {
        _taskPaneVisible = visible;
        InvalidateTaskPaneButton();
    }

    /// <summary>Notifies Excel to repaint the task pane toggle button.</summary>
    public void InvalidateTaskPaneButton() =>
        _ribbon?.InvalidateControl("btnToggleTaskPane");

    /// <summary>Updates ribbon toggle state when the AI Report pane is shown or hidden externally.</summary>
    public void SetAiReportPaneVisible(bool visible)
    {
        _aiReportPaneVisible = visible;
        _ribbon?.InvalidateControl("btnToggleAiReport");
    }
}
