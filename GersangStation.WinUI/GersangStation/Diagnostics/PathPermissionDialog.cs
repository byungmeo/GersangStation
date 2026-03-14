using Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace GersangStation.Diagnostics;

/// <summary>
/// 쓰기 권한 사전 점검 실패 시 해결 방법 안내와 계속 진행 여부를 묻는 공용 대화상자입니다.
/// </summary>
public static class PathPermissionDialog
{
    public const string PermissionHelpUrl = "https://github.com/byungmeo/GersangStation/wiki/Q&A#%EB%8B%A4%ED%81%B4%EB%9D%BC-%EC%83%9D%EC%84%B1-%EB%B6%88%EA%B0%80-%EB%AC%B8%EC%A0%9C";

    /// <summary>
    /// 권한 부족일 때만 해결 방법 링크 또는 계속 진행 여부를 묻는 대화상자를 표시합니다.
    /// </summary>
    public static async Task<bool> ConfirmContinueWhenPermissionMissingAsync(
        XamlRoot? xamlRoot,
        DirectoryWriteProbeResult probeResult)
    {
        if (!IsPermissionFailure(probeResult))
            return true;

        xamlRoot ??= (App.CurrentWindow?.Content as FrameworkElement)?.XamlRoot;
        if (xamlRoot is null)
            return false;

        ContentDialog dialog = new()
        {
            XamlRoot = xamlRoot,
            Title = "권한 확인 필요",
            Content =
                "현재 게임 설치 또는 설치 예정 경로에 대한 권한이 부족합니다.\n" +
                "\"해결 방법 확인하기\" 버튼을 누르면 이 문제에 대한 해결 방법을 확인하실 수 있습니다.\n" +
                "그래도 계속 하시겠습니까?",
            PrimaryButtonText = "해결 방법 확인하기",
            SecondaryButtonText = "네, 계속 하겠습니다.",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (App.CurrentWindow is Main.MainWindow window)
                window.NavigateToWebViewPage(PermissionHelpUrl);

            return false;
        }

        return result == ContentDialogResult.Secondary;
    }

    /// <summary>
    /// 사전 점검 실패가 권한 부족 때문인지 판별합니다.
    /// </summary>
    public static bool IsPermissionFailure(DirectoryWriteProbeResult probeResult)
    {
        Exception? exception = probeResult.Exception;
        return !probeResult.CanWrite &&
            exception is not null &&
            (exception is UnauthorizedAccessException || exception.HResult == unchecked((int)0x80070005));
    }
}
