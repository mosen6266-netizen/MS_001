using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush=System.Windows.Media.Brush;
using Brushes=System.Windows.Media.Brushes;
using Color=System.Windows.Media.Color;
using Button=System.Windows.Controls.Button;
using System.Windows.Threading;
using Mosen.GroupScheduler.Notifications;
using Mosen.GroupScheduler.OutboundPolicy;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.Scheduler;
using Mosen.GroupScheduler.RuntimeDashboard;

namespace Mosen.GroupScheduler.Desktop;
public partial class MainWindow
{
    private readonly NotificationCenter _alerts = new();
    private readonly OutboundCoordinator _notificationPolicy = new();
    private readonly AlertHistoryStore _alertStore=new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MOSEN","TelegramScheduler","alerts.json"));
    private EmergencyStopController? _emergency;
    private System.Windows.Forms.NotifyIcon? _tray;
    private System.Windows.Forms.ContextMenuStrip? _trayMenu;
    private DispatcherTimer? _alertTimer;
    private Window? _alertWindow;
    private Window? _historyWindow;
    private bool _refreshingAlerts;
    private DateTimeOffset _lastVisualAlertUtc=DateTimeOffset.MinValue;
    private string _visibleAlertKey=string.Empty;

    private static readonly Brush AccentGreen=new SolidColorBrush(Color.FromRgb(52,143,99));
    private static readonly Brush AccentRed=new SolidColorBrush(Color.FromRgb(177,62,57));
    private static readonly Brush DarkPanel=new SolidColorBrush(Color.FromRgb(29,34,42));

    private void InitializeAlertNotifications()
    {
        // No Windows background agent. Everything is owned by this window and disposed on close.
        _alerts.Restore(_alertStore.Load());
        var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MOSEN","TelegramScheduler");
        _emergency=new EmergencyStopController(_notificationPolicy,
            Path.Combine(folder,"emergency-stop.latch"),now=>
                new FairScheduler(new SchedulerCheckpointStore(AppDatabase.ForCurrentUser()),now));
        if(_emergency.IsStopped)
        {
            SafetyNotice.Text="紧急停止标记已存在 · 发送许可关闭 · 请人工检查本地任务";
            EmergencyStopButton.IsEnabled=false;
        }
        _trayMenu=new System.Windows.Forms.ContextMenuStrip();
        _trayMenu.Items.Add("打开主窗口",null,(_,_)=>Dispatcher.BeginInvoke((Action)BringMainWindowForward));
        _trayMenu.Items.Add("查看异常历史",null,(_,_)=>Dispatcher.BeginInvoke((Action)OpenAlertHistory));
        _trayMenu.Items.Add("全局紧急停止",null,(_,_)=>Dispatcher.BeginInvoke(
            (Action)(()=>EmergencyStopWithConfirmation())));
        _trayMenu.Items.Add("退出程序",null,(_,_)=>Dispatcher.BeginInvoke((Action)Close));
        _tray=new System.Windows.Forms.NotifyIcon {
            Text="MOSEN Group Scheduler · 仅离线模拟",
            Icon=System.Drawing.SystemIcons.Warning,
            ContextMenuStrip=_trayMenu,Visible=true
        };
        _tray.DoubleClick+=(_,_)=>Dispatcher.BeginInvoke((Action)BringMainWindowForward);
        _alertTimer=new DispatcherTimer(DispatcherPriority.Background) {
            Interval=TimeSpan.FromSeconds(6)
        };
        _alertTimer.Tick+=async (_,_)=>await ScanLocalAlertsAsync();
        _alertTimer.Start();
    }
    private async Task ScanLocalAlertsAsync()
    {
        if(_closing||_refreshingAlerts||_emergency?.IsStopped==true)return;
        _refreshingAlerts=true;
        try
        {
            var snapshot=await Task.Run(()=>new RuntimeDashboardReader(
                AppDatabase.ForCurrentUser()).Read(DateTimeOffset.UtcNow));
            if(_closing)return;
            var now=DateTimeOffset.UtcNow;
            foreach(var task in snapshot.Items)
            {
                var category=task.IssueCode switch {
                    "OUTCOME_UNKNOWN"=>AlertCategory.DeliveryUncertain,
                    "TASK_FAILED"=>AlertCategory.TaskFailed,
                    "REQUIRES_REVIEW" or "DELIVERY_REVIEW"=>AlertCategory.RecoveryRequired,
                    _=>task.State=="Failed"?AlertCategory.TaskFailed:(AlertCategory?)null
                };
                if(category is not null)PublishAlert(category.Value,task.RunId,now);
            }
            if(snapshot.Summary.CorruptRecords>0)
                PublishAlert(AlertCategory.CorruptRecord,"Quarantine",now);
        }
        catch(Exception)
        {
            if(!_closing)PublishAlert(AlertCategory.RecoveryRequired,"LocalSnapshotUnavailable",
                DateTimeOffset.UtcNow);
        }
        finally{_refreshingAlerts=false;}
    }
    private void PublishAlert(AlertCategory category,string key,DateTimeOffset now)
    {
        var result=_alerts.Publish(category,key,now);
        if(result.IsNew)_= _alertStore.Save(_alerts.Snapshot(500));
        if(!result.ShouldPop||_closing)return;
        // Distinct faults still enter history, but one burst must not queue dozens of tray balloons.
        if(now-_lastVisualAlertUtc<TimeSpan.FromSeconds(20))return;
        _lastVisualAlertUtc=now;
        var safeText=NotificationCenter.Describe(category);
        if(_tray is not null)
        {
            _tray.BalloonTipTitle="MOSEN 任务提醒";
            _tray.BalloonTipText=safeText;
            _tray.BalloonTipIcon=System.Windows.Forms.ToolTipIcon.Warning;
            _tray.ShowBalloonTip(3000);
        }
        ShowSingleTopmostAlert(safeText,result.Alert.Key);
    }
    private void ShowSingleTopmostAlert(string message,string alertKey)
    {
        _visibleAlertKey=alertKey;
        if(_alertWindow is not null)
        {
            if(_alertWindow.Content is StackPanel stack&&stack.Children.OfType<TextBlock>().FirstOrDefault() is { } label)
                label.Text=message;
            _alertWindow.Activate();
            return; // never stack multiple popups
        }
        var dialog=new Window {
            Title="MOSEN · 任务异常提醒",Width=430,Height=200,
            MinWidth=390,WindowStartupLocation=WindowStartupLocation.CenterScreen,
            ResizeMode=ResizeMode.NoResize,Topmost=true,
            ShowInTaskbar=true,Background=DarkPanel,Foreground=Brushes.White
        };
        var layout=new StackPanel {Margin=new Thickness(22)};
        layout.Children.Add(new TextBlock{Text=message,FontSize=15,FontWeight=FontWeights.SemiBold,
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,22)});
        var buttons=new StackPanel {Orientation=System.Windows.Controls.Orientation.Horizontal,HorizontalAlignment=System.Windows.HorizontalAlignment.Right};
        var history=MakeButton("查看历史",DarkPanel);
        history.Click+=(_,_)=>OpenAlertHistory();
        var acknowledge=MakeButton("知道了",AccentGreen);
        acknowledge.Click+=(_,_)=>{_alerts.Acknowledge(_visibleAlertKey);_=_alertStore.Save(_alerts.Snapshot(500));dialog.Close();};
        buttons.Children.Add(history);buttons.Children.Add(acknowledge);
        layout.Children.Add(buttons);dialog.Content=layout;
        dialog.Closed+=(_,_)=>{if(ReferenceEquals(_alertWindow,dialog))_alertWindow=null;};
        _alertWindow=dialog;dialog.Show();
        dialog.Activate();
    }
    private static Button MakeButton(string text,Brush background)=>new() {
        Content=text,Background=background,Foreground=Brushes.White,BorderBrush=Brushes.DimGray,
        Padding=new Thickness(18,9,18,9),Margin=new Thickness(5,0,0,0),
        Cursor=System.Windows.Input.Cursors.Hand,MinWidth=100
    };
    private void BringMainWindowForward()
    {
        if(_closing)return;
        if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;
        Show();Activate();Topmost=true;Topmost=false;Focus();
    }
    private void OnOpenAlertHistory(object sender,RoutedEventArgs e)=>OpenAlertHistory();
    private void OpenAlertHistory()
    {
        if(_closing)return;
        if(_historyWindow is not null){_historyWindow.Activate();return;}
        var dialog=new Window{
            Title="MOSEN · 本地异常历史",Width=620,Height=460,MinWidth=430,MinHeight=280,
            Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Background=DarkPanel,Foreground=Brushes.White
        };
        var panel=new DockPanel {Margin=new Thickness(16)};
        var title=new TextBlock{Text="最近500条本地异常记录（无消息正文）",
            FontSize=14,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)};
        DockPanel.SetDock(title,Dock.Top);panel.Children.Add(title);
        var viewer=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var items=new StackPanel();
        var list=_alerts.Snapshot(500);
        if(list.Count==0)items.Children.Add(new TextBlock{Text="目前没有已记录的异常。",
            Foreground=Brushes.LightGray});
        foreach(var alert in list)
        {
            items.Children.Add(new TextBlock{Text=alert.LatestUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                +"   "+NotificationCenter.Describe(alert.Category)+"   ×"+alert.Occurrences,
                Foreground=alert.Acknowledged?Brushes.LightGray:Brushes.LightSalmon,
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,8)});
        }
        viewer.Content=items;panel.Children.Add(viewer);dialog.Content=panel;
        dialog.Closed+=(_,_)=>{if(ReferenceEquals(_historyWindow,dialog))_historyWindow=null;};
        _historyWindow=dialog;dialog.Show();dialog.Activate();
    }
    private void OnEmergencyStopClick(object sender,RoutedEventArgs e)=>EmergencyStopWithConfirmation();
    private void EmergencyStopWithConfirmation()
    {
        if(_closing||_emergency is null)return;
        if(_emergency.IsStopped){SafetyNotice.Text="紧急停止已生效，禁止自动恢复。";return;}
        var dlg=new Window {
            Title="确认全局紧急停止",Width=440,Height=225,
            ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Owner=this,Topmost=true,Background=DarkPanel,Foreground=Brushes.White
        };
        var root=new StackPanel {Margin=new Thickness(22)};
        root.Children.Add(new TextBlock{Text="确定要停止所有本地模拟群任务吗？",
            FontWeight=FontWeights.SemiBold,FontSize=15,TextWrapping=TextWrapping.Wrap,
            Margin=new Thickness(0,0,0,10)});
        root.Children.Add(new TextBlock{Text="将立即关闭出站许可、写入停止标记，并停止全部模拟任务；不会自动恢复。",
            TextWrapping=TextWrapping.Wrap,Foreground=Brushes.LightGray,
            Margin=new Thickness(0,0,0,21)});
        var row=new StackPanel{Orientation=System.Windows.Controls.Orientation.Horizontal,HorizontalAlignment=System.Windows.HorizontalAlignment.Right};
        var confirm=MakeButton("确认停止",AccentGreen);
        var cancel=MakeButton("取消",AccentRed);
        confirm.Click+=(_,_)=>{dlg.DialogResult=true;dlg.Close();};
        cancel.Click+=(_,_)=>{dlg.DialogResult=false;dlg.Close();};
        row.Children.Add(confirm);row.Children.Add(cancel);
        root.Children.Add(row);dlg.Content=root;
        if(dlg.ShowDialog()!=true)return;
        var result=_emergency.Stop(DateTimeOffset.UtcNow);
        _alertTimer?.Stop();
        EmergencyStopButton.IsEnabled=false;
        SafetyNotice.Text=result.PersistentLatchWritten&&result.AllSimulationsStopped
            ?"已全局停止 · 所有本地模拟任务已终止 · 不可自动恢复"
            :"发送许可已关闭 · 停止记录或任务落盘需人工复核";
        PublishAlert(AlertCategory.EmergencyStop,"Global",DateTimeOffset.UtcNow);
        if(!result.PersistentLatchWritten||!result.AllSimulationsStopped)
        {
            SafetyNotice.Foreground=Brushes.LightSalmon;
            ShowSingleTopmostAlert("紧急停止未全部写入磁盘；许可已关闭，请人工核对本地任务。",
                "EmergencyStop:Global");
        }
    }
    private void StopAlertNotifications()
    {
        _notificationPolicy.Shutdown();
        _alertTimer?.Stop();_alertTimer=null;
        _tray?.Dispose();_tray=null;
        _trayMenu?.Dispose();_trayMenu=null;
        _alertWindow?.Close();_alertWindow=null;
        _historyWindow?.Close();_historyWindow=null;
    }
}
