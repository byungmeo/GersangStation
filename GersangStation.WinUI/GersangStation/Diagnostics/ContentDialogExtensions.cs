using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using System.Threading;

namespace Microsoft.UI.Xaml.Controls;

/// <summary>
/// ContentDialog를 전역 coordinator로 표시하는 확장 메서드를 제공합니다.
/// </summary>
public static class ContentDialogExtensions
{
    /// <summary>
    /// 앱 공용 대화상자 coordinator를 거쳐 현재 대화상자를 표시합니다.
    /// </summary>
    public static Task<ContentDialogResult> ShowManagedAsync(this ContentDialog dialog, CancellationToken cancellationToken = default)
        => global::GersangStation.App.DialogCoordinator.ShowAsync(dialog, cancellationToken);
}
