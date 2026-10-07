using System;
using System.Threading;
using System.Threading.Tasks;

namespace SideDrawer.Controls;

/// <summary>Handled=true이면 닫기를 취소합니다. 비동기 결정은 GetDeferral()로 보호합니다.</summary>
public sealed class DrawerClosingEventArgs : EventArgs
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _pending = 1;
    private bool _sealed;
    private bool _handled;

    internal DrawerClosingEventArgs(DrawerCloseReason reason) => Reason = reason;
    public DrawerCloseReason Reason { get; }
    public bool Handled
    {
        get { lock (_gate) return _handled; }
        set { lock (_gate) _handled = value; }
    }

    public DrawerClosingDeferral GetDeferral()
    {
        lock (_gate)
        {
            if (_sealed) throw new InvalidOperationException("GetDeferral must be called before the Closing handler returns.");
            _pending++;
            return new DrawerClosingDeferral(CompleteOne);
        }
    }

    internal Task<bool> WaitAsync() => _completion.Task;
    internal void Seal()
    {
        lock (_gate)
        {
            if (_sealed) return;
            _sealed = true;
            CompleteOne();
        }
    }

    internal void Abort()
    {
        lock (_gate)
        {
            _sealed = true;
            _handled = true;
            _completion.TrySetResult(false);
        }
    }

    private void CompleteOne()
    {
        lock (_gate)
        {
            if (--_pending == 0) _completion.TrySetResult(!_handled);
        }
    }
}

/// <summary>Complete/Dispose는 중복 호출해도 안전합니다.</summary>
public sealed class DrawerClosingDeferral : IDisposable
{
    private Action? _complete;
    internal DrawerClosingDeferral(Action complete) => _complete = complete;
    public void Complete() => Interlocked.Exchange(ref _complete, null)?.Invoke();
    public void Dispose() => Complete();
}
