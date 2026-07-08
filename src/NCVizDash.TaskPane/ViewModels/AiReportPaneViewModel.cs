using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.TaskPane.Ai;

namespace NCVizDash.TaskPane.ViewModels;

// ─────────────────────────────────────────────────────────────────────────────
//  AI Report Generator ViewModel
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A single bar in the report-preview mini chart (height in device-independent pixels).</summary>
public sealed record PreviewBar(string Label, double Height, bool IsHighlight);

/// <summary>
/// Everything needed to materialise a previewed report into the workbook or onto
/// the clipboard: the category/value series behind the preview, the chart kind
/// key (matches the \directive vocabulary — "bar", "pie", "line", …), and the
/// narrative text shown alongside it.
/// </summary>
public sealed record ReportDraft(
    string Prompt,
    string ChartKind,
    string ChartTypeLabel,
    IReadOnlyList<string> Categories,
    IReadOnlyList<double> Values,
    string InsightText,
    string SeriesName);

/// <summary>
/// Drives the AI Report Generator task pane — a standalone assistant, deliberately
/// decoupled from the dashboard shell (<see cref="ShellViewModel"/>). Users type a
/// natural-language request (or pick a quick action), preview the drafted report,
/// then insert it into the workbook or refine the draft.
/// </summary>
public sealed partial class AiReportPaneViewModel : ObservableObject
{
    private readonly ILogger<AiReportPaneViewModel> _logger;
    private readonly IExcelDataReader _excelDataReader;
    private readonly AiFeatureGate _aiGate;
    private readonly IAppSettingsProvider _settings;

    /// <summary>Chart-type override directives users can type in the prompt, e.g. "Q2 sales \pie".</summary>
    private static readonly Regex ChartDirectiveRegex = new(
        @"(?<=^|\s)\\(bar|column|pie|donut|doughnut|line|area|scatter|kpi|table)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Raised when the user clicks the ✕ close button.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Raised when the user confirms inserting the drafted report.</summary>
    public event EventHandler<ReportDraft>? InsertRequested;

    /// <summary>Raised when the user asks to copy the chart to the clipboard for email.</summary>
    public event EventHandler<ReportDraft>? CopyForEmailRequested;

    // ── Bindable state ───────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateCommand))]
    private string _prompt = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _hasDraft;

    [ObservableProperty]
    private string _insightText = string.Empty;

    [ObservableProperty]
    private string _rowsScanned = "0";

    [ObservableProperty]
    private string _confidenceScore = "–";

    [ObservableProperty]
    private ObservableCollection<PreviewBar> _previewBars = [];

    /// <summary>Raw chart-kind key ("bar", "pie", "line", …) — used when building the real chart.</summary>
    [ObservableProperty]
    private string _chartKind = "column";

    /// <summary>Human-readable chart type shown on the preview badge ("Column", "Pie", …).</summary>
    [ObservableProperty]
    private string _chartTypeLabel = "Column";

    /// <summary>True when the preview renders the mini bar chart; false for icon previews.</summary>
    [ObservableProperty]
    private bool _isBarPreview = true;

    /// <summary>Icon used for non-bar chart previews (pie, line, scatter, …).</summary>
    [ObservableProperty]
    private PackIconKind _chartIconKind = PackIconKind.ChartBar;

    /// <summary>Which LLM (if any) the pane is connected to — driven by Settings ▸ AI Settings.</summary>
    [ObservableProperty]
    private string _connectionStatus = string.Empty;

    /// <summary>Inverse of <see cref="IsGenerating"/> for enabling inputs.</summary>
    public bool IsIdle => !IsGenerating;

    /// <summary>One-click shortcuts shown under the prompt box.</summary>
    public IReadOnlyList<string> QuickActions { get; } =
        ["Summarize sheet", "Trend analysis", "Find anomalies"];

    // ── Constructor ──────────────────────────────────────────────────────────

    /// <summary>
    /// Creates the AI Report Generator pane's view model, wiring the analytics
    /// engine, the shared <see cref="AiFeatureGate"/> (so this pane respects the
    /// same "AI must remain optional" gate as the rest of the app), and the
    /// settings provider used to display the current connection status.
    /// </summary>
    /// <param name="logger">Logger for diagnostic and audit output.</param>
    /// <param name="excelDataReader">Reads the active workbook's tables and ranges.</param>
    /// <param name="aiGate">Resolves the enabled AI provider, if any.</param>
    /// <param name="settings">Persisted application settings, including AI configuration.</param>
    public AiReportPaneViewModel(
        ILogger<AiReportPaneViewModel> logger,
        IExcelDataReader excelDataReader,
        AiFeatureGate aiGate,
        IAppSettingsProvider settings)
    {
        _logger = logger;
        _excelDataReader = excelDataReader;
        _aiGate = aiGate;
        _settings = settings;
        RefreshConnectionStatus();
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    private bool CanGenerate() => !string.IsNullOrWhiteSpace(Prompt) && !IsGenerating;

    /// <summary>Generates a report draft from the natural-language prompt.</summary>
    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        var (cleanPrompt, chartType) = ParseChartDirective(Prompt);
        ApplyChartType(chartType);

        _logger.LogInformation(
            "AI report requested: {Prompt} (chart: {Chart})", cleanPrompt, ChartTypeLabel);
        IsGenerating = true;
        RefreshConnectionStatus();

        try
        {
            // Phase 1: deterministic local draft so the pane works with AI disabled.
            // TODO Phase 2: route through AiFeatureGate → IAiProvider when the user
            //               has opted in (AnthropicProvider / OpenAiCompatibleProvider).
            await Task.Delay(600).ConfigureAwait(true); // simulate analysis latency

            PreviewBars =
            [
                new("JAN", 42, false), new("FEB", 66, false), new("MAR", 54, false),
                new("APR", 96, true),  new("MAY", 50, false), new("JUN", 46, false),
            ];

            InsightText =
                "VizDash identified a 12% growth in Western regions during Q2. " +
                $"Drafting a {ChartTypeLabel.ToLowerInvariant()} chart for the \"Sales_2024\" worksheet.";
            RowsScanned = "1,248";
            ConfidenceScore = "94%";
            HasDraft = true;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>Fills the prompt from a quick-action chip and generates immediately.</summary>
    [RelayCommand]
    private Task QuickActionAsync(string action)
    {
        Prompt = action switch
        {
            "Summarize sheet" => "Summarize the active sheet with key metrics.",
            "Trend analysis"  => "Analyze trends across all numeric columns.",
            "Find anomalies"  => "Find anomalies and outliers in the data.",
            _                 => action,
        };
        return GenerateAsync();
    }

    /// <summary>Inserts the drafted report into the workbook.</summary>
    [RelayCommand]
    private void InsertIntoWorkbook()
    {
        if (!HasDraft) return;
        _logger.LogInformation("Inserting AI report draft into workbook.");
        InsertRequested?.Invoke(this, BuildDraft());
    }

    /// <summary>Copies just the chart to the clipboard, ready to paste into an email.</summary>
    [RelayCommand]
    private void CopyChartForEmail()
    {
        if (!HasDraft) return;
        _logger.LogInformation("Copying AI report chart to clipboard for email.");
        CopyForEmailRequested?.Invoke(this, BuildDraft());
    }

    /// <summary>Returns focus to the prompt so the user can iterate on the draft.</summary>
    [RelayCommand]
    private void RefineDraft()
    {
        _logger.LogInformation("User refining AI report draft.");
        HasDraft = false;
    }

    /// <summary>Snapshots the current preview into an immutable draft for the host to materialise.</summary>
    private ReportDraft BuildDraft() => new(
        Prompt: Prompt,
        ChartKind: ChartKind,
        ChartTypeLabel: ChartTypeLabel,
        Categories: PreviewBars.Select(b => b.Label).ToList(),
        Values: PreviewBars.Select(b => b.Height).ToList(),
        InsightText: InsightText,
        SeriesName: "Value");

    // ── Chart directives & connection status ────────────────────────────────

    /// <summary>
    /// Extracts a trailing/inline chart directive (\bar, \pie, \line, …) from the
    /// prompt. Returns the prompt with directives stripped (that's what would be
    /// sent to the LLM) and the last directive found, or null if none.
    /// </summary>
    internal static (string CleanPrompt, string? ChartType) ParseChartDirective(string prompt)
    {
        string? chartType = null;
        foreach (Match m in ChartDirectiveRegex.Matches(prompt))
            chartType = m.Groups[1].Value.ToLowerInvariant(); // last directive wins

        var clean = ChartDirectiveRegex.Replace(prompt, string.Empty);
        clean = Regex.Replace(clean, @"\s{2,}", " ").Trim();
        return (clean, chartType);
    }

    private void ApplyChartType(string? chartType)
    {
        var normalized = chartType is "donut" ? "doughnut" : chartType ?? "column";
        ChartKind = normalized;

        (ChartTypeLabel, ChartIconKind, IsBarPreview) = normalized switch
        {
            "bar"       => ("Bar",     PackIconKind.ChartBar,          true),
            "column"    => ("Column",  PackIconKind.ChartBar,          true),
            "pie"       => ("Pie",     PackIconKind.ChartPie,          false),
            "doughnut"  => ("Donut",   PackIconKind.ChartDonut,        false),
            "line"      => ("Line",    PackIconKind.ChartLineVariant,  false),
            "area"      => ("Area",    PackIconKind.ChartAreaspline,   false),
            "scatter"   => ("Scatter", PackIconKind.ChartScatterPlot,  false),
            "kpi"       => ("KPI",     PackIconKind.Numeric,           false),
            "table"     => ("Table",   PackIconKind.Table,             false),
            _           => ("Column",  PackIconKind.ChartBar,          true),
        };
    }

    private void RefreshConnectionStatus()
    {
        var s = _settings.Settings;
        ConnectionStatus = _aiGate.IsAvailable
            ? $"AI connected: {s.AiProvider}" +
              (string.IsNullOrWhiteSpace(s.AiModel) ? string.Empty : $" · {s.AiModel}")
            : "Local draft mode — connect an LLM via Settings ▸ AI Settings…";
    }

    /// <summary>✕ button.</summary>
    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
