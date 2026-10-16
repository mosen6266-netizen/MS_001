using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Mosen.GroupScheduler.Core.Shell;

namespace Mosen.GroupScheduler.Desktop;

public partial class MainWindow : Window
{
    private const string TrustedOrigin = "https://ms001.local/";
    private const string Protocol = "mosen.desktop.v1";
    private readonly WindowStateStore _windowStore = new();
    private bool _started;
    private bool _closing;
    private record BridgeRequest([property: JsonPropertyName("protocol")] string Protocol,
                                 [property: JsonPropertyName("requestId")] string RequestId,
                                 [property: JsonPropertyName("type")] string Type);

    public MainWindow()
    {
        InitializeComponent();
        ApplyWindowPlacement();
        Loaded += InitializeBrowser;
        Closing += (_, _) => SaveWindowPlacement();
        Closed += (_, _) =>
        {
            _closing = true;
            // WebView2 runs only while the window is open; never keep background sessions.
            if (Browser.CoreWebView2 is { } core)
                core.WebMessageReceived -= OnWebMessageReceived;
            Browser.Dispose();
        };
    }

    private void ApplyWindowPlacement()
    {
        var work = SystemParameters.WorkArea;
        var safe = WindowPlacementPolicy.Normalize(_windowStore.Load(),
            new DisplayArea(work.Left, work.Top, work.Width, work.Height));
        Left = safe.Left;
        Top = safe.Top;
        Width = safe.Width;
        Height = safe.Height;
        if (safe.Maximized) WindowState = WindowState.Maximized;
    }

    private void SaveWindowPlacement()
    {
        if (_closing) return;
        Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        var snapshot = new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState == WindowState.Maximized);
        try { _windowStore.Save(snapshot); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // A read-only local profile must not prevent safe shutdown.
        }
    }

    private async void InitializeBrowser(object sender, RoutedEventArgs e)
    {
        if (_started || _closing) return;
        _started = true;
        string folder = Path.Combine(AppContext.BaseDirectory, "ui");
        if (!File.Exists(Path.Combine(folder, "index.html")))
        {
            Diagnostics.Text = "未找到离线界面资源，请检查安装文件是否完整。";
            return;
        }
        try
        {
            await Browser.EnsureCoreWebView2Async();
            if (_closing) return;
            CoreWebView2 core = Browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.NewWindowRequested += (_, evt) => evt.Handled = true;
            core.NavigationStarting += (_, evt) =>
            {
                if (!Uri.TryCreate(evt.Uri, UriKind.Absolute, out Uri? uri) ||
                    uri.Scheme != Uri.UriSchemeHttps ||
                    !string.Equals(uri.Host, "ms001.local", StringComparison.OrdinalIgnoreCase) ||
                    uri.Port != 443)
                    evt.Cancel = true;
            };
            core.NavigationCompleted += (_, evt) =>
            {
                if (_closing) return;
                if (!evt.IsSuccess)
                {
                    Diagnostics.Text = "界面加载失败，请尝试重新启动或检查本地界面文件。";
                    Browser.Visibility = Visibility.Hidden;
                    StatusPanel.Visibility = Visibility.Visible;
                    return;
                }
                Browser.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Collapsed;
            };
            core.SetVirtualHostNameToFolderMapping("ms001.local", folder, CoreWebView2HostResourceAccessKind.DenyCors);
            core.WebMessageReceived += OnWebMessageReceived;
            core.Navigate(TrustedOrigin + "index.html"); // exactly once; menu clicks never navigate CoreWebView2
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or FileNotFoundException)
        {
            if (!_closing) Diagnostics.Text = "界面初始化失败：" + ex.GetType().Name + "。请检查 WebView2 Runtime。";
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs evt)
    {
        if (_closing || !string.Equals(evt.Source, TrustedOrigin + "index.html", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            var request = JsonSerializer.Deserialize<BridgeRequest>(evt.TryGetWebMessageAsString());
            if (request is null || request.Protocol != Protocol || string.IsNullOrEmpty(request.RequestId) ||
                request.RequestId.Length > 64) return;
            string type = request.Type switch { "system.ping" => "system.pong", "app.info" => "app.info.response", _ => "denied" };
            bool allowed = type != "denied";
            Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {
                protocol = Protocol, requestId = request.RequestId, ok = allowed, type,
                payload = new { name = "MOSEN Group Scheduler", mode = "offline-foundation" }
            }));
        }
        catch (Exception) { /* Invalid JSON is ignored; never dispatch an unrecognized IPC action. */ }
    }
}
