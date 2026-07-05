using Microsoft.Extensions.Logging;
using Microsoft.Office.Interop.Excel;

namespace NCVizDash.ExcelAddIn.DataAccess;

/// <summary>
/// Excel Snapshot export (Phase 13): pastes captured widget PNG images onto a new
/// worksheet in the active workbook, one picture per widget, stacked vertically.
/// This is the one export format that genuinely needs Excel Interop, so it lives
/// in the add-in host rather than <c>NCVizDash.TaskPane.Export.ExportService</c>
/// (which deliberately has no Excel dependency).
/// </summary>
public sealed class ExcelSnapshotExporter
{
    private readonly Microsoft.Office.Interop.Excel.Application _excelApp;
    private readonly ILogger<ExcelSnapshotExporter> _logger;

    public ExcelSnapshotExporter(Microsoft.Office.Interop.Excel.Application excelApp, ILogger<ExcelSnapshotExporter> logger)
    {
        _excelApp = excelApp;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new worksheet named "&lt;dashboardName&gt; Snapshot" and pastes each
    /// widget's PNG bytes onto it as a picture, stacked top-to-bottom with a title
    /// label above each. Returns the number of images placed.
    /// </summary>
    public int ExportSnapshot(string dashboardName, IReadOnlyDictionary<string, byte[]> widgetImages)
    {
        var workbook = _excelApp.ActiveWorkbook;
        if (workbook is null)
        {
            _logger.LogWarning("No active workbook; Excel snapshot export aborted.");
            return 0;
        }

        var sheetName = SanitiseSheetName($"{dashboardName} Snapshot");
        var sheet = (Worksheet)workbook.Worksheets.Add();
        sheet.Name = sheetName;

        var tempDir = Path.Combine(Path.GetTempPath(), "NCVizDashSnapshot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var topOffset = 10.0;
        var placed = 0;

        try
        {
            foreach (var kvp in widgetImages)
            {
                var tempFile = Path.Combine(tempDir, $"{placed}.png");
                File.WriteAllBytes(tempFile, kvp.Value);

                var labelCell = (Range)sheet.Cells[(int)(topOffset / 15) + 1, 1];
                labelCell.Value2 = kvp.Key;
                labelCell.Font.Bold = true;

                var picture = sheet.Shapes.AddPicture(
                    tempFile, MsoTriState.msoFalse, MsoTriState.msoCTrue,
                    10, (float)topOffset + 15, 480, 280);

                topOffset += picture.Height + 40;
                placed++;
            }

            _logger.LogInformation("Excel snapshot: placed {Count} image(s) on sheet '{Sheet}'.", placed, sheetName);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
        }

        return placed;
    }

    private static string SanitiseSheetName(string raw)
    {
        var invalid = new[] { '\\', '/', '?', '*', '[', ']', ':' };
        var cleaned = new string(raw.Where(c => !invalid.Contains(c)).ToArray());
        return cleaned.Length > 31 ? cleaned.Substring(0, 31) : cleaned;
    }
}
