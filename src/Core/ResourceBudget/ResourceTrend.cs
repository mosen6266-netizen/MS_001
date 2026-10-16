namespace Mosen.GroupScheduler.ResourceBudget;
/// <summary>One minute aggregate sample per minute; no unbounded retention, credentials,
/// strings or request payloads. Latest eight hours only.</summary>
public sealed class ResourceTrend
{
    private readonly object _lock=new();
    private readonly ResourceSample[] _ring;
    private long _slot=long.MinValue;
    private int _count;
    private int _next;
    public ResourceTrend(int capacity=480)
    {
        if(capacity is <1 or >480)throw new ArgumentOutOfRangeException(nameof(capacity));
        _ring=new ResourceSample[capacity];
    }
    public int Count{get{lock(_lock)return _count;}}
    public void Record(ResourceSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var minute=sample.CapturedUtc.ToUnixTimeSeconds()/60;
        lock(_lock)
        {
            if(minute<_slot)throw new ArgumentOutOfRangeException(nameof(sample));
            if(minute==_slot)
            {
                var pos=(_next-1+_ring.Length)%_ring.Length;
                _ring[pos]=sample;return;
            }
            _slot=minute;_ring[_next]=sample;_next=(_next+1)%_ring.Length;
            if(_count<_ring.Length)_count++;
        }
    }
    public IReadOnlyList<ResourceSample> Snapshot()
    {
        lock(_lock)
        {
            var result=new ResourceSample[_count];
            for(int i=0;i<_count;i++)
                result[i]=_ring[(_next-_count+i+_ring.Length)%_ring.Length];
            return result;
        }
    }
}
