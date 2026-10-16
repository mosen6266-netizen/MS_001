using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Mosen.GroupScheduler.Desktop;

public partial class MainWindow : Window
{
    private const string TrustedOrigin = "https://ms001.local/";
    private const string Protocol = "mosen.desktop.v1";
    private record BridgeRequest([property: JsonPropertyName("protocol")] string Protocol,
                                 [property: JsonPropertyName("requestId")] string RequestId,
                                 [property: JsonPropertyName("type")] string Type);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += InitializeBrowser;
    }

    private async void InitializeBrowser(object sender, RoutedEventArgs e)
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "ui");
        if (!File.Exists(Path.Combine(folder, "index.html")))
        {
            Diagnostics.Text = "未找到离线界面文件 ui/index.html；请检查安装包完整性。";
            Browser.Visibility = Visibility.Collapsed;
            return;
        }
        try
        {
            await Browser.EnsureCoreWebView2Async();
            CoreWebView2 core = Browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.NewWindowRequested += (_, evt) => evt.Handled = true;
            core.NavigationStarting += (_, evt) =>
            {
                if (!evt.Uri.StartsWith(TrustedOrigin, StringComparison.OrdinalIgnoreCase)) evt.Cancel = true;
            };
            core.SetVirtualHostNameToFolderMapping("ms001.local", folder, CoreWebView2HostResourceAccessKind.DenyCors);
            core.WebMessageReceived += OnWebMessageReceived;
            Browser.CoreWebView2.Navigate(TrustedOrigin + "index.html");
            Diagnostics.Text = "本地离线界面已加载（尚未启用 Telegram 登录或发送）。";
        }
        catch (Exception ex)
        {
            Diagnostics.Text = "本地 WebView2 初始化失败：" + ex.GetType().Name + "；请检查 WebView2 Runtime。";
            Browser.Visibility = Visibility.Collapsed;
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs evt)
    {
        if (!evt.Source.StartsWith(TrustedOrigin, StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            var request = JsonSerializer.Deserialize<BridgeRequest>(evt.TryGetWebMessageAsString());
            if (request is null || request.Protocol != Protocol || request.RequestId.Length is < 1 or > 64) return;
            string type = request.Type switch { "system.ping" => "system.pong", "app.info" => "app.info.response", _ => "denied" };
            bool allowed = type != "denied";
            string json = JsonSerializer.Serialize(new {
                protocol = Protocol, requestId = request.RequestId, ok = allowed, type,
                payload = allowed ? new { name = "MOSEN Group Scheduler", mode = "offline-foundation" } : new { name = "unsupported request", mode = "blocked" }
            });
            Browser.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch (Exception) { /* Invalid input: silently refuse; never execute arbitrary IPC. */ }
    }
}
