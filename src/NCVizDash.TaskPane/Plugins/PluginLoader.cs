using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions.Plugins;

namespace NCVizDash.TaskPane.Plugins;

/// <summary>
/// Scans <see cref="Models.AppSettings.PluginDirectory"/> for `.dll` files and, for
/// each one, discovers types implementing any of the four plugin interfaces
/// (<see cref="IChartPlugin"/>, <see cref="IWidgetPlugin"/>, <see cref="IDataSourcePlugin"/>,
/// <see cref="IThemePlugin"/>) via reflection.
/// <para>
/// <b>Isolation note:</b> true per-plugin isolation (a separate, collectible
/// <c>AssemblyLoadContext</c> per plugin so a broken/malicious plugin can't corrupt
/// the host's own loaded types, and so plugins could later be unloaded without
/// restarting Excel) is a .NET 5+ API and isn't available on .NET Framework 4.8,
/// which this host targets. Plugins are loaded directly into the default load
/// context via <see cref="Assembly.LoadFrom(string)"/> instead — the closest
/// equivalent on .NET Framework would be a separate <c>AppDomain</c>, but that
/// requires cross-domain marshaling for every plugin call, which is a much larger
/// scope change than this pass covers. Only load plugins you trust.
/// </para>
/// </summary>
public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly List<Assembly> _loadedAssemblies = [];

    /// <summary>Discovered chart plugins.</summary>
    public List<IChartPlugin> ChartPlugins { get; } = [];

    /// <summary>Discovered widget plugins.</summary>
    public List<IWidgetPlugin> WidgetPlugins { get; } = [];

    /// <summary>Discovered data source plugins.</summary>
    public List<IDataSourcePlugin> DataSourcePlugins { get; } = [];

    /// <summary>Discovered theme plugins.</summary>
    public List<IThemePlugin> ThemePlugins { get; } = [];

    /// <summary>Initializes a new instance of the <see cref="PluginLoader"/> class.</summary>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>Scans the given directory for plugin DLLs and loads every discoverable plugin type.</summary>
    public void LoadFrom(string pluginDirectory)
    {
        var expanded = Environment.ExpandEnvironmentVariables(pluginDirectory);
        if (!Directory.Exists(expanded))
        {
            _logger.LogDebug("Plugin directory '{Dir}' does not exist; no plugins loaded.", expanded);
            return;
        }

        foreach (var dll in Directory.GetFiles(expanded, "*.dll"))
        {
            try
            {
                LoadPluginAssembly(dll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin assembly '{Dll}'.", dll);
            }
        }

        _logger.LogInformation(
            "Plugin scan complete: {Charts} chart, {Widgets} widget, {Sources} data source, {Themes} theme plugin(s) loaded.",
            ChartPlugins.Count, WidgetPlugins.Count, DataSourcePlugins.Count, ThemePlugins.Count);
    }

    private void LoadPluginAssembly(string path)
    {
        var assembly = Assembly.LoadFrom(path);
        _loadedAssemblies.Add(assembly);

        foreach (var type in assembly.GetExportedTypes())
        {
            if (type.IsAbstract || type.IsInterface) continue;

            TryInstantiate<IChartPlugin>(type, ChartPlugins);
            TryInstantiate<IWidgetPlugin>(type, WidgetPlugins);
            TryInstantiate<IDataSourcePlugin>(type, DataSourcePlugins);
            TryInstantiate<IThemePlugin>(type, ThemePlugins);
        }
    }

    private void TryInstantiate<T>(Type candidate, List<T> target) where T : class
    {
        if (!typeof(T).IsAssignableFrom(candidate)) return;

        try
        {
            if (Activator.CreateInstance(candidate) is T instance)
            {
                target.Add(instance);
                _logger.LogDebug("Loaded plugin: {Type} implements {Interface}.", candidate.FullName, typeof(T).Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin type '{Type}' implements {Interface} but could not be instantiated (needs a public parameterless constructor).",
                candidate.FullName, typeof(T).Name);
        }
    }
}
