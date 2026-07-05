using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Classification;
using NCVizDash.Models;

namespace NCVizDash.Connectors.Rest;

/// <summary>
/// Generic REST API connector: GETs a JSON endpoint and flattens the response into
/// rows, exactly like <see cref="Connectors.Json.JsonFileConnector"/> does for a
/// local file. <c>connectionInfo</c> is the full request URL. If the
/// response's top level is an object with a single array property (a very common
/// API shape — <c>{ "data": [...] }</c>, <c>{ "results": [...] }</c>, etc.), that
/// array is used automatically; otherwise the response must be a bare top-level array.
/// </summary>
public sealed class RestApiConnector : IDataConnector
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RestApiConnector> _logger;

    /// <inheritdoc/>
    public string ConnectorType => "rest";

    /// <summary>Initializes a new instance of the <see cref="RestApiConnector"/> class.</summary>
    /// <param name="httpClient">HTTP client used to issue GET requests.</param>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public RestApiConnector(HttpClient httpClient, ILogger<RestApiConnector> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DataSourceDescriptor>> DiscoverAsync(string connectionInfo, CancellationToken ct = default)
    {
        var rows = await FetchAsync(connectionInfo, ct);
        if (rows.Count == 0) return [];

        var uri = new Uri(connectionInfo);
        var descriptor = new DataSourceDescriptor
        {
            Name = uri.Host + uri.AbsolutePath,
            SourceType = "RestApi",
            SheetName = string.Empty,
            RowCount = rows.Count
        };

        var headers = rows.SelectMany(r => r.Keys).Distinct().ToList();
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
        DataSourceDescriptor descriptor, string connectionInfo, CancellationToken ct = default) =>
        await FetchAsync(connectionInfo, ct);

    private async Task<List<IReadOnlyDictionary<string, object?>>> FetchAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(), cancellationToken: ct);
            var array = LocateArray(doc.RootElement);

            var result = new List<IReadOnlyDictionary<string, object?>>();
            foreach (var element in array.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;

                var row = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                    row[prop.Name] = ExtractValue(prop.Value);
                result.Add(row);
            }

            _logger.LogInformation("REST connector fetched {Count} row(s) from '{Url}'.", result.Count, url);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "REST connector failed to fetch '{Url}'.", url);
            return [];
        }
    }

    private static JsonElement LocateArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
                if (prop.Value.ValueKind == JsonValueKind.Array)
                    return prop.Value;
        }

        throw new InvalidDataException("REST response did not contain a top-level or single-property array of records.");
    }

    private static object? ExtractValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => element.GetRawText()
    };
}
