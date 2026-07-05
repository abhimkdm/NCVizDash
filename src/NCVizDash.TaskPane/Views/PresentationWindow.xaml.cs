using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using NCVizDash.TaskPane.Controls;
using NCVizDash.TaskPane.Presentation;

namespace NCVizDash.TaskPane.Views;

/// <summary>
/// Full-screen Story Mode window. Re-parents the existing <see cref="DashboardCanvas"/>
/// (rather than creating a second one) so the presentation always shows live,
/// current widget data — no separate "presentation copy" to keep in sync.
/// </summary>
public sealed partial class PresentationWindow : Window
{
    private readonly PresentationController _controller;
    private readonly Core.Abstractions.IGlobalFilterManager _globalFilterManager;
    private readonly FrameworkElement _canvasHost;
    private readonly Guid _dashboardId;

    public PresentationWindow(
        PresentationController controller,
        Core.Abstractions.IGlobalFilterManager globalFilterManager,
        FrameworkElement canvasHost,
        Guid dashboardId)
    {
        InitializeComponent();

        _controller = controller;
        _globalFilterManager = globalFilterManager;
        _canvasHost = canvasHost;
        _dashboardId = dashboardId;

        CanvasHostBorder.Child = _canvasHost;
        _canvasHost.IsHitTestVisible = false; // Story Mode is view-only — no accidental edits

        _controller.PageChanged += OnPageChanged;
        Loaded += (_, _) => _controller.Start(_dashboardId);
        Closed += (_, _) =>
        {
            _controller.PageChanged -= OnPageChanged;
            _controller.Stop();
            _canvasHost.IsHitTestVisible = true;
        };

        UpdatePageIndicator();
    }

    private void OnPageChanged(object? sender, Models.Bookmark bookmark)
    {
        // Smooth transition: fade out, apply the new bookmark's filters, fade in.
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        fadeOut.Completed += (_, _) =>
        {
            _globalFilterManager.ClearAll();
            foreach (var filter in bookmark.GlobalFilters)
                _globalFilterManager.AddOrUpdateFilter(filter);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
            CanvasHostBorder.BeginAnimation(OpacityProperty, fadeIn);
        };
        CanvasHostBorder.BeginAnimation(OpacityProperty, fadeOut);

        UpdatePageIndicator();
    }

    private void UpdatePageIndicator()
    {
        PageIndicator.Text = _controller.Pages.Count == 0
            ? "No bookmarks"
            : $"{_controller.CurrentPageIndex + 1} / {_controller.Pages.Count}";
    }

    private void PreviousButton_Click(object sender, RoutedEventArgs e) => _controller.PreviousCommand.Execute(null);
    private void NextButton_Click(object sender, RoutedEventArgs e) => _controller.NextCommand.Execute(null);
    private void ExitButton_Click(object sender, RoutedEventArgs e) => Close();

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_controller.IsPlaying)
        {
            _controller.StopAutoPlayCommand.Execute(null);
            PlayPauseIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Play;
        }
        else
        {
            _controller.PlayAutoCommand.Execute(null);
            PlayPauseIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Pause;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right: _controller.Next(); break;
            case Key.Left: _controller.Previous(); break;
            case Key.Space: PlayPauseButton_Click(this, new RoutedEventArgs()); break;
            case Key.Escape: Close(); break;
        }
    }
}
