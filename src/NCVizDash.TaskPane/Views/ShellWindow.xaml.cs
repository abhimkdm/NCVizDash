using System.ComponentModel;
using System.Windows;
using MaterialDesignThemes.Wpf;
using NCVizDash.TaskPane.Services;
using NCVizDash.TaskPane.ViewModels;

namespace NCVizDash.TaskPane.Views;

/// <summary>
/// Code-behind for the NC VizDash three-panel shell window.
/// Most logic lives in <see cref="ShellViewModel"/>; the one exception is theme
/// application, which mutates the local <c>BundledTheme</c> resource directly
/// (see <see cref="ThemeService"/> for why this isn't done via PaletteHelper).
/// </summary>
public sealed partial class ShellWindow : Window
{
    private readonly ThemeService _themeService;

    /// <summary>Initialises the shell, binds the DataContext, and wires theme application.</summary>
    public ShellWindow(ShellViewModel viewModel, ThemeService themeService)
    {
        InitializeComponent();

        _themeService = themeService;
        DataContext = viewModel;

        _themeService.ThemeChanged += (_, theme) => ApplyThemeToResources(theme);

        // Apply whatever theme the ViewModel started with (set during its own construction).
        ApplyThemeToResources(viewModel.ActiveTheme);
    }

    private void ApplyThemeToResources(string themeName)
    {
        BundledThemeDictionary.BaseTheme = themeName.Equals("Dark", StringComparison.OrdinalIgnoreCase)
            ? BaseTheme.Dark
            : BaseTheme.Light;
    }
}
