using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using NCVizDash.Core.Abstractions.Plugins;

namespace NCVizDash.TaskPane.Plugins;

/// <summary>
/// Scans <see cref="Models.AppSettings.PluginDirectory"/> for `.dll` files and, for
/// each one, discovers types implementing any of the four plugin interfaces
/// (<see cref="IChartPlugin"/>, <see cref="IWidgetPlugin"/>, <see cref="IDataSourcePlugin"/>,
/// <see cref="IThemePlugin"/>) via reflection. Each plugin DLL is loaded into its
/// own collectible <see cref="AssemblyLoadContext"/> so a broken/malicious plugin
/// can't corrupt the host app's own loaded types, and so plugins could in principle
/// be unloaded later without restarting Excel (not wired up yet, but the isolation
/// is in place for it).
/// </summary>
public sealed class PluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly List<AssemblyLoadContext> _loadedContexts = [];

    public List<IChartPlugin> ChartPlugins { get; } = [];
    public List<IWidgetPlugin> WidgetPlugins { get; } = [];
    public List<IDataSourcePlugin> DataSourcePlugins { get; } = [];
    public List<IThemePlugin> ThemePlugins { get; } = [];

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
        var context = new AssemblyLoadContext($"Plugin_{Path.GetFileNameWithoutExtension(path)}", isCollectible: true);
        _loadedContexts.Add(context);

        var assembly = context.LoadFromAssemblyPath(path);

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
