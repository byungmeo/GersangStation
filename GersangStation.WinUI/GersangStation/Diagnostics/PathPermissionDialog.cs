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
    private const int AccessDeniedHResult = unchecked((int)0x80070005);

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

        ContentDialog dialog = CreateDialog(
            xamlRoot,
            "현재 게임 설치 또는 설치 예정 경로에 대한 권한이 부족합니다.\n" +
            "\"해결 방법 확인하기\" 버튼을 누르면 이 문제에 대한 해결 방법을 확인하실 수 있습니다.\n" +
            "그래도 계속 하시겠습니까?",
            allowContinue: true);

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            NavigateToPermissionHelp();

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
        return !probeResult.CanWrite && IsPermissionFailure(exception);
    }

    /// <summary>
    /// 실제 작업 도중 발생한 권한 예외라면 해결 방법 안내 대화상자를 표시합니다.
    /// </summary>
    public static async Task<bool> ShowFailureGuidanceWhenPermissionMissingAsync(
        XamlRoot? xamlRoot,
        Exception? exception,
        string actionDisplayName)
    {
        if (!IsPermissionFailure(exception))
            return false;

        xamlRoot ??= (App.CurrentWindow?.Content as FrameworkElement)?.XamlRoot;
        if (xamlRoot is null)
            return false;

        string? targetPath = TryGetRelatedPath(exception);
        string content =
            $"{actionDisplayName} 중 대상 경로에 쓸 권한이 없어 작업을 완료하지 못했습니다.\n" +
            "\"해결 방법 확인하기\" 버튼을 누르면 관리자 권한 실행이나 경로 권한 변경 방법을 확인하실 수 있습니다.";

        if (!string.IsNullOrWhiteSpace(targetPath))
            content += $"\n\n확인할 경로: {targetPath}";

        ContentDialog dialog = CreateDialog(xamlRoot, content, allowContinue: false);

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            NavigateToPermissionHelp();

        return true;
    }

    /// <summary>
    /// 예외 체인 전체를 확인해 권한 부족 계열 예외인지 판별합니다.
    /// </summary>
    public static bool IsPermissionFailure(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is UnauthorizedAccessException || current.HResult == AccessDeniedHResult)
                return true;
        }

        return false;
    }

    private static string? TryGetRelatedPath(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is GameClientHelper.MultiClientCreationException multiClientException)
            {
                if (!string.IsNullOrWhiteSpace(multiClientException.DestinationPath))
                    return multiClientException.DestinationPath;

                if (!string.IsNullOrWhiteSpace(multiClientException.SourcePath))
                    return multiClientException.SourcePath;
            }

            if (current is GameInstallManager.GameInstallOperationException gameInstallException)
            {
                if (!string.IsNullOrWhiteSpace(gameInstallException.InstallPath))
                    return gameInstallException.InstallPath;

                if (!string.IsNullOrWhiteSpace(gameInstallException.ArchivePath))
                    return gameInstallException.ArchivePath;
            }

            if (current is PatchManager.PatchOperationException patchOperationException)
            {
                if (!string.IsNullOrWhiteSpace(patchOperationException.TargetPath))
                    return patchOperationException.TargetPath;
            }

            if (current is ExtractorWorkerException extractorWorkerException)
            {
                if (!string.IsNullOrWhiteSpace(extractorWorkerException.DestinationPath))
                    return extractorWorkerException.DestinationPath;

                if (!string.IsNullOrWhiteSpace(extractorWorkerException.ArchivePath))
                    return extractorWorkerException.ArchivePath;
            }
        }

        return null;
    }

    private static ContentDialog CreateDialog(XamlRoot xamlRoot, string content, bool allowContinue)
        => new()
        {
            XamlRoot = xamlRoot,
            Title = "권한 확인 필요",
            Content = content,
            PrimaryButtonText = "해결 방법 확인하기",
            SecondaryButtonText = allowContinue ? "네, 계속 하겠습니다." : string.Empty,
            CloseButtonText = allowContinue ? "취소" : "확인",
            DefaultButton = ContentDialogButton.Primary
        };

    private static void NavigateToPermissionHelp()
    {
        if (App.CurrentWindow is Main.MainWindow window)
            window.NavigateToWebViewPage(PermissionHelpUrl);
    }
}
