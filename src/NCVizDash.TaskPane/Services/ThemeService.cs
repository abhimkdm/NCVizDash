using Microsoft.Extensions.Logging;

namespace NCVizDash.TaskPane.Services;

/// <summary>
/// Coordinates theme changes across the task pane.
/// <para>
/// Deliberately does NOT use MaterialDesignThemes' <c>PaletteHelper</c>, which by
/// default targets <c>System.Windows.Application.Current.Resources</c> — an object
/// that may not exist in a VSTO host process (Excel owns the message loop, not a
/// WPF <c>Application</c>). Instead, this service raises <see cref="ThemeChanged"/>,
/// and <see cref="Views.ShellWindow"/> applies the change directly to its own
/// <c>BundledTheme</c> resource instance, which is always present and safe to mutate
/// regardless of whether a WPF <c>Application</c> object exists.
/// </para>
/// </summary>
public sealed class ThemeService
{
    private readonly ILogger<ThemeService> _logger;

    /// <summary>Raised whenever a theme change is requested. Carries the new theme name.</summary>
    public event EventHandler<string>? ThemeChanged;

    /// <summary>Initialises the theme service.</summary>
    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger;
    }

    /// <summary>Requests that the given theme ("Light" or "Dark") be applied.</summary>
    public void ApplyTheme(string themeName)
    {
        _logger.LogInformation("Theme change requested: '{Theme}'.", themeName);
        ThemeChanged?.Invoke(this, themeName);
    }
}
