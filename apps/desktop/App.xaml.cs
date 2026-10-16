using System;
using System.Threading;
using System.Windows;

namespace Mosen.GroupScheduler.Desktop;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instanceMutex = new Mutex(true, "Local\\MOSEN.GroupScheduler.Desktop.Singleton", out bool first);
        if (!first)
        {
            _instanceMutex.Dispose();
            _instanceMutex = null;
            MessageBox.Show("MOSEN Group Scheduler 已在运行。请切换到已有窗口。", "重复启动");
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
