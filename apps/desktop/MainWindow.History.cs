using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Color=System.Windows.Media.Color;
using Brushes=System.Windows.Media.Brushes;
using ComboBox=System.Windows.Controls.ComboBox;
using TextBox=System.Windows.Controls.TextBox;
using Mosen.GroupScheduler.History;
using Mosen.GroupScheduler.Persistence;
using SaveFileDialog=Microsoft.Win32.SaveFileDialog;

namespace Mosen.GroupScheduler.Desktop;
public partial class MainWindow
{
    private Window? _taskHistoryWindow;
    private HistoryRepository? _historyRepository;
    private readonly List<long?> _historyCursors=new(){null};
    private int _historyPageIndex;
    private bool _historyBusy;
    private HistorySeverity? _historySeverity;
    private StackPanel? _historyRows;
    private TextBlock? _historyPosition;
    private TextBlock? _historySummary;
    private System.Windows.Controls.Button? _historyPrev;
    private System.Windows.Controls.Button? _historyNext;
    private static readonly System.Windows.Media.Brush HistoryPanel=
        new SolidColorBrush(Color.FromRgb(29,34,42));

    private void OnOpenTaskHistory(object sender,RoutedEventArgs e)=>OpenTaskHistory();
    private void OpenTaskHistory()
    {
        if(_closing)return;
        if(_taskHistoryWindow is not null){_taskHistoryWindow.Activate();return;}
        var view=new Window {
            Title="MOSEN · 任务历史与审计",Width=840,Height=630,
            MinWidth=610,MinHeight=440,Owner=this,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Background=HistoryPanel,Foreground=Brushes.White
        };
        var shell=new DockPanel{Margin=new Thickness(18)};
        var header=new StackPanel{Margin=new Thickness(0,0,0,14)};
        header.Children.Add(new TextBlock{Text="任务历史与审计（离线只读记录）",
            FontSize=18,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,6)});
        header.Children.Add(new TextBlock{Text="按时间查看任务状态与投递异常，不显示消息正文、账号或群成员信息。",
            Foreground=Brushes.LightGray,FontSize=12,TextWrapping=TextWrapping.Wrap});
        _historySummary=new TextBlock{Text="正在读取已保存记录…",Foreground=Brushes.LightGray,
            Margin=new Thickness(0,12,0,0)};
        header.Children.Add(_historySummary);
        DockPanel.SetDock(header,Dock.Top);shell.Children.Add(header);
        var toolbar=new WrapPanel{Margin=new Thickness(0,0,0,12)};
        var filter=new ComboBox{MinWidth=140,Margin=new Thickness(0,0,8,0),SelectedIndex=0,
            ItemsSource=new[]{"全部事件","普通记录","需注意","异常错误"}};
        filter.SelectionChanged+=async (_,_)=>{
            _historySeverity=filter.SelectedIndex switch{1=>HistorySeverity.Info,
                2=>HistorySeverity.Warning,3=>HistorySeverity.Error,_=>null};
            _historyCursors.Clear();_historyCursors.Add(null);_historyPageIndex=0;
            await ReadTaskHistoryPageAsync();
        };
        var refresh=HistoryButton("刷新记录");
        refresh.Click+=async (_,_)=>await ReadTaskHistoryPageAsync(sync:true);
        var export=HistoryButton("导出脱敏诊断 ZIP");
        export.Click+=async (_,_)=>await ExportTaskSupportBundleAsync();
        var prune=HistoryButton("保留与清理");
        prune.Click+=async (_,_)=>await PruneTaskHistoryAsync();
        toolbar.Children.Add(filter);toolbar.Children.Add(refresh);
        toolbar.Children.Add(export);toolbar.Children.Add(prune);
        DockPanel.SetDock(toolbar,Dock.Top);shell.Children.Add(toolbar);
        var footer=new StackPanel{Orientation=System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment=System.Windows.HorizontalAlignment.Right,
            Margin=new Thickness(0,12,0,0)};
        _historyPrev=HistoryButton("上一页");
        _historyNext=HistoryButton("下一页");
        _historyPosition=new TextBlock{Text="第1页 · 每页10条",VerticalAlignment=VerticalAlignment.Center,
            Foreground=Brushes.LightGray,Margin=new Thickness(9,0,9,0)};
        _historyPrev.Click+=async (_,_)=>{
            if(_historyPageIndex==0||_historyBusy)return;
            _historyPageIndex--;await ReadTaskHistoryPageAsync();
        };
        _historyNext.Click+=async (_,_)=>{
            if(_historyBusy||_historyCursors.Count<=_historyPageIndex+1)return;
            _historyPageIndex++;await ReadTaskHistoryPageAsync();
        };
        footer.Children.Add(_historyPrev);footer.Children.Add(_historyPosition);
        footer.Children.Add(_historyNext);DockPanel.SetDock(footer,Dock.Bottom);
        shell.Children.Add(footer);
        _historyRows=new StackPanel();
        var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=_historyRows};
        shell.Children.Add(scroll);view.Content=shell;
        view.Closed+=(_,_)=>{
            if(ReferenceEquals(_taskHistoryWindow,view))_taskHistoryWindow=null;
            _historyRows=null;_historyPosition=null;_historySummary=null;
            _historyPrev=null;_historyNext=null;_historyCursors.Clear();_historyCursors.Add(null);
            _historyPageIndex=0;
        };
        _taskHistoryWindow=view;view.Show();
        _=ReadTaskHistoryPageAsync(sync:true);
    }
    private static System.Windows.Controls.Button HistoryButton(string text)=>new(){
        Content=text,Padding=new Thickness(12,8,12,8),Background=HistoryPanel,
        Foreground=Brushes.White,BorderBrush=Brushes.Gray,
        Margin=new Thickness(0,0,8,0),MinWidth=80,
        Cursor=System.Windows.Input.Cursors.Hand
    };
    private async Task ReadTaskHistoryPageAsync(bool sync=false)
    {
        if(_historyBusy||_closing||_taskHistoryWindow is null)return;
        _historyBusy=true;
        var pageIndex=_historyPageIndex;
        var before=_historyCursors[pageIndex];
        try
        {
            var data=await Task.Run(()=>{
                var repo=_historyRepository??new HistoryRepository(AppDatabase.ForCurrentUser());
                if(sync)repo.CaptureLocalSnapshot(DateTimeOffset.UtcNow);
                var summary=repo.Summarize();
                var page=repo.ReadPage(10,before,_historySeverity);
                return (Repo:repo,Summary:summary,Page:page);
            });
            if(_taskHistoryWindow is null||_closing)return;
            _historyRepository=data.Repo;
            if(_historySummary is not null)_historySummary.Text=
                $"总记录 {data.Summary.Total:N0} · 需注意 {data.Summary.Warnings:N0} · 错误 {data.Summary.Errors:N0}";
            _historyRows?.Children.Clear();
            if(data.Page.Items.Count==0)_historyRows?.Children.Add(new TextBlock{
                Text="此筛选条件下没有历史记录。",Foreground=Brushes.LightGray,
                Margin=new Thickness(0,20,0,0)});
            foreach(var item in data.Page.Items)
            {
                var row=new Border{BorderBrush=new SolidColorBrush(Color.FromRgb(66,73,81)),
                    BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(0,12,0,12)};
                var line=new DockPanel();
                var level=item.Severity==HistorySeverity.Error?"错误":
                    item.Severity==HistorySeverity.Warning?"需注意":"普通";
                var badge=new TextBlock{Text=level,Width=63,FontWeight=FontWeights.Bold,
                    Foreground=item.Severity==HistorySeverity.Error?Brushes.LightSalmon:
                    item.Severity==HistorySeverity.Warning?Brushes.Khaki:Brushes.LightGreen};
                DockPanel.SetDock(badge,Dock.Left);line.Children.Add(badge);
                var detail=new TextBlock{Text=$"{item.EventUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}    "+
                    HistoryDescription(item.Code),TextWrapping=TextWrapping.Wrap,FontSize=12,
                    Foreground=Brushes.White};
                line.Children.Add(detail);row.Child=line;_historyRows?.Children.Add(row);
            }
            if(data.Page.HasMore&&data.Page.NextBeforeSeq is not null)
            {
                if(_historyCursors.Count<=pageIndex+1)_historyCursors.Add(data.Page.NextBeforeSeq);
                else _historyCursors[pageIndex+1]=data.Page.NextBeforeSeq;
            }
            else if(_historyCursors.Count>pageIndex+1)
                _historyCursors.RemoveRange(pageIndex+1,_historyCursors.Count-(pageIndex+1));
            if(_historyPosition is not null)_historyPosition.Text=$"第{pageIndex+1}页 · 每页10条";
            if(_historyPrev is not null)_historyPrev.IsEnabled=pageIndex>0;
            if(_historyNext is not null)_historyNext.IsEnabled=data.Page.HasMore;
        }
        catch(Exception)
        {
            if(_historySummary is not null)_historySummary.Text=
                "读取历史失败。已保留原始数据，请检查磁盘或诊断信息。";
        }
        finally{_historyBusy=false;}
    }
    private static string HistoryDescription(HistoryCode code)=>code switch{
        HistoryCode.TaskReady=>"模拟群任务待启动",
        HistoryCode.TaskRunning=>"模拟群任务运行中",
        HistoryCode.TaskPaused=>"模拟群任务已暂停",
        HistoryCode.TaskReview=>"模拟群任务等待人工复核",
        HistoryCode.TaskCompleted=>"模拟群任务已完成",
        HistoryCode.TaskStopped=>"模拟群任务已停止",
        HistoryCode.TaskFailed=>"模拟群任务失败",
        HistoryCode.DeliveryPlanned=>"投递事务已登记",
        HistoryCode.DeliverySubmitting=>"发送意图已登记",
        HistoryCode.DeliveryPending=>"投递等待可信回执",
        HistoryCode.DeliveryConfirmed=>"可信回执已确认",
        HistoryCode.DeliveryFailed=>"投递明确失败",
        HistoryCode.DeliveryUnknown=>"投递结果未知，禁止自动补发",
        HistoryCode.DeliveryReview=>"投递需人工核对",
        HistoryCode.CorruptRecord=>"本地记录损坏隔离",
        HistoryCode.EmergencyStopped=>"紧急停止记录",
        HistoryCode.HistoryPurged=>"用户清理派生历史",
        _=>"运行事件"
    };
    private async Task ExportTaskSupportBundleAsync()
    {
        if(_closing||_taskHistoryWindow is null)return;
        var dialog=new SaveFileDialog {
            Title="导出脱敏支持诊断包",Filter="MOSEN 安全诊断 ZIP (*.zip)|*.zip",
            DefaultExt=".zip",AddExtension=true,OverwritePrompt=true,
            FileName="MOSEN-Support-"+DateTime.Now.ToString("yyyyMMdd-HHmm")+".zip"
        };
        if(dialog.ShowDialog(_taskHistoryWindow)!=true)return;
        try
        {
            var dest=dialog.FileName;
            var result=await Task.Run(()=>{
                var repo=_historyRepository??new HistoryRepository(AppDatabase.ForCurrentUser());
                var bundle=SupportBundleBuilder.Build(repo,DateTimeOffset.UtcNow);
                var temporary=dest+".partial-"+Guid.NewGuid().ToString("N");
                try
                {
                    using var file=new FileStream(temporary,FileMode.CreateNew,
                        FileAccess.Write,FileShare.None);
                    file.Write(bundle.Archive);file.Flush(flushToDisk:true);
                    file.Close();File.Move(temporary,dest,overwrite:true);
                }
                finally{if(File.Exists(temporary))File.Delete(temporary);}
                return bundle;
            });
            if(_taskHistoryWindow is not null)
                System.Windows.MessageBox.Show(_taskHistoryWindow,
                    $"已保存脱敏诊断包。支持编号：{result.TicketId}\n最多只包含200条固定类别记录。",
                    "导出完成",MessageBoxButton.OK,MessageBoxImage.Information);
        }
        catch(Exception)
        {
            if(_taskHistoryWindow is not null)
                System.Windows.MessageBox.Show(_taskHistoryWindow,"导出失败，请检查磁盘空间与权限。",
                    "无法导出",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
    private async Task PruneTaskHistoryAsync()
    {
        if(_closing||_taskHistoryWindow is null)return;
        var dialog=new Window {Title="历史保留与清理",Owner=_taskHistoryWindow,Width=465,Height=265,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,
            ResizeMode=ResizeMode.NoResize,Background=HistoryPanel,Foreground=Brushes.White};
        var stack=new StackPanel{Margin=new Thickness(22)};
        stack.Children.Add(new TextBlock{Text="只清理派生历史，不删除原始回执、账号、剧本或停止标记。",
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
        var select=new ComboBox{ItemsSource=new[]{"保留7天","保留30天","保留90天","保留180天","保留365天",
            "清空全部派生历史"},SelectedIndex=2,Margin=new Thickness(0,0,0,13)};
        stack.Children.Add(select);
        var input=new TextBox{Text="",MinHeight=34,Background=new SolidColorBrush(Color.FromRgb(40,46,54)),
            Foreground=Brushes.White,Padding=new Thickness(8),
            ToolTip="输入 清理历史 以确认"};
        stack.Children.Add(new TextBlock{Text="请输入“清理历史”以确认：",
            Margin=new Thickness(0,0,0,5)});
        stack.Children.Add(input);
        var buttons=new StackPanel{Orientation=System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment=System.Windows.HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        var confirm=HistoryButton("确认清理");
        confirm.Background=new SolidColorBrush(Color.FromRgb(47,131,91));
        var cancel=HistoryButton("取消");
        cancel.Background=new SolidColorBrush(Color.FromRgb(157,56,50));
        confirm.Click+=(_,_)=>{if(input.Text=="清理历史")dialog.DialogResult=true;};
        cancel.Click+=(_,_)=>dialog.DialogResult=false;
        buttons.Children.Add(confirm);buttons.Children.Add(cancel);
        stack.Children.Add(buttons);dialog.Content=stack;
        if(dialog.ShowDialog()!=true)return;
        var choice=select.SelectedIndex;
        try
        {
            var result=await Task.Run(()=>{
                var repo=_historyRepository??new HistoryRepository(AppDatabase.ForCurrentUser());
                if(choice==5)return repo.ClearAll("清理历史",DateTimeOffset.UtcNow);
                var days=new[]{7,30,90,180,365}[choice];
                return repo.Prune(days,DateTimeOffset.UtcNow,confirmed:true);
            });
            _historyCursors.Clear();_historyCursors.Add(null);_historyPageIndex=0;
            await ReadTaskHistoryPageAsync();
            if(_taskHistoryWindow is not null)
                System.Windows.MessageBox.Show(_taskHistoryWindow,
                    $"已清理 {result.Removed:N0} 条派生历史，原始审计回执保留。",
                    "清理完成",MessageBoxButton.OK,MessageBoxImage.Information);
        }
        catch(Exception)
        {
            if(_taskHistoryWindow is not null)
                System.Windows.MessageBox.Show(_taskHistoryWindow,
                    "清理未完成。原始回执和账号数据没有更改。","清理失败",
                    MessageBoxButton.OK,MessageBoxImage.Warning);
        }
    }
}
