using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace NCVizDash.TaskPane.Controls;

/// <summary>
/// Hosts a single WebView2 instance that renders one widget's chart (or KPI/Table HTML).
/// <para>
/// Deployment: <c>chart-host.html</c> and <c>echarts.min.js</c> are copied to the add-in's
/// output directory under <c>Assets\ChartHost\</c> at build time (see the .csproj
/// <c>Content</c> items). ECharts is loaded as a local file, not from a CDN, so
/// dashboards render fully offline per the product vision.
/// </para>
/// <para>
/// <b>ECharts license note:</b> Apache ECharts is Apache-2.0 licensed. The minified
/// distribution (<c>echarts.min.js</c>) must be downloaded once from
/// https://echarts.apache.org/en/download.html (or via <c>npm install echarts</c> and
/// copying <c>dist/echarts.min.js</c>) and placed in
/// <c>NCVizDash.TaskPane/Assets/echarts.min.js</c> before building — it is not
/// fetched automatically by this project to avoid committing a large generated binary
/// to source control, and downloading it from this environment isn't possible (no
/// network access to npm/CDN registries here).
/// </para>
/// </summary>
public sealed class ChartHost : System.Windows.Controls.UserControl
{
    private readonly ILogger<ChartHost>? _logger;
    private WebView2? _webView;
    private bool _isHostReady;
    private string? _pendingPayload;

    /// <summary>Raised when the user clicks a data point inside the chart (Phase 8 cross-filter source).</summary>
    public event EventHandler<ChartClickEventArgs>? ChartClicked;

    /// <summary>Initialises the host. Call <see cref="InitializeAsync"/> before the first <see cref="RenderAsync"/>.</summary>
    public ChartHost(ILogger<ChartHost>? logger = null)
    {
        _logger = logger;
        Loaded += async (_, _) => await InitializeAsync();
    }

    /// <summary>
    /// Creates the underlying WebView2 control and navigates it to the local chart-host
    /// harness. Safe to call multiple times — subsequent calls are no-ops once initialised.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_webView is not null) return;

        _webView = new WebView2
        {
            DefaultBackgroundColor = System.Drawing.Color.Transparent
        };
        Content = _webView;

        try
        {
            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "NCVizDash", "WebView2"));

            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            var hostHtmlPath = ResolveAssetPath("chart-host.html");
            _webView.CoreWebView2.Navigate(new Uri(hostHtmlPath).AbsoluteUri);

            _logger?.LogDebug("ChartHost WebView2 initialised, navigating to '{Path}'.", hostHtmlPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialise WebView2 for ChartHost.");
        }
    }

    /// <summary>Prints this widget's current rendered content to a PDF file, using WebView2's native PDF export.</summary>
    public async Task<bool> ExportToPdfAsync(string filePath)
    {
        if (_webView?.CoreWebView2 is null) return false;

        try
        {
            return await _webView.CoreWebView2.PrintToPdfAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to export widget to PDF at '{Path}'.", filePath);
            return false;
        }
    }

    /// <summary>Captures this widget's current rendered content as a PNG image.</summary>
    public async Task<bool> ExportToPngAsync(string filePath)
    {
        if (_webView?.CoreWebView2 is null) return false;

        try
        {
            using var stream = File.Create(filePath);
            await _webView.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, stream);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to export widget to PNG at '{Path}'.", filePath);
            return false;
        }
    }

    /// <summary>Captures this widget's current rendered content as an in-memory PNG byte array (for embedding into PPTX/Excel exports).</summary>
    public async Task<byte[]?> CapturePngBytesAsync()
    {
        if (_webView?.CoreWebView2 is null) return null;

        try
        {
            using var stream = new MemoryStream();
            await _webView.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, stream);
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to capture PNG bytes.");
            return null;
        }
    }

    /// <summary>
    /// Sends a rendering payload (as produced by <c>IChartEngine.BuildChartOption</c>) to
    /// the WebView2 host. If the host page hasn't signalled readiness yet, the payload is
    /// queued and flushed automatically once it does.
    /// </summary>
    public async Task RenderAsync(string payloadJson)
    {
        if (_webView is null)
            await InitializeAsync();

        if (_webView?.CoreWebView2 is null)
        {
            _pendingPayload = payloadJson;
            return;
        }

        if (!_isHostReady)
        {
            _pendingPayload = payloadJson;
            return;
        }

        await InvokeRenderAsync(payloadJson);
    }

    // ── Web message handling ─────────────────────────────────────────────────

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var type = doc.RootElement.GetProperty("type").GetString();

            switch (type)
            {
                case "host-ready":
                    _isHostReady = true;
                    _logger?.LogDebug("ChartHost harness signalled ready.");
                    if (_pendingPayload is not null)
                    {
                        var payload = _pendingPayload;
                        _pendingPayload = null;
                        await InvokeRenderAsync(payload);
                    }
                    break;

                case "chart-click":
                    var root = doc.RootElement;
                    var args = new ChartClickEventArgs(
                        SeriesName: root.TryGetProperty("seriesName", out var sn) ? sn.GetString() : null,
                        CategoryName: root.TryGetProperty("name", out var n) ? n.GetString() : null,
                        DataIndex: root.TryGetProperty("dataIndex", out var di) ? di.GetInt32() : -1);

                    ChartClicked?.Invoke(this, args);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse WebView2 message.");
        }
    }

    private async Task InvokeRenderAsync(string payloadJson)
    {
        if (_webView?.CoreWebView2 is null) return;

        try
        {
            // Payload is already valid JSON; encode it as a JS string literal argument.
            var escaped = System.Text.Json.JsonSerializer.Serialize(payloadJson);
            await _webView.CoreWebView2.ExecuteScriptAsync($"window.ncvizdashRender({escaped});");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute render script in ChartHost.");
        }
    }

    // ── Asset resolution ──────────────────────────────────────────────────────

    private static string ResolveAssetPath(string fileName)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var assetPath = Path.Combine(baseDir, "Assets", "ChartHost", fileName);

        if (!File.Exists(assetPath))
            throw new FileNotFoundException(
                $"Chart host asset '{fileName}' not found at '{assetPath}'. " +
                "Ensure chart-host.html and echarts.min.js are set to 'Copy to Output Directory' " +
                "in NCVizDash.TaskPane.csproj (see the ItemGroup with Include=\"Assets\\**\").",
                assetPath);

        return assetPath;
    }

    /// <summary>Disposes the underlying WebView2 instance. Call when the widget card is removed.</summary>
    public void Cleanup()
    {
        if (_webView is null) return;

        _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        _webView.Dispose();
        _webView = null;
        _isHostReady = false;
    }
}

/// <summary>Event args for a chart data-point click, forwarded from the WebView2 host.</summary>
public sealed record ChartClickEventArgs(string? SeriesName, string? CategoryName, int DataIndex);
