using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Ai;

/// <summary>Anthropic's Messages API — a different request/response shape from the OpenAI-compatible providers.</summary>
public sealed class AnthropicProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly IAppSettingsProvider _settings;
    private readonly ILogger<AnthropicProvider> _logger;

    private const string Endpoint = "https://api.anthropic.com/v1/messages";

    public string ProviderId => "anthropic";

    public AnthropicProvider(HttpClient httpClient, IAppSettingsProvider settings, ILogger<AnthropicProvider> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<string> ExplainChartAsync(
        DashboardWidget widget, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, CancellationToken ct = default)
    {
        var sample = string.Join("; ", rows.Take(10).Select(r => string.Join(",", r.Select(kv => $"{kv.Key}={kv.Value}"))));
        var prompt = $"Explain in 2-3 plain-English sentences what this {widget.VisualType} chart titled " +
                     $"'{widget.Title}' shows, given this sample data: {sample}";
        return await CompleteAsync(prompt, ct);
    }

    public Task<IReadOnlyList<DashboardWidget>> SuggestWidgetsAsync(
        string prompt, DataSourceDescriptor dataSource, CancellationToken ct = default)
    {
        _logger.LogWarning("SuggestWidgetsAsync is not implemented for AnthropicProvider — returning no suggestions.");
        return Task.FromResult<IReadOnlyList<DashboardWidget>>([]);
    }

    public async Task<string> GenerateInsightsAsync(Dashboard dashboard, CancellationToken ct = default)
    {
        var widgetSummary = string.Join("; ", dashboard.Widgets.Select(w => $"{w.Title} ({w.VisualType})"));
        var prompt = $"Given a dashboard named '{dashboard.Name}' containing these widgets: {widgetSummary}, " +
                     "write 3 short bullet points a business user would find useful, based only on the widget names given.";
        return await CompleteAsync(prompt, ct);
    }

    public Task<IReadOnlyList<double>> ForecastAsync(IReadOnlyList<double> historicalValues, int periodsAhead, CancellationToken ct = default)
    {
        // Same deterministic linear-trend approach as the OpenAI-compatible providers —
        // forecasting is numeric extrapolation, not a language-model task.
        if (historicalValues.Count < 2)
            return Task.FromResult<IReadOnlyList<double>>(Enumerable.Repeat(historicalValues.LastOrDefault(), periodsAhead).ToList());

        var n = historicalValues.Count;
        var xs = Enumerable.Range(0, n).Select(i => (double)i).ToList();
        var xMean = xs.Average();
        var yMean = historicalValues.Average();
        var slope = xs.Zip(historicalValues, (x, y) => (x - xMean) * (y - yMean)).Sum() / xs.Sum(x => (x - xMean) * (x - xMean));
        var intercept = yMean - slope * xMean;

        return Task.FromResult<IReadOnlyList<double>>(
            Enumerable.Range(n, periodsAhead).Select(i => intercept + slope * i).ToList());
    }

    private async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            model = "claude-sonnet-4-6",
            max_tokens = 300,
            messages = new[] { new { role = "user", content = prompt } }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _settings.Settings.AiApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anthropic completion request failed.");
            return "(AI request failed — check the configured API key.)";
        }
    }
}
