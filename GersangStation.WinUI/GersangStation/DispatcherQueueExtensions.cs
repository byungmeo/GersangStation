using System;
using System.Threading.Tasks;

namespace GersangStation;

public static class DispatcherQueueExtensions
{
    public static Task EnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue queue, Func<Task> action)
    {
        var tcs = new TaskCompletionSource<object?>();
        queue.TryEnqueue(async () =>
        {
            try
            {
                await action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}