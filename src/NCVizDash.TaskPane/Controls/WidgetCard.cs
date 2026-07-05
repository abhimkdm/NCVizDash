using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using NCVizDash.Models;
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
    private const double TitleBarHeight = 28d;
    private const double ResizeHandleSize = 10d;
    private const double TitleFontSize = 11d;
    private const double Elevation = 3d;

    // ── Fields ───────────────────────────────────────────────────────────────

    /// <summary>The widget data model this card represents.</summary>
    public DashboardWidget Widget { get; }

    private readonly ChartHost _chartHost;
    private bool _isDark;

    /// <summary>Initialises a card for the given widget, creating its child <see cref="ChartHost"/>.</summary>
    public WidgetCard(DashboardWidget widget, ILogger<ChartHost>? chartHostLogger = null)
    {
        Widget = widget;
        Widget.PropertyChanged += OnWidgetPropertyChanged;
        Widget.Layout.PropertyChanged += OnLayoutPropertyChanged;

        _chartHost = new ChartHost(chartHostLogger);
        AddVisualChild(_chartHost);
        AddLogicalChild(_chartHost);
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
    public void Cleanup() => _chartHost.Cleanup();

    // ── Visual tree plumbing (single child) ──────────────────────────────────

    /// <inheritdoc/>
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc/>
    protected override Visual GetVisualChild(int index) => _chartHost;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _chartHost.Measure(availableSize);
        return availableSize;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // Body area: below the title bar, inset by the 2px card margin used in OnRender.
        var bodyRect = new Rect(
            4, TitleBarHeight + 2,
            Math.Max(0, finalSize.Width - 8),
            Math.Max(0, finalSize.Height - TitleBarHeight - 6));

        _chartHost.Arrange(bodyRect);
        return finalSize;
    }

    // ── Property change listeners ────────────────────────────────────────────

    private void OnWidgetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DashboardWidget.IsSelected) or nameof(DashboardWidget.Title))
            InvalidateVisual();
    }

    private void OnLayoutPropertyChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

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
        RenderText(dc, Widget.Title, new Point(cardRect.X + 10, cardRect.Y + 7), titleBrush, cardRect.Width - 20);

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
