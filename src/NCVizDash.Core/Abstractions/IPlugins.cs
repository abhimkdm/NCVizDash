using NCVizDash.Models;

namespace NCVizDash.Core.Abstractions.Plugins;

/// <summary>
/// A custom chart type contributed by a plugin. Returns the same ECharts option
/// JSON shape <see cref="IChartEngine"/>'s built-in builders produce, so a custom
/// chart plugs into the exact same WebView2/`chart-host.html` rendering pipeline
/// with no changes to the host application.
/// </summary>
public interface IChartPlugin
{
    /// <summary>Unique identifier for this custom visual type (shown in the Visual Library).</summary>
    string VisualTypeId { get; }

    /// <summary>Display name shown in the Visual Library tile.</summary>
    string DisplayName { get; }

    /// <summary>Builds the ECharts option (or `{"kind":"html",...}` envelope) for this custom chart.</summary>
    string BuildOption(DashboardWidget widget, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, string theme);
}

/// <summary>A custom canvas widget contributed by a plugin (non-chart — e.g. an embedded web page, a custom control).</summary>
public interface IWidgetPlugin
{
    /// <summary>Unique identifier for this custom widget type.</summary>
    string WidgetTypeId { get; }

    /// <summary>Display name shown wherever custom widget types are offered to the user.</summary>
    string DisplayName { get; }
}

/// <summary>A custom external data source contributed by a plugin, following the same shape as the built-in connectors.</summary>
public interface IDataSourcePlugin
{
    /// <summary>Unique identifier for this custom data source type.</summary>
    string SourceTypeId { get; }

    /// <summary>Display name shown wherever custom data source types are offered to the user.</summary>
    string DisplayName { get; }

    /// <summary>Connects to the source described by <paramref name="connectionInfo"/> and returns its discoverable data source(s).</summary>
    Task<IReadOnlyList<DataSourceDescriptor>> DiscoverAsync(string connectionInfo, CancellationToken ct = default);

    /// <summary>Reads all rows for a previously-discovered data source.</summary>
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(DataSourceDescriptor descriptor, string connectionInfo, CancellationToken ct = default);
}

/// <summary>A custom colour theme contributed by a plugin.</summary>
public interface IThemePlugin
{
    /// <summary>Unique identifier for this custom theme.</summary>
    string ThemeId { get; }

    /// <summary>Display name shown in the theme picker.</summary>
    string DisplayName { get; }

    /// <summary>The 10-colour chart palette for this theme.</summary>
    IReadOnlyList<string> Palette { get; }

    /// <summary>The theme's primary accent colour, as a hex string.</summary>
    string PrimaryColor { get; }
}
