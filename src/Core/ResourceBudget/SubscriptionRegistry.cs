namespace Mosen.GroupScheduler.ResourceBudget;
/// <summary>Window-owned disposal registry. Replaces untracked event lambdas with
/// unsubscription closures. All resources are released exactly once, including late add.</summary>
public sealed class SubscriptionRegistry:IDisposable
{
    private readonly object _gate=new();
    private readonly Stack<Action> _subscriptions=new();
    private bool _disposed;
    public int Count{get{lock(_gate)return _subscriptions.Count;}}
    public void Track(Action unsubscribe)
    {
        ArgumentNullException.ThrowIfNull(unsubscribe);
        lock(_gate)
        {
            if(_disposed){unsubscribe();return;}
            _subscriptions.Push(unsubscribe);
        }
    }
    public void Dispose()
    {
        Action[] actions;
        lock(_gate)
        {
            if(_disposed)return;
            _disposed=true;actions=_subscriptions.ToArray();_subscriptions.Clear();
        }
        foreach(var action in actions)
        {
            try{action();}
            catch(Exception ex) when(ex is ObjectDisposedException or InvalidOperationException)
            { /* already disposed by owner */ }
        }
    }
}
