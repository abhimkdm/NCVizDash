using System.Windows.Controls;
using NCVizDash.TaskPane.Services;
using NCVizDash.TaskPane.Theming;
using NCVizDash.TaskPane.ViewModels;

namespace NCVizDash.TaskPane.Views;

/// <summary>
/// Three-panel shell hosted inside the VSTO task pane via <c>ElementHost</c>.
/// </summary>
public sealed partial class ShellView : UserControl
{
    private readonly ThemeService _themeService;

    /// <summary>Initialises the shell chrome. Call <see cref="BindViewModel"/> after hosting in ElementHost.</summary>
    public ShellView(ThemeService themeService)
    {
        InitializeComponent();

        _themeService = themeService;
        _themeService.ThemeChanged += (_, theme) => WpfResourceBootstrap.ApplyBaseTheme(theme);
    }

    /// <summary>Binds the shell to its view model once the control is hosted.</summary>
    /// <param name="viewModel">Root task pane view model.</param>
    public void BindViewModel(ShellViewModel viewModel)
    {
        DataContext = viewModel;
        WpfResourceBootstrap.ApplyBaseTheme(viewModel.ActiveTheme);
    }
}
