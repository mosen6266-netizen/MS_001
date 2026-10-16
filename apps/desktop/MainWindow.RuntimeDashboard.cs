using System;
using System.Threading.Tasks;
using Mosen.GroupScheduler.Persistence;
using Mosen.GroupScheduler.RuntimeDashboard;

namespace Mosen.GroupScheduler.Desktop;
public partial class MainWindow
{
    private async Task HandleRuntimeSnapshotAsync(string requestId)
    {
        try
        {
            // No transport, no scheduler instance, no startup recovery mutations.
            var snapshot=await Task.Run(()=>new RuntimeDashboardReader(
                AppDatabase.ForCurrentUser()).Read(DateTimeOffset.UtcNow));
            if(!_closing)SendSettingResponse(requestId,"runtime.snapshot.result",true,new{
                checkedUtc=snapshot.CheckedUtc,mode=snapshot.Mode,summary=snapshot.Summary,
                items=snapshot.Items,pollHintSeconds=RuntimeDashboardPollSeconds});
        }
        catch(Exception)
        {
            if(!_closing)SendSettingResponse(requestId,"runtime.snapshot.result",false,
                new{code="DASHBOARD_UNAVAILABLE",message="无法读取本地任务状态，请检查数据库或诊断。"});
        }
    }
}
