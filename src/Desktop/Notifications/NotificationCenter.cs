namespace Mosen.GroupScheduler.Notifications;

/// <summary>Safe, bounded, deduplicated operational alerts. Never store raw Telegram errors,
/// usernames, message bodies, tokens or phone numbers.</summary>
public enum AlertCategory { TaskFailed, DeliveryUncertain, RecoveryRequired, CorruptRecord, EmergencyStop }
public sealed record OperationalAlert(string Key,AlertCategory Category,DateTimeOffset FirstUtc,
    DateTimeOffset LatestUtc,int Occurrences,bool Acknowledged);
public sealed record AlertPublishResult(OperationalAlert Alert,bool IsNew,bool ShouldPop);
public sealed class NotificationCenter
{
    private readonly object _mutex=new();
    private readonly List<OperationalAlert> _history=new();
    private readonly TimeSpan _quiet;
    public NotificationCenter(TimeSpan? quiet=null)
    {
        _quiet=quiet??TimeSpan.FromMinutes(5);
        if(_quiet<TimeSpan.FromSeconds(10))throw new ArgumentOutOfRangeException(nameof(quiet));
    }
    public AlertPublishResult Publish(AlertCategory category,string stableKey,DateTimeOffset now)
    {
        if(string.IsNullOrWhiteSpace(stableKey)||stableKey.Length>80||
            !stableKey.All(c=>c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_' or ':'))
            throw new ArgumentException("Only opaque safe record IDs are allowed.");
        var key=category+":"+stableKey;
        lock(_mutex)
        {
            int index=_history.FindIndex(x=>x.Key==key);
            if(index>=0)
            {
                var prior=_history[index];
                if(now<prior.LatestUtc)throw new ArgumentOutOfRangeException(nameof(now));
                bool popup=prior.Acknowledged && now-prior.LatestUtc>=_quiet;
                var updated=prior with {LatestUtc=now,Occurrences=prior.Occurrences+1,
                    Acknowledged=popup?false:prior.Acknowledged};
                _history[index]=updated;
                return new(updated,false,popup);
            }
            var first=new OperationalAlert(key,category,now,now,1,false);
            if(_history.Count>=500)_history.RemoveAt(0);
            _history.Add(first);
            return new(first,true,true);
        }
    }
    public void Restore(IReadOnlyList<OperationalAlert> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        lock(_mutex)
        {
            if(_history.Count!=0)throw new InvalidOperationException("Restore once into a fresh center.");
            foreach(var item in items.Take(500))
            {
                if(item.Key.StartsWith(item.Category+":",StringComparison.Ordinal)&&
                   item.Occurrences>0&&item.FirstUtc<=item.LatestUtc&&
                   !_history.Any(existing=>existing.Key==item.Key))
                    _history.Add(item);
            }
        }
    }
    public bool Acknowledge(string key)
    {
        lock(_mutex)
        {
            var idx=_history.FindIndex(x=>x.Key==key);
            if(idx<0)return false;
            _history[idx]=_history[idx] with {Acknowledged=true};
            return true;
        }
    }
    public IReadOnlyList<OperationalAlert> Snapshot(int limit=50)
    {
        if(limit is <1 or >500)throw new ArgumentOutOfRangeException(nameof(limit));
        lock(_mutex)return _history.OrderByDescending(x=>x.LatestUtc)
            .Take(limit).ToArray();
    }
    public static string Describe(AlertCategory category)=>category switch {
        AlertCategory.TaskFailed=>"群任务发生故障，请核对运行记录",
        AlertCategory.DeliveryUncertain=>"消息发送结果未知，已阻止自动重试",
        AlertCategory.RecoveryRequired=>"群任务需人工复核后方可继续",
        AlertCategory.CorruptRecord=>"本地任务记录已隔离，请检查存储",
        AlertCategory.EmergencyStop=>"全局紧急停止已触发",
        _=>"运行异常，需人工确认"
    };
}
