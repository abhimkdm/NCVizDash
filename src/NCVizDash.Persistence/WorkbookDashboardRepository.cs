using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Office.Core;
using Microsoft.Office.Interop.Excel;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;

namespace NCVizDash.Persistence;

/// <summary>
/// Stores dashboards inside the active workbook's Custom XML Parts — the standard
/// Office mechanism for persisting arbitrary structured data with a workbook,
/// invisible to the user and surviving save/close/reopen. Each dashboard is one
/// XML part; the dashboard's full JSON (via <see cref="System.Text.Json"/>) is
/// embedded as the text content of a single root element, so the Dashboard model
/// itself is the only schema that needs to stay in sync — no separate XML schema.
/// </summary>
public sealed class WorkbookDashboardRepository : IDashboardRepository
{
    private const string RootElementName = "NCVizDashDashboard";
    private const string NamespaceUri = "https://ncvizdash.local/schema/dashboard/v1";
    private const string IdAttribute = "id";

    private readonly Microsoft.Office.Interop.Excel.Application _excelApp;
    private readonly ILogger<WorkbookDashboardRepository> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>Initialises the repository against the live Excel Application object.</summary>
    public WorkbookDashboardRepository(Microsoft.Office.Interop.Excel.Application excelApp, ILogger<WorkbookDashboardRepository> logger)
    {
        _excelApp = excelApp;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<Dashboard>> GetAllAsync(CancellationToken ct = default)
    {
        var results = new List<Dashboard>();
        var workbook = _excelApp.ActiveWorkbook;
        if (workbook is null) return Task.FromResult<IReadOnlyList<Dashboard>>(results);

        foreach (CustomXMLPart part in workbook.CustomXMLParts)
        {
            ct.ThrowIfCancellationRequested();
            var dashboard = TryDeserialize(part);
            if (dashboard is not null)
                results.Add(dashboard);
        }

        _logger.LogInformation("Loaded {Count} dashboard(s) from workbook custom XML parts.", results.Count);
        return Task.FromResult<IReadOnlyList<Dashboard>>(results);
    }

    /// <inheritdoc/>
    public async Task<Dashboard?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        return all.FirstOrDefault(d => d.Id == id);
    }

    /// <inheritdoc/>
    public Task SaveAsync(Dashboard dashboard, CancellationToken ct = default)
    {
        var workbook = _excelApp.ActiveWorkbook;
        if (workbook is null)
        {
            _logger.LogWarning("SaveAsync called with no active workbook; dashboard '{Name}' not saved.", dashboard.Name);
            return Task.CompletedTask;
        }

        // Custom XML Parts have no in-place "replace content" API via Interop —
        // delete the existing part for this dashboard ID (if any) and add a fresh one.
        RemoveExistingPart(workbook, dashboard.Id);

        var xml = Serialize(dashboard);
        workbook.CustomXMLParts.Add(xml);

        _logger.LogInformation("Dashboard '{Name}' ({Id}) saved to workbook.", dashboard.Name, dashboard.Id);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var workbook = _excelApp.ActiveWorkbook;
        if (workbook is null) return Task.CompletedTask;

        var removed = RemoveExistingPart(workbook, id);
        if (removed)
            _logger.LogInformation("Dashboard {Id} deleted from workbook.", id);

        return Task.CompletedTask;
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    private static string Serialize(Dashboard dashboard)
    {
        var json = JsonSerializer.Serialize(dashboard, JsonOptions);

        var element = new XElement(XName.Get(RootElementName, NamespaceUri),
            new XAttribute(IdAttribute, dashboard.Id.ToString("D")),
            json);

        return new XDocument(element).ToString(SaveOptions.DisableFormatting);
    }

    private Dashboard? TryDeserialize(CustomXMLPart part)
    {
        try
        {
            var doc = XDocument.Parse(part.XML);
            var root = doc.Root;
            if (root is null || root.Name.LocalName != RootElementName || root.Name.NamespaceName != NamespaceUri)
                return null; // not one of ours — workbook may have unrelated custom XML parts

            var json = root.Value;
            var dashboard = JsonSerializer.Deserialize<Dashboard>(json, JsonOptions);
            return dashboard;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse a custom XML part as an NC VizDash dashboard; skipped.");
            return null;
        }
    }

    private bool RemoveExistingPart(Workbook workbook, Guid dashboardId)
    {
        foreach (CustomXMLPart part in workbook.CustomXMLParts)
        {
            try
            {
                var doc = XDocument.Parse(part.XML);
                var root = doc.Root;
                if (root is null || root.Name.LocalName != RootElementName || root.Name.NamespaceName != NamespaceUri)
                    continue;

                var idAttr = root.Attribute(IdAttribute)?.Value;
                if (idAttr is not null && Guid.TryParse(idAttr, out var existingId) && existingId == dashboardId)
                {
                    part.Delete();
                    return true;
                }
            }
            catch
            {
                // Not parseable / not ours — ignore and keep scanning.
            }
        }

        return false;
    }
}
