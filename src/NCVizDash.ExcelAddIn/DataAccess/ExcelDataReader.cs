using Microsoft.Extensions.Logging;
using Microsoft.Office.Interop.Excel;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Classification;
using NCVizDash.Models;

namespace NCVizDash.ExcelAddIn.DataAccess;

/// <summary>
/// Reads data sources directly from the active Excel workbook using COM Interop:
/// ListObjects (Tables), Named Ranges, and plain Worksheets (used-range fallback).
/// Each source's columns are classified into <see cref="FieldType"/> automatically.
/// </summary>
public sealed class ExcelDataReader : IExcelDataReader
{
    private readonly Microsoft.Office.Interop.Excel.Application _excelApp;
    private readonly ILogger<ExcelDataReader> _logger;

    /// <summary>
    /// Cache mapping a generated <see cref="DataSourceDescriptor.Id"/> back to its
    /// originating Excel range, so subsequent reads don't need re-discovery.
    /// </summary>
    private readonly Dictionary<Guid, DataSourceLocation> _sourceLocations = new();

    /// <summary>Initialises the reader against the live Excel Application object.</summary>
    public ExcelDataReader(Microsoft.Office.Interop.Excel.Application excelApp, ILogger<ExcelDataReader> logger)
    {
        _excelApp = excelApp;
        _logger = logger;
    }

    // ── IExcelDataReader ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<IReadOnlyList<DataSourceDescriptor>> GetDataSourcesAsync(CancellationToken ct = default)
    {
        var results = new List<DataSourceDescriptor>();
        _sourceLocations.Clear();

        var workbook = _excelApp.ActiveWorkbook;
        if (workbook is null)
        {
            _logger.LogWarning("No active workbook; returning empty data source list.");
            return Task.FromResult<IReadOnlyList<DataSourceDescriptor>>(results);
        }

        try
        {
            foreach (Worksheet sheet in workbook.Worksheets)
            {
                ct.ThrowIfCancellationRequested();

                // 1. Excel Tables (ListObjects) — highest fidelity, has real headers.
                foreach (ListObject table in sheet.ListObjects)
                {
                    var descriptor = BuildDescriptorFromListObject(table, sheet.Name);
                    if (descriptor is not null)
                        results.Add(descriptor);
                }

                // 2. Worksheet-scoped Named Ranges (skip ones already covered by a table).
                foreach (Name name in sheet.Names)
                {
                    var descriptor = BuildDescriptorFromNamedRange(name, sheet.Name, isWorkbookScoped: false);
                    if (descriptor is not null)
                        results.Add(descriptor);
                }
            }

            // 3. Workbook-scoped Named Ranges.
            foreach (Name name in workbook.Names)
            {
                var descriptor = BuildDescriptorFromNamedRange(name, sheetName: null, isWorkbookScoped: true);
                if (descriptor is not null)
                    results.Add(descriptor);
            }

            _logger.LogInformation("Discovered {Count} data source(s) in workbook '{Workbook}'.",
                results.Count, workbook.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate data sources.");
        }

        return Task.FromResult<IReadOnlyList<DataSourceDescriptor>>(results);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(
        Guid dataSourceId, CancellationToken ct = default)
    {
        if (!_sourceLocations.TryGetValue(dataSourceId, out var location))
        {
            _logger.LogWarning("ReadRowsAsync called for unknown data source {Id}.", dataSourceId);
            return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(
                Array.Empty<IReadOnlyDictionary<string, object?>>());
        }

        var rows = ReadRangeAsRows(location.DataRange, location.Headers);
        return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(rows);
    }

    // ── Excel Table (ListObject) handling ────────────────────────────────────

    private DataSourceDescriptor? BuildDescriptorFromListObject(ListObject table, string sheetName)
    {
        try
        {
            if (table.DataBodyRange is null || table.HeaderRowRange is null)
            {
                _logger.LogDebug("Table '{Table}' on '{Sheet}' has no data; skipping.", table.Name, sheetName);
                return null;
            }

            var headers = ExtractHeaders(table.HeaderRowRange);
            var descriptor = new DataSourceDescriptor
            {
                Name = table.Name,
                SourceType = "ExcelTable",
                SheetName = sheetName,
                RowCount = table.DataBodyRange.Rows.Count,
                LastLoaded = DateTimeOffset.UtcNow
            };

            ClassifyAndAttachFields(descriptor, headers, table.DataBodyRange);

            _sourceLocations[descriptor.Id] = new DataSourceLocation(table.DataBodyRange, headers);
            return descriptor;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read table '{Table}' on '{Sheet}'.", table.Name, sheetName);
            return null;
        }
    }

    // ── Named Range handling ──────────────────────────────────────────────────

    private DataSourceDescriptor? BuildDescriptorFromNamedRange(Name name, string? sheetName, bool isWorkbookScoped)
    {
        try
        {
            // Skip hidden/internal names (e.g. print areas, table-backing names like "_xlnm.*").
            if (name.Name.StartsWith("_xlnm", StringComparison.OrdinalIgnoreCase))
                return null;

            Range range;
            try
            {
                range = name.RefersToRange;
            }
            catch
            {
                // RefersTo isn't a simple range (e.g. it's a formula) — not a usable data source.
                return null;
            }

            if (range is null || range.Rows.Count < 2) // need at least a header + 1 data row
                return null;

            var headerRow = (Range)range.Rows[1];
            var dataRange = range.Rows.Count > 1
                ? range.Resize[range.Rows.Count - 1, range.Columns.Count].Offset[1, 0]
                : range;

            var headers = ExtractHeaders(headerRow);
            var resolvedSheetName = sheetName ?? ((Worksheet)range.Worksheet).Name;

            var descriptor = new DataSourceDescriptor
            {
                Name = CleanNamedRangeDisplayName(name.Name),
                SourceType = "NamedRange",
                SheetName = resolvedSheetName,
                RowCount = dataRange.Rows.Count,
                LastLoaded = DateTimeOffset.UtcNow
            };

            ClassifyAndAttachFields(descriptor, headers, dataRange);

            _sourceLocations[descriptor.Id] = new DataSourceLocation(dataRange, headers);
            return descriptor;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read named range '{Name}'.", name.Name);
            return null;
        }
    }

    /// <summary>
    /// Named ranges are often qualified like "Sheet1!MyRange" — strip the sheet
    /// qualifier for a cleaner display name in the explorer.
    /// </summary>
    private static string CleanNamedRangeDisplayName(string rawName)
    {
        var bangIndex = rawName.IndexOf('!');
        return bangIndex >= 0 ? rawName.Substring(bangIndex + 1) : rawName;
    }

    // ── Shared classification + extraction logic ─────────────────────────────

    private void ClassifyAndAttachFields(DataSourceDescriptor descriptor, IReadOnlyList<string> headers, Range dataRange)
    {
        for (var col = 0; col < headers.Count; col++)
        {
            var isDateFormatted = IsColumnDateFormatted(dataRange, col);
            var sample = SampleColumnValues(dataRange, col, maxSamples: 25, isDateFormatted);
            var fieldType = FieldTypeClassifier.ClassifyFromSample(headers[col], sample);

            descriptor.Fields.Add(new FieldDescriptor
            {
                Name = headers[col],
                DisplayName = headers[col],
                ClrType = sample.FirstOrDefault(v => v is not null)?.GetType().FullName ?? "System.Object",
                FieldType = fieldType
            });
        }
    }

    /// <summary>
    /// Inspects the cell number format of the first data cell in a column to determine
    /// whether Excel is displaying it as a date/time. OLE date doubles are only converted
    /// to <see cref="DateTime"/> when this returns true, avoiding false positives on
    /// plain numeric columns that happen to fall in a plausible date-serial range.
    /// </summary>
    private static bool IsColumnDateFormatted(Range dataRange, int zeroBasedColIndex)
    {
        try
        {
            var cell = (Range)dataRange.Cells[1, zeroBasedColIndex + 1];
            var format = cell.NumberFormat as string ?? string.Empty;

            return format.Contains('y', StringComparison.OrdinalIgnoreCase) ||
                   (format.Contains('d', StringComparison.OrdinalIgnoreCase) &&
                    format.Contains('m', StringComparison.OrdinalIgnoreCase)) ||
                   format.Contains("h:mm", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static List<string> ExtractHeaders(Range headerRange)
    {
        var values = (object[,])headerRange.Value2;
        var headers = new List<string>();
        var cols = values.GetLength(1);

        for (var c = 1; c <= cols; c++)
        {
            var raw = values[1, c];
            headers.Add(raw?.ToString()?.Trim() ?? $"Column{c}");
        }

        return headers;
    }

    private static List<object?> SampleColumnValues(Range dataRange, int zeroBasedColIndex, int maxSamples, bool isDateFormatted)
    {
        var samples = new List<object?>();
        var rowCount = Math.Min(dataRange.Rows.Count, maxSamples);
        if (rowCount == 0) return samples;

        var sampleRange = (Range)dataRange.Resize[rowCount, 1].Offset[0, zeroBasedColIndex];
        var raw = sampleRange.Value2;

        if (rowCount == 1)
        {
            samples.Add(NormaliseCellValue(raw, isDateFormatted));
        }
        else
        {
            var values = (object[,])raw;
            for (var r = 1; r <= values.GetLength(0); r++)
                samples.Add(NormaliseCellValue(values[r, 1], isDateFormatted));
        }

        return samples;
    }

    private List<IReadOnlyDictionary<string, object?>> ReadRangeAsRows(Range dataRange, IReadOnlyList<string> headers)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var rowCount = dataRange.Rows.Count;
        var colCount = headers.Count;

        if (rowCount == 0 || colCount == 0)
            return rows;

        // Determine per-column date formatting once, up front.
        var dateFlags = new bool[colCount];
        for (var c = 0; c < colCount; c++)
            dateFlags[c] = IsColumnDateFormatted(dataRange, c);

        var raw = dataRange.Value2;

        if (rowCount == 1 && colCount == 1)
        {
            rows.Add(new Dictionary<string, object?> { [headers[0]] = NormaliseCellValue(raw, dateFlags[0]) });
            return rows;
        }

        var values = (object[,])raw;
        for (var r = 1; r <= rowCount; r++)
        {
            var row = new Dictionary<string, object?>(colCount);
            for (var c = 1; c <= colCount; c++)
                row[headers[c - 1]] = NormaliseCellValue(values[r, c], dateFlags[c - 1]);
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Converts Excel's raw COM value into a proper CLR type. OLE date serials are
    /// converted to <see cref="DateTime"/> only when <paramref name="isDateFormatted"/>
    /// is true (i.e. the source cell's NumberFormat indicates a date/time display).
    /// </summary>
    private static object? NormaliseCellValue(object? raw, bool isDateFormatted)
    {
        switch (raw)
        {
            case null:
                return null;

            case string s when string.IsNullOrWhiteSpace(s):
                return null;

            case double d when isDateFormatted:
                try
                {
                    return DateTime.FromOADate(d);
                }
                catch (ArgumentException)
                {
                    return d; // out-of-range OLE date; fall back to raw number
                }

            case double d:
                return d;

            case bool b:
                return b;

            case string str:
                return str.Trim();

            default:
                return raw;
        }
    }

    /// <summary>Holds the resolved Excel range + header list for a previously discovered data source.</summary>
    private sealed record DataSourceLocation(Range DataRange, IReadOnlyList<string> Headers);
}
