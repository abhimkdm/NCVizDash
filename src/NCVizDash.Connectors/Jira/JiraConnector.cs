using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Core.Classification;
using NCVizDash.Models;

namespace NCVizDash.Connectors.Jira;

/// <summary>
/// Jira Cloud REST API connector using JQL. Implements <see cref="IDataConnector"/>
/// — the same contract every other connector satisfies — so once a JQL query is
/// imported, the dashboard engine cannot tell a Jira-sourced widget from an
/// Excel-sourced one: both are just a <see cref="DataSourceDescriptor"/> loaded
/// into <see cref="IAnalyticsEngine"/>, queried via the exact same
/// <c>WidgetRenderCoordinator</c> → <c>QuerySpec</c> → DuckDB pipeline. This is
/// the "Excel and Jira should not be distinguished" requirement satisfied
/// structurally, not by special-casing Jira anywhere downstream.
/// <para>
/// <c>connectionInfo</c> for both interface methods is a composite
/// string: <c>"&lt;profileId&gt;||&lt;jql&gt;"</c> — see <c>JqlQueryService</c>
/// for the higher-level API that builds this correctly; call this connector
/// directly only if you're constructing that string yourself.
/// </para>
/// </summary>
public sealed class JiraConnector : IDataConnector
{
    private readonly HttpClient _httpClient;
    private readonly JiraConnectionProfileStore _profileStore;
    private readonly ILogger<JiraConnector> _logger;

    private const int MaxResultsPerPage = 100;

    /// <inheritdoc/>
    public string ConnectorType => "jira";

    /// <summary>Initializes a new instance of the <see cref="JiraConnector"/> class.</summary>
    /// <param name="httpClient">HTTP client used to issue requests against the Jira REST API.</param>
    /// <param name="profileStore">Store used to resolve saved connection profiles by ID.</param>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public JiraConnector(HttpClient httpClient, JiraConnectionProfileStore profileStore, ILogger<JiraConnector> logger)
    {
        _httpClient = httpClient;
        _profileStore = profileStore;
        _logger = logger;
    }

    /// <summary>Tests a connection profile by hitting Jira's <c>/myself</c> endpoint. Returns null on success, an error message otherwise.</summary>
    public async Task<string?> TestConnectionAsync(JiraConnectionProfile profile, CancellationToken ct = default)
    {
        try
        {
            using var request = BuildRequest(profile, "/rest/api/2/myself");
            using var response = await _httpClient.SendAsync(request, ct);

            return response.IsSuccessStatusCode
                ? null
                : $"Jira returned {(int)response.StatusCode} {response.ReasonPhrase}. Check the URL, email, and API token.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Jira connection test failed for '{Name}'.", profile.ConnectionName);
            return $"Connection failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Validates JQL syntax by issuing the query with <c>maxResults=0</c> — Jira
    /// itself is the validator, so this catches every real syntax/field error
    /// without reimplementing JQL grammar client-side.
    /// </summary>
    public async Task<string?> ValidateJqlAsync(JiraConnectionProfile profile, string jql, CancellationToken ct = default)
    {
        try
        {
            using var request = BuildRequest(profile, "/rest/api/2/search",
                new Dictionary<string, string> { ["jql"] = jql, ["maxResults"] = "0" });
            using var response = await _httpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            var message = TryExtractErrorMessage(body) ?? $"{(int)response.StatusCode} {response.ReasonPhrase}";
            return message;
        }
        catch (Exception ex)
        {
            return $"Validation request failed: {ex.Message}";
        }
    }

    /// <summary>Runs a JQL query and returns up to <paramref name="maxResults"/> issues as flattened rows.</summary>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteJqlAsync(
        JiraConnectionProfile profile, string jql, int maxResults = 500, CancellationToken ct = default)
    {
        var results = new List<IReadOnlyDictionary<string, object?>>();
        var startAt = 0;

        while (results.Count < maxResults)
        {
            ct.ThrowIfCancellationRequested();

            var pageSize = Math.Min(MaxResultsPerPage, maxResults - results.Count);
            using var request = BuildRequest(profile, "/rest/api/2/search", new Dictionary<string, string>
            {
                ["jql"] = jql,
                ["startAt"] = startAt.ToString(),
                ["maxResults"] = pageSize.ToString()
            });

            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var issues = doc.RootElement.GetProperty("issues");
            var pageCount = 0;

            foreach (var issue in issues.EnumerateArray())
            {
                results.Add(FlattenIssue(issue));
                pageCount++;
            }

            _logger.LogDebug("Jira JQL page fetched: {Count} issue(s) (startAt={StartAt}).", pageCount, startAt);

            if (pageCount < pageSize) break; // fewer than requested → no more pages
            startAt += pageCount;
        }

        _logger.LogInformation("Jira JQL query returned {Count} issue(s).", results.Count);
        return results;
    }

    // ── IDataConnector ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DataSourceDescriptor>> DiscoverAsync(string connectionInfo, CancellationToken ct = default)
    {
        var (profile, jql) = ParseConnectionInfo(connectionInfo);
        var rows = await ExecuteJqlAsync(profile, jql, ct: ct);

        var descriptor = new DataSourceDescriptor
        {
            Name = $"Jira: {Truncate(jql, 40)}",
            SourceType = "Jira",
            SheetName = string.Empty,
            RowCount = rows.Count
        };

        if (rows.Count > 0)
        {
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
        }

        return [descriptor];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(
        DataSourceDescriptor descriptor, string connectionInfo, CancellationToken ct = default)
    {
        var (profile, jql) = ParseConnectionInfo(connectionInfo);
        return await ExecuteJqlAsync(profile, jql, ct: ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private (JiraConnectionProfile Profile, string Jql) ParseConnectionInfo(string connectionInfo)
    {
        var parts = connectionInfo.Split(new[] { "||" }, 2, StringSplitOptions.None);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var profileId))
            throw new ArgumentException("connectionInfo must be \"<profileId>||<jql>\" for JiraConnector.");

        var profile = _profileStore.LoadAll().FirstOrDefault(p => p.Id == profileId)
            ?? throw new InvalidOperationException($"No saved Jira connection profile with ID {profileId}.");

        return (profile, parts[1]);
    }

    private HttpRequestMessage BuildRequest(JiraConnectionProfile profile, string path, Dictionary<string, string>? query = null)
    {
        var url = profile.JiraUrl.TrimEnd('/') + path;
        if (query is { Count: > 0 })
            url += "?" + string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Jira Cloud uses HTTP Basic auth with the user's email + API token.
        var authBytes = Encoding.UTF8.GetBytes($"{profile.Email}:{profile.ApiToken}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return request;
    }

    /// <summary>
    /// Flattens a Jira issue JSON object into a single-level row: <c>key</c>, plus
    /// every scalar field under <c>fields</c> (nested objects like <c>assignee</c>
    /// or <c>status</c> are reduced to their human-readable <c>name</c>/<c>displayName</c>
    /// where present, otherwise their raw JSON, matching the same flattening
    /// philosophy as <c>JsonFileConnector</c>/<c>RestApiConnector</c>).
    /// </summary>
    private static Dictionary<string, object?> FlattenIssue(JsonElement issue)
    {
        var row = new Dictionary<string, object?> { ["key"] = issue.GetProperty("key").GetString() };

        if (!issue.TryGetProperty("fields", out var fields)) return row;

        foreach (var field in fields.EnumerateObject())
        {
            row[field.Name] = field.Value.ValueKind switch
            {
                JsonValueKind.String => field.Value.GetString(),
                JsonValueKind.Number => field.Value.TryGetInt64(out var l) ? l : field.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.Object => ExtractDisplayName(field.Value),
                _ => field.Value.GetRawText()
            };
        }

        return row;
    }

    private static object? ExtractDisplayName(JsonElement obj)
    {
        if (obj.TryGetProperty("displayName", out var dn)) return dn.GetString();
        if (obj.TryGetProperty("name", out var n)) return n.GetString();
        if (obj.TryGetProperty("value", out var v)) return v.GetString();
        return obj.GetRawText();
    }

    private static string? TryExtractErrorMessage(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("errorMessages", out var messages) && messages.GetArrayLength() > 0)
                return string.Join("; ", messages.EnumerateArray().Select(m => m.GetString()));
        }
        catch { /* not JSON or unexpected shape — fall through to status-code message */ }

        return null;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value.Substring(0, maxLength) + "…";
}
