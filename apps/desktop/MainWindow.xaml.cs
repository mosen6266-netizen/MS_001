using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Mosen.GroupScheduler.Core.Shell;
using Mosen.GroupScheduler.Core.Security;
using Mosen.GroupScheduler.Core.Telemetry;
using Mosen.GroupScheduler.ResourceBudget;
using Mosen.GroupScheduler.Installation;

namespace Mosen.GroupScheduler.Desktop;

public partial class MainWindow : Window
{
    private const string TrustedOrigin = "https://ms001.local/";
    private const string Protocol = "mosen.desktop.v1";
    private readonly WindowStateStore _windowStore = new();
    private readonly SafeDiagnostics _safeTelemetry = SafeDiagnostics.ForCurrentUser();
    private string _appHealth = "initializing";
    private bool _started;
    private bool _closing;
    private record BridgeRequest([property: JsonPropertyName("protocol")] string Protocol,
                                 [property: JsonPropertyName("requestId")] string RequestId,
                                 [property: JsonPropertyName("type")] string Type);

    public MainWindow()
    {
        InitializeComponent();
        _safeTelemetry.Record(DiagnosticEvent.Started);
        ApplyWindowPlacement();
        InitializeAlertNotifications();
        InitializeResourceHealth();
        Loaded += InitializeBrowser;
        Closing += (_, _) => SaveWindowPlacement();
        Closed += (_, _) =>
        {
            _closing = true;
            _appHealth = "closing";
            _safeTelemetry.Record(DiagnosticEvent.Closed);
            // WebView2 runs only while the window is open; never keep background sessions.
            _ = LiveStopAsync(); // close official TDLib clients when the window exits
            _webSubscriptions.Dispose();
            StopResourceHealth();
            StopAlertNotifications();
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
            _appHealth = "degraded";
            _safeTelemetry.Record(DiagnosticEvent.RecoverableError, FailureCategory.FileSystem);
            Diagnostics.Text = "未找到离线界面资源，请检查安装文件是否完整。";
            return;
        }
        try
        {
            var dataRoot=UserDataPaths.ForCurrentUser();
            UserDataPaths.AssertNoInstallDirectoryOverlap(AppContext.BaseDirectory,dataRoot);
            var webView2Profile=UserDataPaths.WebView2(dataRoot);
            Directory.CreateDirectory(webView2Profile);
            var environment=await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder:null,userDataFolder:webView2Profile);
            await Browser.EnsureCoreWebView2Async(environment);
            if (_closing) return;
            CoreWebView2 core = Browser.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.NewWindowRequested += OnCoreNewWindowRequested;
            _webSubscriptions.Track(()=>core.NewWindowRequested -= OnCoreNewWindowRequested);
            core.NavigationStarting += OnCoreNavigationStarting;
            _webSubscriptions.Track(()=>core.NavigationStarting -= OnCoreNavigationStarting);
            core.NavigationCompleted += OnCoreNavigationCompleted;
            _webSubscriptions.Track(()=>core.NavigationCompleted -= OnCoreNavigationCompleted);
            core.ProcessFailed += OnCoreProcessFailed;
            _webSubscriptions.Track(()=>core.ProcessFailed -= OnCoreProcessFailed);
            core.SetVirtualHostNameToFolderMapping("ms001.local", folder, CoreWebView2HostResourceAccessKind.DenyCors);
            core.WebMessageReceived += OnWebMessageReceived;
            _webSubscriptions.Track(()=>core.WebMessageReceived -= OnWebMessageReceived);
            core.Navigate(TrustedOrigin + "index.html"); // exactly once; menu clicks never navigate CoreWebView2
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or FileNotFoundException)
        {
            if (!_closing) {
                _appHealth = "degraded";
                _safeTelemetry.Record(DiagnosticEvent.RecoverableError,FailureClassifier.Classify(ex));
                Diagnostics.Text = "界面初始化失败，请检查 WebView2 Runtime 与程序安装文件。";
            }
        }
    }

    private void OnCoreNewWindowRequested(object? sender,CoreWebView2NewWindowRequestedEventArgs evt)
        =>evt.Handled=true;
    private void OnCoreNavigationStarting(object? sender,CoreWebView2NavigationStartingEventArgs evt)
    {
        if(!Uri.TryCreate(evt.Uri,UriKind.Absolute,out var uri)||
            uri.Scheme!=Uri.UriSchemeHttps||
            !string.Equals(uri.Host,"ms001.local",StringComparison.OrdinalIgnoreCase)||
            uri.Port!=443)evt.Cancel=true;
    }
    private void OnCoreNavigationCompleted(object? sender,CoreWebView2NavigationCompletedEventArgs evt)
    {
        if(_closing)return;
        if(!evt.IsSuccess)
        {
            _appHealth="degraded";
            _safeTelemetry.Record(DiagnosticEvent.RecoverableError,FailureCategory.Protocol);
            Diagnostics.Text="界面加载失败，请尝试重新启动或检查本地界面文件。";
            Browser.Visibility=Visibility.Hidden;
            StatusPanel.Visibility=Visibility.Visible;
            return;
        }
        Browser.Visibility=Visibility.Visible;
        StatusPanel.Visibility=Visibility.Collapsed;
        _appHealth="ready";
        _safeTelemetry.Record(DiagnosticEvent.Ready);
    }
    private void OnCoreProcessFailed(object? sender,CoreWebView2ProcessFailedEventArgs evt)
    {
        // A WebView2 renderer/GPU crash must show a visible recovery screen, not all-black.
        // Do not log raw JavaScript stacks, Telegram payloads, account IDs or QR values.
        if(_closing)return;
        Dispatcher.BeginInvoke(new Action(()=>{
            if(_closing)return;
            _appHealth="degraded";
            _safeTelemetry.Record(DiagnosticEvent.RecoverableError,FailureCategory.Protocol);
            Diagnostics.Text="界面渲染异常，请退出并重新启动软件。已保存的账号、剧本和设置不会因此被删除。";
            Browser.Visibility=Visibility.Hidden;
            StatusPanel.Visibility=Visibility.Visible;
        }));
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs evt)
    {
        if (_closing || !string.Equals(evt.Source, TrustedOrigin + "index.html", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            string jsonMessage = evt.TryGetWebMessageAsString();
            if (!IpcRequestGuard.TryValidate(jsonMessage, out AllowedIpcOperation operation))
            {
                _safeTelemetry.Record(DiagnosticEvent.IpcRejected); // never record submitted JSON
                return;
            }
            var request = JsonSerializer.Deserialize<BridgeRequest>(jsonMessage)!;
            if (operation is AllowedIpcOperation.TelegramAuthStatus or AllowedIpcOperation.TelegramAuthStart or
                AllowedIpcOperation.TelegramAuthSubmit or AllowedIpcOperation.TelegramAuthStop or
                AllowedIpcOperation.TelegramChatsRefresh or AllowedIpcOperation.TelegramManualMessage)
            {
                await HandleLiveTelegramAsync(jsonMessage,request.RequestId,request.Type);
                return;
            }
            if (operation is AllowedIpcOperation.ApiConfigStatus or AllowedIpcOperation.ApiConfigSave or AllowedIpcOperation.ApiConfigClear)
            {
                await HandleTelegramApiConfigurationAsync(jsonMessage,request.RequestId,request.Type);
                return;
            }
            if (operation is AllowedIpcOperation.SettingsGet or AllowedIpcOperation.SettingsSave)
            {
                await HandleSettingsRequestAsync(jsonMessage, request.RequestId, request.Type);
                return;
            }
            if (operation is AllowedIpcOperation.AccountsList or AllowedIpcOperation.AccountsCreate or
                AllowedIpcOperation.AccountsEdit or AllowedIpcOperation.AccountsArchive or
                AllowedIpcOperation.AccountsRestore)
            {
                await HandleAccountRequestAsync(jsonMessage, request.RequestId, request.Type);
                return;
            }
            if (operation is AllowedIpcOperation.GroupsList or AllowedIpcOperation.GroupsTopics
                or AllowedIpcOperation.GroupsMembers)
            {
                await HandleGroupRequestAsync(jsonMessage,request.RequestId,request.Type);
                return;
            }
            if(operation is AllowedIpcOperation.ScriptsList or AllowedIpcOperation.ScriptsGet or
                AllowedIpcOperation.ScriptsCreate or AllowedIpcOperation.ScriptsSave or
                AllowedIpcOperation.ScriptsDuplicate or AllowedIpcOperation.ScriptsArchive or
                AllowedIpcOperation.ScriptsRestore or AllowedIpcOperation.ScriptsExport or
                AllowedIpcOperation.ScriptsImport or AllowedIpcOperation.ScriptsPublish or
                AllowedIpcOperation.ScriptsVersions or AllowedIpcOperation.ScriptsPreview or
                AllowedIpcOperation.ScriptsPreflight)
            {
                await HandleScriptRequestAsync(jsonMessage,request.RequestId,request.Type);
                return;
            }
            if(operation is AllowedIpcOperation.MediaImport or AllowedIpcOperation.MediaPreview)
            {
                await HandleMediaRequestAsync(jsonMessage,request.RequestId,request.Type);
                return;
            }
            if (operation == AllowedIpcOperation.RuntimeSnapshot)
            {
                await HandleRuntimeSnapshotAsync(request.RequestId);
                return;
            }
            if (operation == AllowedIpcOperation.DiagnosticsGet)
            {
                SendSettingResponse(request.RequestId,"diagnostics.get.result",true,_safeTelemetry.Snapshot(_appHealth));
                return;
            }
            string type = operation switch {
                AllowedIpcOperation.SystemPing => "system.pong",
                AllowedIpcOperation.AppInfo => "app.info.response", _ => "denied"
            };
            bool allowed = type != "denied";
            Browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new {
                protocol = Protocol, requestId = request.RequestId, ok = allowed, type,
                payload = new { name = "MOSEN Group Scheduler", mode = "offline-foundation" }
            }));
        }
        catch (Exception) { _safeTelemetry.Record(DiagnosticEvent.IpcRejected); /* Never log input */ }
    }
}
