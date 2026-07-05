using NCVizDash.Models;

namespace NCVizDash.Core.Abstractions;

/// <summary>
/// Optional AI capabilities (Phase 18) — natural-language dashboard creation,
/// insight narration, chart explanation, and simple forecasting. Per the product
/// vision ("No AI dependency in the core product... AI must always remain
/// optional"), nothing in Phases 1–17 depends on this interface, no service
/// implementing it is registered by default, and the UI never calls it unless
/// <see cref="AppSettings.AiEnabled"/> is explicitly turned on by the user AND a
/// provider is configured. See <c>AiFeatureGate</c> for the enforcement point.
/// </summary>
public interface IAiProvider
{
    /// <summary>Short identifier for this provider ("azure-openai", "openai", "anthropic", "local").</summary>
    string ProviderId { get; }

    /// <summary>Generates a short natural-language narrative describing what a widget's data shows.</summary>
    Task<string> ExplainChartAsync(DashboardWidget widget, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, CancellationToken ct = default);

    /// <summary>
    /// Given a natural-language prompt and the fields available on a data source,
    /// suggests a set of widgets (visual type + field mappings) — the human still
    /// reviews and applies them; this never silently mutates a dashboard.
    /// </summary>
    Task<IReadOnlyList<DashboardWidget>> SuggestWidgetsAsync(string prompt, DataSourceDescriptor dataSource, CancellationToken ct = default);

    /// <summary>Produces a short bullet-point summary of notable patterns across a dashboard's widgets.</summary>
    Task<string> GenerateInsightsAsync(Dashboard dashboard, CancellationToken ct = default);

    /// <summary>Forecasts the next <paramref name="periodsAhead"/> points of a time series (simple, single measure).</summary>
    Task<IReadOnlyList<double>> ForecastAsync(IReadOnlyList<double> historicalValues, int periodsAhead, CancellationToken ct = default);
}
