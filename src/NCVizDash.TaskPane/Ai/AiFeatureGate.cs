using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions;
using NCVizDash.Models;

namespace NCVizDash.TaskPane.Ai;

/// <summary>
/// The single enforcement point for "AI must always remain optional." Every AI
/// entry point in the UI (an "Explain Chart" button, a natural-language dashboard
/// prompt, etc. — none of which exist as wired UI yet in this pass, but any future
/// one MUST go through this gate) calls <see cref="TryGetProvider"/> rather than
/// resolving <see cref="IAiProvider"/> directly, so there is exactly one place that
/// decides whether AI is actually allowed to run.
/// </summary>
public sealed class AiFeatureGate
{
    private readonly IAppSettingsProvider _settings;
    private readonly IReadOnlyDictionary<string, IAiProvider> _providersById;
    private readonly ILogger<AiFeatureGate> _logger;

    /// <summary>Initialises the gate with settings, registered providers, and a logger.</summary>
    public AiFeatureGate(
        IAppSettingsProvider settings,
        IEnumerable<IAiProvider> providers,
        ILogger<AiFeatureGate> logger)
    {
        _settings = settings;
        _providersById = providers.ToDictionary(p => p.ProviderId, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    /// <summary>True only when the user has explicitly enabled AI and configured a known provider.</summary>
    public bool IsAvailable =>
        _settings.Settings.AiEnabled &&
        !string.IsNullOrWhiteSpace(_settings.Settings.AiProvider) &&
        _providersById.ContainsKey(_settings.Settings.AiProvider);

    /// <summary>Returns the configured provider if AI is enabled and available, otherwise null.</summary>
    public IAiProvider? TryGetProvider()
    {
        if (!IsAvailable)
        {
            _logger.LogDebug("AI feature requested but not available (AiEnabled={Enabled}, Provider='{Provider}').",
                _settings.Settings.AiEnabled, _settings.Settings.AiProvider);
            return null;
        }

        return _providersById[_settings.Settings.AiProvider];
    }
}
