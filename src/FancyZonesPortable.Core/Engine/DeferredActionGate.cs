namespace FancyZonesPortable.Core.Engine;

/// <summary>
/// Coalesces repeated requests into a single action while a deferred scope is active.
/// </summary>
public sealed class DeferredActionGate
{
    private readonly Action _flushAction;
    private int _deferDepth;
    private bool _hasPendingRequest;

    public DeferredActionGate(Action flushAction)
    {
        _flushAction = flushAction ?? throw new ArgumentNullException(nameof(flushAction));
    }

    public IDisposable Defer()
    {
        _deferDepth++;
        return new DeferredScope(this);
    }

    public void Request()
    {
        if (_deferDepth > 0)
        {
            _hasPendingRequest = true;
            return;
        }

        _flushAction();
    }

    private void EndDefer()
    {
        if (_deferDepth <= 0)
            return;

        _deferDepth--;
        if (_deferDepth == 0 && _hasPendingRequest)
        {
            _hasPendingRequest = false;
            _flushAction();
        }
    }

    private sealed class DeferredScope : IDisposable
    {
        private readonly DeferredActionGate _owner;
        private bool _disposed;

        public DeferredScope(DeferredActionGate owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _owner.EndDefer();
        }
    }
}
