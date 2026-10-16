using System;
using System.Threading;
using System.Windows;
using Mosen.GroupScheduler.Core.Telemetry;

namespace Mosen.GroupScheduler.Desktop;

public partial class App : System.Windows.Application
{
    private Mutex? _instanceMutex;
    private readonly SafeDiagnostics _safeTelemetry = SafeDiagnostics.ForCurrentUser();
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, fault) =>
        {
            _safeTelemetry.Record(DiagnosticEvent.FatalError, FailureClassifier.Classify(fault.Exception));
            // A generic message only; never display private exception text or stack.
            System.Windows.MessageBox.Show("程序遇到意外错误并将关闭。诊断文件不包含账号或消息内容。", "运行异常");
            fault.Handled = true;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, fault) =>
        {
            var category = fault.ExceptionObject is Exception ex
                ? FailureClassifier.Classify(ex) : FailureCategory.Unknown;
            _safeTelemetry.Record(DiagnosticEvent.FatalError, category);
        };
        TaskScheduler.UnobservedTaskException += (_, fault) =>
        {
            _safeTelemetry.Record(DiagnosticEvent.RecoverableError, FailureClassifier.Classify(fault.Exception));
            fault.SetObserved();
        };
        _instanceMutex = new Mutex(true, "Local\\MOSEN.GroupScheduler.Desktop.Singleton", out bool first);
        if (!first)
        {
            _instanceMutex.Dispose();
            _instanceMutex = null;
            System.Windows.MessageBox.Show("MOSEN Group Scheduler 已在运行。请切换到已有窗口。", "重复启动");
            Shutdown();
            return;
        }
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
