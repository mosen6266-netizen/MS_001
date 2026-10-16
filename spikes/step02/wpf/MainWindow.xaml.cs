using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace MS001.Step02;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            string runtime = CoreWebView2Environment.GetAvailableBrowserVersionString();
            await Browser.EnsureCoreWebView2Async();
            Browser.NavigateToString("<!doctype html><html lang=\"zh\"><meta charset=\"utf-8\"><body style=\"background:#141414;color:#eee;font:16px system-ui;padding:24px\"><h2>Windows WPF + WebView2 本地原型</h2><p>本页面不连接 Telegram，不收集凭据，不发送消息。</p></body></html>");
            RuntimeStatus.Text = "WebView2 Runtime detected: " + runtime;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            RuntimeStatus.Text = "WebView2 Runtime not installed: package/runtime prerequisite unmet.";
        }
        catch (Exception error)
        {
            RuntimeStatus.Text = "WebView2 initialization failed: " + error.GetType().Name;
        }
    }

    private async void ProbeButton_Click(object sender, RoutedEventArgs e)
    {
        ProbeButton.IsEnabled = false;
        try
        {
            TdlibStatus.Text = await Task.Run(TdLibAbiProbe.ProbeVersion);
        }
        finally
        {
            ProbeButton.IsEnabled = true;
        }
    }
}
