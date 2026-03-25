using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Diagnostics;

/// <summary>
/// 앱 전체에서 ContentDialog를 한 번에 하나씩만 표시하도록 직렬화합니다.
/// </summary>
public sealed class ContentDialogCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// 지정한 대화상자를 전역 gate를 거쳐 안전하게 표시합니다.
    /// </summary>
    public async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        await _gate.WaitAsync();
        try
        {
            Microsoft.UI.Dispatching.DispatcherQueue? dispatcherQueue = App.UiDispatcherQueue ?? App.CurrentWindow?.DispatcherQueue;
            if (dispatcherQueue is null)
                throw new InvalidOperationException("ContentDialog를 표시할 UI DispatcherQueue를 찾을 수 없습니다.");

            ContentDialogResult result = ContentDialogResult.None;
            await dispatcherQueue.RunOrEnqueueAsync(async () =>
            {
                EnsureXamlRoot(dialog);
                result = await dialog.ShowAsync();
            });

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void EnsureXamlRoot(ContentDialog dialog)
    {
        if (dialog.XamlRoot is not null)
            return;

        if (App.CurrentWindow?.Content is FrameworkElement element && element.XamlRoot is not null)
            dialog.XamlRoot = element.XamlRoot;
    }
}
