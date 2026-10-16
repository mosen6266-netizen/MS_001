namespace Mosen.GroupScheduler.ResourceBudget;
/// <summary>Small deterministic LRU. Locked insert/read/eviction, no timers or background
/// sweeper, no unbounded retained history. Never cache sessions, auth or message content.</summary>
public sealed class BoundedLruCache<TKey,TValue> where TKey:notnull
{
    private readonly object _gate=new();
    private readonly Dictionary<TKey,LinkedListNode<(TKey Key,TValue Value)>> _nodes=new();
    private readonly LinkedList<(TKey Key,TValue Value)> _order=new();
    public int Capacity{get;}
    public BoundedLruCache(int capacity)
    {
        if(capacity is <1 or >1024)throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity=capacity;
    }
    public int Count{get{lock(_gate)return _nodes.Count;}}
    public void Set(TKey key,TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock(_gate)
        {
            if(_nodes.TryGetValue(key,out var existing))
            {
                existing.Value=(key,value);_order.Remove(existing);_order.AddFirst(existing);return;
            }
            var node=_order.AddFirst((key,value));_nodes.Add(key,node);
            if(_nodes.Count>Capacity)
            {
                var last=_order.Last!;
                _nodes.Remove(last.Value.Key);_order.RemoveLast();
            }
        }
    }
    public bool TryGet(TKey key,out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock(_gate)
        {
            if(_nodes.TryGetValue(key,out var entry))
            {
                _order.Remove(entry);_order.AddFirst(entry);value=entry.Value.Value;return true;
            }
            value=default!;return false;
        }
    }
    public bool Remove(TKey key)
    {
        lock(_gate)
        {
            if(!_nodes.Remove(key,out var entry))return false;
            _order.Remove(entry);return true;
        }
    }
    public void Clear(){lock(_gate){_nodes.Clear();_order.Clear();}}
}
