using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Classification;
using NCVizDash.Models;

namespace NCVizDash.Connectors.Json;

/// <summary>
/// Reads a local JSON file (a top-level array of flat objects) into a
/// <see cref="DataSourceDescriptor"/> + rows. <c>connectionInfo</c> is
/// the file path. Nested objects/arrays within a record are flattened to a JSON
/// string value rather than expanded into further columns, keeping the column set
/// stable across records that don't all share the same nested shape.
/// </summary>
public sealed class JsonFileConnector : IDataConnector
{
    private readonly ILogger<JsonFileConnector> _logger;

    /// <inheritdoc/>
    public string ConnectorType => "json";

    /// <summary>Initializes a new instance of the <see cref="JsonFileConnector"/> class.</summary>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public JsonFileConnector(ILogger<JsonFileConnector> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DataSourceDescriptor>> DiscoverAsync(string connectionInfo, CancellationToken ct = default)
    {
        if (!File.Exists(connectionInfo))
        {
            _logger.LogWarning("JSON file not found: {Path}", connectionInfo);
            return [];
        }

        var rows = await ParseAsync(connectionInfo, ct);
        if (rows.Count == 0) return [];

        var headers = rows.SelectMany(r => r.Keys).Distinct().ToList();
        var descriptor = new DataSourceDescriptor
        {
            Name = Path.GetFileNameWithoutExtension(connectionInfo),
            SourceType = "Json",
            SheetName = string.Empty,
            RowCount = rows.Count
        };

        foreach (var header in headers)
        {
            var sample = rows.Take(25).Select(r => r.TryGetValue(header, out var v) ? v : null);
            descriptor.Fields.Add(new FieldDescriptor
            {
                Name = header,
                DisplayName = header,
                ClrType = "System.Object",
                FieldType = FieldTypeClassifier.ClassifyFromSample(header, sample)
            });
        }

        return [descriptor];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(
        DataSourceDescriptor descriptor, string connectionInfo, CancellationToken ct = default)
    {
        var rows = await ParseAsync(connectionInfo, ct);
        return rows.Select(r => (IReadOnlyDictionary<string, object?>)r).ToList();
    }

    private static async Task<List<Dictionary<string, object?>>> ParseAsync(string path, CancellationToken ct)
    {
        using var stream = File.OpenRead(path);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var root = doc.RootElement;
        var elements = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray()
            : throw new InvalidDataException("Expected a top-level JSON array of objects.");

        var result = new List<Dictionary<string, object?>>();
        foreach (var element in elements)
        {
            if (element.ValueKind != JsonValueKind.Object) continue;

            var row = new Dictionary<string, object?>();
            foreach (var prop in element.EnumerateObject())
                row[prop.Name] = ExtractValue(prop.Value);

            result.Add(row);
        }

        return result;
    }

    private static object? ExtractValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        // Nested structures: preserve as raw JSON text rather than expanding —
        // keeps the flat row shape stable across heterogeneous records.
        _ => element.GetRawText()
    };
}
