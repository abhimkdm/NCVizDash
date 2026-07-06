using System.Windows;
using MaterialDesignThemes.Wpf;

namespace NCVizDash.TaskPane.Theming;

/// <summary>
/// Bootstraps WPF <see cref="Application.Current"/> resources for the VSTO host (no App.xaml).
/// </summary>
public static class WpfResourceBootstrap
{
    private static bool _resourcesLoaded;

    /// <summary>Ensures a WPF application object and shared task pane resource dictionaries exist.</summary>
    public static void EnsureApplicationResources()
    {
        if (Application.Current is null)
        {
            _ = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
        }

        if (_resourcesLoaded || Application.Current is null)
            return;

        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/NCVizDash.TaskPane;component/Themes/TaskPaneResources.xaml",
                UriKind.Absolute)
        });

        _resourcesLoaded = true;
    }

    /// <summary>Returns the shared <see cref="BundledTheme"/> instance, if loaded.</summary>
    public static BundledTheme? GetBundledTheme()
    {
        if (Application.Current is null)
            return null;

        return FindBundledTheme(Application.Current.Resources);
    }

    private static BundledTheme? FindBundledTheme(ResourceDictionary dictionary)
    {
        foreach (var value in dictionary.Values)
        {
            if (value is BundledTheme bundledTheme)
                return bundledTheme;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            var found = FindBundledTheme(merged);
            if (found is not null)
                return found;
        }

        return null;
    }

    /// <summary>Applies the light/dark base theme to the shared Material Design palette.</summary>
    public static void ApplyBaseTheme(string themeName)
    {
        var bundledTheme = GetBundledTheme();
        if (bundledTheme is null)
            return;

        bundledTheme.BaseTheme = themeName.Equals("Dark", StringComparison.OrdinalIgnoreCase)
            ? BaseTheme.Dark
            : BaseTheme.Light;
    }
}
