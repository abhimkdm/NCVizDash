using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Ai;

/// <summary>
/// Base implementation shared by every OpenAI-Chat-Completions-compatible provider
/// (OpenAI itself, Azure OpenAI, and most local LLM servers like Ollama/LM Studio
/// all speak this same request/response shape). Subclasses only need to supply the
/// endpoint URL and auth header. Anthropic's Messages API has a different request
/// shape and gets its own implementation (<see cref="AnthropicProvider"/>).
/// </summary>
public abstract class OpenAiCompatibleProvider : IAiProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly IAppSettingsProvider Settings;
    protected readonly ILogger Logger;

    public abstract string ProviderId { get; }
    protected abstract string Endpoint { get; }
    protected abstract void ApplyAuth(HttpRequestMessage request);

    protected OpenAiCompatibleProvider(HttpClient httpClient, IAppSettingsProvider settings, ILogger logger)
    {
        HttpClient = httpClient;
        Settings = settings;
        Logger = logger;
    }

    public async Task<string> ExplainChartAsync(
        DashboardWidget widget, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, CancellationToken ct = default)
    {
        var sample = string.Join("; ", rows.Take(10).Select(r => string.Join(",", r.Select(kv => $"{kv.Key}={kv.Value}"))));
        var prompt = $"Explain in 2-3 plain-English sentences what this {widget.VisualType} chart titled " +
                     $"'{widget.Title}' shows, given this sample data: {sample}";

        return await CompleteAsync(prompt, ct);
    }

    public async Task<IReadOnlyList<DashboardWidget>> SuggestWidgetsAsync(
        string prompt, DataSourceDescriptor dataSource, CancellationToken ct = default)
    {
        // Deliberately not implemented beyond a documented no-op: turning free text
        // into safe, valid widget definitions needs strict output-schema validation
        // (reject anything that doesn't map to real fields on dataSource) which is
        // a correctness-critical feature deserving its own dedicated pass rather
        // than a quick LLM-json-parsing implementation bolted on here.
        Logger.LogWarning("SuggestWidgetsAsync called but is not implemented in this provider — returning no suggestions.");
        return await Task.FromResult<IReadOnlyList<DashboardWidget>>([]);
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
        // Simple, deterministic linear-trend forecast — no LLM call needed for numeric
        // extrapolation, and it's both cheaper and more reliable than asking a chat
        // model to "predict the next N numbers" from a text prompt.
        if (historicalValues.Count < 2)
            return Task.FromResult<IReadOnlyList<double>>(Enumerable.Repeat(historicalValues.LastOrDefault(), periodsAhead).ToList());

        var n = historicalValues.Count;
        var xs = Enumerable.Range(0, n).Select(i => (double)i).ToList();
        var xMean = xs.Average();
        var yMean = historicalValues.Average();

        var slope = xs.Zip(historicalValues, (x, y) => (x - xMean) * (y - yMean)).Sum() /
                    xs.Sum(x => (x - xMean) * (x - xMean));
        var intercept = yMean - slope * xMean;

        var forecast = Enumerable.Range(n, periodsAhead).Select(i => intercept + slope * i).ToList();
        return Task.FromResult<IReadOnlyList<double>>(forecast);
    }

    protected async Task<string> CompleteAsync(string prompt, CancellationToken ct)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            model = "gpt-4o-mini",
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 300
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        ApplyAuth(request);

        try
        {
            using var response = await HttpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{Provider} completion request failed.", ProviderId);
            return "(AI request failed — check the configured endpoint and API key.)";
        }
    }
}

/// <summary>OpenAI's public API (api.openai.com).</summary>
public sealed class OpenAiProvider(HttpClient httpClient, IAppSettingsProvider settings, ILogger<OpenAiProvider> logger)
    : OpenAiCompatibleProvider(httpClient, settings, logger)
{
    public override string ProviderId => "openai";
    protected override string Endpoint => "https://api.openai.com/v1/chat/completions";
    protected override void ApplyAuth(HttpRequestMessage request) =>
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Settings.Settings.AiApiKey);
}

/// <summary>Azure OpenAI — endpoint is the user's own Azure resource + deployment URL.</summary>
public sealed class AzureOpenAiProvider(HttpClient httpClient, IAppSettingsProvider settings, ILogger<AzureOpenAiProvider> logger)
    : OpenAiCompatibleProvider(httpClient, settings, logger)
{
    public override string ProviderId => "azure-openai";
    protected override string Endpoint => Settings.Settings.AiEndpoint;
    protected override void ApplyAuth(HttpRequestMessage request) =>
        request.Headers.Add("api-key", Settings.Settings.AiApiKey);
}

/// <summary>A local LLM server exposing an OpenAI-compatible endpoint (Ollama, LM Studio, etc.).</summary>
public sealed class LocalLlmProvider(HttpClient httpClient, IAppSettingsProvider settings, ILogger<LocalLlmProvider> logger)
    : OpenAiCompatibleProvider(httpClient, settings, logger)
{
    public override string ProviderId => "local";
    protected override string Endpoint => string.IsNullOrWhiteSpace(Settings.Settings.AiEndpoint)
        ? "http://localhost:11434/v1/chat/completions" // Ollama's default OpenAI-compatible port
        : Settings.Settings.AiEndpoint;
    protected override void ApplyAuth(HttpRequestMessage request) { /* local servers typically need no auth */ }
}
