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

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            EnsureXamlRoot(dialog);
            return await dialog.ShowAsync();
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
