using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private ShellViewModel? _viewModel;

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
        if (_viewModel is not null)
        {
            _viewModel.PopOutRequested -= OnPopOutRequested;
            _viewModel.PresentRequested -= OnPresentRequested;
        }

        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.PopOutRequested += OnPopOutRequested;
        _viewModel.PresentRequested += OnPresentRequested;

        WpfResourceBootstrap.ApplyBaseTheme(viewModel.ActiveTheme);
    }

    private void OnPopOutRequested(object? sender, EventArgs e) =>
        FindDescendant<CanvasPanelView>(this)?.RequestPopOut();

    private void OnPresentRequested(object? sender, EventArgs e) =>
        FindDescendant<CanvasPanelView>(this)?.RequestPresent();

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;

            if (FindDescendant<T>(child) is { } found)
                return found;
        }

        return null;
    }
}
