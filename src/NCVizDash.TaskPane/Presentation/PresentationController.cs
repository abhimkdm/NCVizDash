using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NCVizDash.Models;
using System.Collections.ObjectModel;

namespace NCVizDash.TaskPane.Presentation;

/// <summary>
/// Drives Dashboard Story Mode (v2.0 Feature 3): a full-screen presentation over
/// a sequence of "pages". Each page is a <see cref="Bookmark"/> — the dashboard's
/// filter state at a point in time — so "Next"/"Previous" means "apply the next
/// bookmark's filters", not "switch to a different dashboard". This reuses Phase
/// 12's `Bookmark` model directly rather than inventing a parallel "slide" concept:
/// a presentation is just an ordered walk through a dashboard's saved views.
/// </summary>
public sealed partial class PresentationController : ObservableObject
{
    private readonly ILogger<PresentationController> _logger;
    private readonly TaskPane.Services.BookmarkManager _bookmarkManager;
    private System.Threading.Timer? _autoPlayTimer;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private int _currentPageIndex;

    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>Seconds between auto-advance steps.</summary>
    [ObservableProperty]
    private int _autoPlayIntervalSeconds = 8;

    /// <summary>The ordered bookmark sequence for the active presentation.</summary>
    public ObservableCollection<Bookmark> Pages { get; } = [];

    public Bookmark? CurrentPage => CurrentPageIndex >= 0 && CurrentPageIndex < Pages.Count ? Pages[CurrentPageIndex] : null;

    /// <summary>Raised when the current page changes — the canvas/global-filter-bound UI subscribes to this to apply the new bookmark.</summary>
    public event EventHandler<Bookmark>? PageChanged;

    public PresentationController(ILogger<PresentationController> logger, TaskPane.Services.BookmarkManager bookmarkManager)
    {
        _logger = logger;
        _bookmarkManager = bookmarkManager;
    }

    /// <summary>Starts a presentation over the given dashboard's saved bookmarks. Hides editing controls (host UI reacts to <see cref="IsActive"/>).</summary>
    [RelayCommand]
    public void Start(Guid dashboardId)
    {
        Pages.Clear();
        foreach (var bookmark in _bookmarkManager.BookmarksOf(dashboardId))
            Pages.Add(bookmark);

        if (Pages.Count == 0)
        {
            _logger.LogWarning("Presentation started with no bookmarks on dashboard {DashboardId} — nothing to present.", dashboardId);
        }

        CurrentPageIndex = 0;
        IsActive = true;
        _logger.LogInformation("Presentation started: {Count} page(s).", Pages.Count);

        if (Pages.Count > 0)
            PageChanged?.Invoke(this, Pages[0]);
    }

    /// <summary>Ends the presentation, stops auto-play, and restores the editing UI.</summary>
    [RelayCommand]
    public void Stop()
    {
        StopAutoPlay();
        IsActive = false;
        _logger.LogInformation("Presentation stopped.");
    }

    /// <summary>Advances to the next page, wrapping to the first if already at the last (so auto-play loops).</summary>
    [RelayCommand]
    public void Next()
    {
        if (Pages.Count == 0) return;
        CurrentPageIndex = (CurrentPageIndex + 1) % Pages.Count;
        PageChanged?.Invoke(this, Pages[CurrentPageIndex]);
    }

    /// <summary>Goes back to the previous page, wrapping to the last if already at the first.</summary>
    [RelayCommand]
    public void Previous()
    {
        if (Pages.Count == 0) return;
        CurrentPageIndex = (CurrentPageIndex - 1 + Pages.Count) % Pages.Count;
        PageChanged?.Invoke(this, Pages[CurrentPageIndex]);
    }

    /// <summary>Starts auto-advancing through pages every <see cref="AutoPlayIntervalSeconds"/>.</summary>
    [RelayCommand]
    public void PlayAuto()
    {
        if (Pages.Count <= 1) return; // nothing to auto-play through

        IsPlaying = true;
        var interval = TimeSpan.FromSeconds(Math.Max(1, AutoPlayIntervalSeconds));
        _autoPlayTimer = new System.Threading.Timer(_ =>
        {
            // Timer callbacks run on a thread-pool thread; ObservableProperty
            // changes bound to WPF UI must be marshalled back to the UI thread.
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null) Next();
            else dispatcher.Invoke(Next);
        }, null, interval, interval);
        _logger.LogInformation("Auto-play started ({Interval}s interval).", AutoPlayIntervalSeconds);
    }

    /// <summary>Stops auto-advancing.</summary>
    [RelayCommand]
    public void StopAutoPlay()
    {
        _autoPlayTimer?.Dispose();
        _autoPlayTimer = null;
        IsPlaying = false;
    }
}
