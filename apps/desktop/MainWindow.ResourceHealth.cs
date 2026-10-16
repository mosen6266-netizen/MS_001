using System;
using System.IO;
using System.Windows.Threading;
using Mosen.GroupScheduler.ResourceBudget;
using Mosen.GroupScheduler.Notifications;

namespace Mosen.GroupScheduler.Desktop;
public partial class MainWindow
{
    private readonly SubscriptionRegistry _webSubscriptions=new();
    private readonly ProcessResourceSampler _resourceSampler=new();
    private readonly ResourceBudgetMonitor _resourceBudget=new();
    private readonly ResourceTrend _resourceTrend=new();
    private DispatcherTimer? _budgetTimer;
    private ResourceDecision? _lastBudget;
    private int RuntimeDashboardPollSeconds=>_lastBudget?.Level switch {
        PressureLevel.Critical=>30,
        PressureLevel.Conserve=>15,
        _=>5
    };
    private void InitializeResourceHealth()
    {
        _budgetTimer=new DispatcherTimer(DispatcherPriority.Background){
            Interval=TimeSpan.FromSeconds(15)
        };
        _budgetTimer.Tick+=OnBudgetTick;
        _webSubscriptions.Track(()=>
        {
            if(_budgetTimer is not null)_budgetTimer.Tick-=OnBudgetTick;
        });
        _budgetTimer.Start();
    }
    private void OnBudgetTick(object? sender,EventArgs e)
    {
        if(_closing)return;
        try
        {
            var diagnosticRoot=Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
                "MOSEN","TelegramScheduler","diagnostics");
            long bytes=0;
            foreach(var name in new[]{"events.jsonl","events.previous.jsonl"})
            {
                var file=Path.Combine(diagnosticRoot,name);
                if(!File.Exists(file))continue;
                var info=new FileInfo(file);
                if((info.Attributes&FileAttributes.ReparsePoint)==0)
                    bytes=Math.Min(long.MaxValue-info.Length,bytes)+info.Length;
            }
            int timers=(_alertTimer?.IsEnabled==true?1:0)+
                (_budgetTimer?.IsEnabled==true?1:0);
            var sample=_resourceSampler.Sample(DateTimeOffset.UtcNow,timers,0,bytes);
            _resourceTrend.Record(sample);
            var decision=_resourceBudget.Observe(sample);
            _lastBudget=decision;
            if(_alertTimer is not null)
                _alertTimer.Interval=decision.AlertScanInterval;
            if(decision.Changed && _emergency?.IsStopped!=true)
            {
                SafetyNotice.Text=decision.Level switch{
                    PressureLevel.Normal=>"离线工程模式 · 资源恢复正常 · 无真实 Telegram 发送",
                    PressureLevel.Conserve=>"资源接近预算 · 已减缓非关键刷新",
                    _=>"资源超出预算 · 已显著减少非关键刷新，请检查任务"
                };
            }
            if(decision.ShouldNotify)
                PublishAlert(AlertCategory.ResourcePressure,"LocalBudget",sample.CapturedUtc);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or
            InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Diagnostics must never block the UI, crash the shell or reopen a send.
        }
    }
    private void StopResourceHealth()
    {
        _budgetTimer?.Stop();_budgetTimer=null;
    }
}
