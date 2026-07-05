using System.IO;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Classification;
using NCVizDash.Models;

namespace NCVizDash.Connectors.Csv;

/// <summary>
/// Reads a local CSV file into a <see cref="DataSourceDescriptor"/> + rows.
/// <c>connectionInfo</c> is the file path. Handles a minimal but
/// correct CSV dialect: comma-separated, double-quote-escaped fields, embedded
/// commas/newlines inside quotes, and "" as an escaped quote — enough for the vast
/// majority of real-world exports without pulling in a full CSV library dependency.
/// </summary>
public sealed class CsvFileConnector : IDataConnector
{
    private readonly ILogger<CsvFileConnector> _logger;

    /// <inheritdoc/>
    public string ConnectorType => "csv";

    /// <summary>Initializes a new instance of the <see cref="CsvFileConnector"/> class.</summary>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public CsvFileConnector(ILogger<CsvFileConnector> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DataSourceDescriptor>> DiscoverAsync(string connectionInfo, CancellationToken ct = default)
    {
        if (!File.Exists(connectionInfo))
        {
            _logger.LogWarning("CSV file not found: {Path}", connectionInfo);
            return [];
        }

        var rows = await ParseAsync(connectionInfo, ct);
        if (rows.Count == 0) return [];

        var headers = rows[0].Keys.ToList();
        var descriptor = new DataSourceDescriptor
        {
            Name = Path.GetFileNameWithoutExtension(connectionInfo),
            SourceType = "Csv",
            SheetName = string.Empty,
            RowCount = rows.Count - 1
        };

        foreach (var header in headers)
        {
            var sample = rows.Skip(1).Take(25).Select(r => r.TryGetValue(header, out var v) ? (object?)v : null);
            descriptor.Fields.Add(new FieldDescriptor
            {
                Name = header,
                DisplayName = header,
                ClrType = "System.String",
                FieldType = FieldTypeClassifier.ClassifyFromSample(header, sample)
            });
        }

        return [descriptor];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(
        DataSourceDescriptor descriptor, string connectionInfo, CancellationToken ct = default)
    {
        var all = await ParseAsync(connectionInfo, ct);
        return all.Skip(1).Select(r => (IReadOnlyDictionary<string, object?>)r).ToList();
    }

    // ── CSV parsing ───────────────────────────────────────────────────────────

    /// <summary>Returns every row (including the header row at index 0) as field→string dictionaries.</summary>
    private async Task<List<Dictionary<string, object?>>> ParseAsync(string path, CancellationToken ct)
    {
        string text;
        using (var reader = new StreamReader(path))
            text = await reader.ReadToEndAsync();
        ct.ThrowIfCancellationRequested();

        var records = ParseCsv(text);
        if (records.Count == 0) return [];

        var headers = records[0];
        var result = new List<Dictionary<string, object?>>();

        foreach (var record in records)
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < headers.Count; i++)
                row[headers[i]] = i < record.Count ? (object?)record[i] : null;
            result.Add(row);
        }

        return result;
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var records = new List<List<string>>();
        var field = new System.Text.StringBuilder();
        var record = new List<string>();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                switch (c)
                {
                    case '"': inQuotes = true; break;
                    case ',': record.Add(field.ToString()); field.Clear(); break;
                    case '\r': break;
                    case '\n':
                        record.Add(field.ToString()); field.Clear();
                        if (record.Count > 1 || !string.IsNullOrEmpty(record[0]))
                            records.Add(record);
                        record = [];
                        break;
                    default: field.Append(c); break;
                }
            }
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }
}
