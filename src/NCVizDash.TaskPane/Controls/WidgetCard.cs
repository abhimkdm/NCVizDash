using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using NCVizDash.Models;
using NCVizDash.TaskPane.Geometry;
using NCVizDash.TaskPane.Services;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace NCVizDash.TaskPane.Controls;

/// <summary>
/// A single widget card. Chrome (title bar, border, resize grip, selection highlight,
/// elevation shadow) is drawn in <see cref="OnRender"/>; the chart body itself is a
/// real child <see cref="ChartHost"/> (WebView2) positioned below the title bar via
/// the standard WPF single-child visual/logical tree pattern.
/// </summary>
public sealed class WidgetCard : FrameworkElement
{
    // ── Static brushes / pens ────────────────────────────────────────────────

    private static readonly Brush CardBackground     = new SolidColorBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush CardBackgroundDark = new SolidColorBrush(Color.FromRgb(37, 37, 37));
    private static readonly Brush TitleBarBrush      = new SolidColorBrush(Color.FromRgb(245, 245, 245));
    private static readonly Brush TitleBarBrushDark  = new SolidColorBrush(Color.FromRgb(48, 48, 48));
    private static readonly Brush TitleTextBrush     = new SolidColorBrush(Color.FromRgb(80, 80, 80));
    private static readonly Brush TitleTextBrushDark = new SolidColorBrush(Color.FromRgb(200, 200, 200));

    private static readonly Pen SelectionBorderPen = new(new SolidColorBrush(Color.FromRgb(103, 58, 183)), 2.0);
    private static readonly Pen DefaultBorderPen    = new(new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), 1.0);
    private static readonly Brush ResizeHandleBrush = new SolidColorBrush(Color.FromArgb(160, 103, 58, 183));

    private static readonly Typeface TitleTypeface = new("Segoe UI");

    private const double CornerRadius = 6d;
    private const double TitleBarHeight = 32d;
    private const double ResizeHandleSize = 10d;
    private const double TitleFontSize = 11d;
    private const double Elevation = 3d;
    private const double DataSourceComboWidth = 108d;

    /// <summary>Per-widget data source picker — hidden until the UX is finalized.</summary>
    private static readonly bool ShowDataSourcePicker = false;

    // ── Fields ───────────────────────────────────────────────────────────────

    /// <summary>The widget data model this card represents.</summary>
    public DashboardWidget Widget { get; }

    private readonly ChartHost _chartHost;
    private readonly ComboBox _dataSourceCombo;
    private readonly Action<DashboardWidget, Guid> _onDataSourceChanged;
    private bool _isDark;
    private bool _isSyncingCombo;

    /// <summary>
    /// Whether this card should render its dark-theme chrome. Not wired to
    /// <see cref="Services.ThemeService"/> automatically — the host (e.g.
    /// <see cref="DashboardCanvas"/>) is expected to set this whenever the theme changes.
    /// </summary>
    public bool IsDark
    {
        get => _isDark;
        set
        {
            if (_isDark == value) return;
            _isDark = value;
            InvalidateVisual();
        }
    }

    /// <summary>Initialises a card for the given widget, creating its child <see cref="ChartHost"/>.</summary>
    public WidgetCard(
        DashboardWidget widget,
        ObservableCollection<DataSourceDescriptor> dataSources,
        Action<DashboardWidget, Guid> onDataSourceChanged,
        ILogger<ChartHost>? chartHostLogger = null)
    {
        Widget = widget;
        _onDataSourceChanged = onDataSourceChanged;
        Widget.PropertyChanged += OnWidgetPropertyChanged;
        Widget.Layout.PropertyChanged += OnLayoutPropertyChanged;

        _dataSourceCombo = new ComboBox
        {
            DisplayMemberPath = nameof(DataSourceDescriptor.Name),
            SelectedValuePath = nameof(DataSourceDescriptor.Id),
            ItemsSource = dataSources,
            FontSize = 9,
            Padding = new Thickness(4, 2, 4, 2),
            ToolTip = "Data source for this widget"
        };
        _dataSourceCombo.SelectionChanged += OnDataSourceComboSelectionChanged;
        SyncDataSourceComboSelection();
        if (!ShowDataSourcePicker)
            _dataSourceCombo.Visibility = Visibility.Collapsed;

        _chartHost = new ChartHost(chartHostLogger);
        if (ShowDataSourcePicker)
        {
            AddVisualChild(_dataSourceCombo);
            AddLogicalChild(_dataSourceCombo);
        }

        AddVisualChild(_chartHost);

        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
    }

    private void OnDataSourceComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingCombo) return;
        if (_dataSourceCombo.SelectedValue is not Guid id || id == Widget.DataSourceId) return;
        _onDataSourceChanged(Widget, id);
    }

    private void SyncDataSourceComboSelection()
    {
        _isSyncingCombo = true;
        try
        {
            _dataSourceCombo.SelectedValue = Widget.DataSourceId;
            if (_dataSourceCombo.SelectedItem is null && _dataSourceCombo.Items.Count > 0)
                _dataSourceCombo.SelectedIndex = 0;
        }
        finally
        {
            _isSyncingCombo = false;
        }
    }

    /// <summary>
    /// Title bar and resize grip live outside the WebView2 body — handle them here
    /// so chart HWND input does not block move/resize after a widget is dropped.
    /// </summary>
    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;
        if (IsDescendantOf(_dataSourceCombo, e.OriginalSource as DependencyObject)) return;

        var pos = e.GetPosition(this);
        var onTitleBar = pos.Y <= TitleBarHeight + 2;
        var onResizeGrip = pos.X >= ActualWidth - ResizeHandleSize - 6
                        && pos.Y >= ActualHeight - ResizeHandleSize - 6;

        if (!onTitleBar && !onResizeGrip) return;

        if (FindParent<DashboardCanvas>(this) is not DashboardCanvas canvas) return;

        canvas.BeginWidgetInteraction(Widget, e.GetPosition(canvas), resize: onResizeGrip, e);
    }

    private static bool IsDescendantOf(DependencyObject ancestor, DependencyObject? node)
    {
        for (var current = node; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current == ancestor)
                return true;
        }

        return false;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        for (var parent = child is null ? null : VisualTreeHelper.GetParent(child);
             parent is not null;
             parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is T match)
                return match;
        }

        return null;
    }

    /// <summary>
    /// Pushes a rendering payload (from <see cref="WidgetRenderCoordinator"/>) into
    /// this card's chart host. Safe to call before the WebView2 finishes initialising —
    /// the payload is queued internally.
    /// </summary>
    public Task RenderAsync(string payloadJson) => _chartHost.RenderAsync(payloadJson);

    /// <summary>Raised when the user clicks a data point in this widget's chart.</summary>
    public event EventHandler<ChartClickEventArgs>? ChartClicked
    {
        add => _chartHost.ChartClicked += value;
        remove => _chartHost.ChartClicked -= value;
    }

    /// <summary>Exports this widget's rendered content to a PDF file.</summary>
    public Task<bool> ExportToPdfAsync(string filePath) => _chartHost.ExportToPdfAsync(filePath);

    /// <summary>Exports this widget's rendered content to a PNG file.</summary>
    public Task<bool> ExportToPngAsync(string filePath) => _chartHost.ExportToPngAsync(filePath);

    /// <summary>Captures this widget's rendered content as in-memory PNG bytes (for PPTX/Excel embedding).</summary>
    public Task<byte[]?> CapturePngBytesAsync() => _chartHost.CapturePngBytesAsync();

    /// <summary>Releases the underlying WebView2 resources. Call when the card is removed from the canvas.</summary>
    public void Cleanup()
    {
        _dataSourceCombo.SelectionChanged -= OnDataSourceComboSelectionChanged;
        _chartHost.Cleanup();
    }

    // ── Visual tree plumbing ─────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override int VisualChildrenCount => ShowDataSourcePicker ? 2 : 1;

    /// <inheritdoc/>
    protected override Visual GetVisualChild(int index)
    {
        if (!ShowDataSourcePicker)
            return _chartHost;

        return index switch
        {
            0 => _dataSourceCombo,
            1 => _chartHost,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = GridGeometryHelper.ToPixels(Widget.Layout.ColumnSpan);
        var height = GridGeometryHelper.ToPixels(Widget.Layout.RowSpan);
        var childSize = new Size(width, height);

        if (ShowDataSourcePicker)
            _dataSourceCombo.Measure(new Size(DataSourceComboWidth, TitleBarHeight));
        _chartHost.Measure(childSize);
        return childSize;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (ShowDataSourcePicker)
        {
            var comboLeft = Math.Max(4, finalSize.Width - DataSourceComboWidth - 18);
            _dataSourceCombo.Arrange(new Rect(comboLeft, 5, DataSourceComboWidth, TitleBarHeight - 6));
        }

        // Leave the bottom-right resize grip outside the WebView2 hit region.
        var bodyRect = new Rect(
            4, TitleBarHeight + 2,
            Math.Max(0, finalSize.Width - 8),
            Math.Max(0, finalSize.Height - TitleBarHeight - ResizeHandleSize - 8));

        _chartHost.Arrange(bodyRect);
        return finalSize;
    }

    // ── Property change listeners ────────────────────────────────────────────

    private void OnWidgetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DashboardWidget.IsSelected) or nameof(DashboardWidget.Title))
            InvalidateVisual();

        if (e.PropertyName is nameof(DashboardWidget.DataSourceId))
            SyncDataSourceComboSelection();
    }

    private void OnLayoutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    // ── Chrome rendering (title bar, border, resize grip) ────────────────────

    /// <inheritdoc/>
    protected override void OnRender(DrawingContext dc)
    {
        var cardRect = new Rect(2, 2, Math.Max(0, ActualWidth - 4), Math.Max(0, ActualHeight - 4));
        var geometry = new RectangleGeometry(cardRect, CornerRadius, CornerRadius);

        // Drop shadow.
        dc.PushOpacity(0.12);
        dc.DrawRectangle(Brushes.Black, null,
            new Rect(cardRect.X + 1, cardRect.Y + Elevation, cardRect.Width, cardRect.Height));
        dc.Pop();

        // Card background.
        var bg = _isDark ? CardBackgroundDark : CardBackground;
        dc.DrawGeometry(bg, null, geometry);

        // Border (selection-aware).
        var borderPen = Widget.IsSelected ? SelectionBorderPen : DefaultBorderPen;
        dc.DrawGeometry(null, borderPen, geometry);

        // Title bar.
        var titleBarRect = new Rect(cardRect.X, cardRect.Y, cardRect.Width, TitleBarHeight);
        var titleBarBg = _isDark ? TitleBarBrushDark : TitleBarBrush;
        dc.DrawGeometry(titleBarBg, null, new RectangleGeometry(titleBarRect, CornerRadius, CornerRadius));
        dc.DrawRectangle(titleBarBg, null,
            new Rect(cardRect.X, cardRect.Y + CornerRadius, cardRect.Width, TitleBarHeight - CornerRadius));

        // Title text.
        var titleBrush = _isDark ? TitleTextBrushDark : TitleTextBrush;
        var titleWidth = ShowDataSourcePicker
            ? Math.Max(40, cardRect.Width - DataSourceComboWidth - 28)
            : cardRect.Width - 20;
        RenderText(dc, Widget.Title, new Point(cardRect.X + 10, cardRect.Y + 8), titleBrush, titleWidth);

        // Resize handle (bottom-right).
        var handleX = cardRect.Right - ResizeHandleSize - 2;
        var handleY = cardRect.Bottom - ResizeHandleSize - 2;
        var handleRect = new Rect(handleX, handleY, ResizeHandleSize, ResizeHandleSize);
        dc.DrawRectangle(ResizeHandleBrush, null, handleRect);
        dc.DrawLine(new Pen(Brushes.White, 1), new Point(handleX + 2, handleY + ResizeHandleSize - 2),
            new Point(handleX + ResizeHandleSize - 2, handleY + 2));
        dc.DrawLine(new Pen(Brushes.White, 1), new Point(handleX + 5, handleY + ResizeHandleSize - 2),
            new Point(handleX + ResizeHandleSize - 2, handleY + 5));
    }

    private static void RenderText(DrawingContext dc, string text, Point origin, Brush foreground, double maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var ft = new FormattedText(
            text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            TitleTypeface, TitleFontSize, foreground,
            VisualTreeHelper.GetDpi(System.Windows.Application.Current?.MainWindow ?? new System.Windows.Window()).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(0, maxWidth),
            Trimming = TextTrimming.CharacterEllipsis,
            MaxLineCount = 1
        };

        dc.DrawText(ft, origin);
    }
}
