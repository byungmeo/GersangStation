using GersangStation.Main;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GersangStation.Diagnostics;

/// <summary>
/// Windows 자격 증명 관리자 서비스 비정상 상태를 사용자 안내 대화상자로 표시합니다.
/// </summary>
public static class CredentialVaultGuidanceDialog
{
    private const int HrServiceCannotAcceptControl = unchecked((int)0x80070425);

    /// <summary>
    /// 예외 체인에 Credential Manager 서비스 제어 불가 HRESULT가 있는지 확인합니다.
    /// </summary>
    public static bool IsServiceUnavailable(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is COMException comException && comException.ErrorCode == HrServiceCannotAcceptControl)
                return true;

            if (current.HResult == HrServiceCannotAcceptControl)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 해당 예외가 서비스 비정상 안내 대상이면 안내 대화상자를 표시하고 true를 반환합니다.
    /// </summary>
    public static async Task<bool> TryShowAsync(XamlRoot? xamlRoot, Exception? exception)
    {
        if (!IsServiceUnavailable(exception))
            return false;

        await ShowAsync(xamlRoot);
        return true;
    }

    /// <summary>
    /// 해결 방법 이동 버튼이 포함된 자격 증명 관리자 안내 대화상자를 표시합니다.
    /// </summary>
    public static async Task ShowAsync(XamlRoot? xamlRoot)
    {
        xamlRoot ??= (App.CurrentWindow?.Content as FrameworkElement)?.XamlRoot;
        if (xamlRoot is null)
            return;

        ContentDialog dialog = new()
        {
            XamlRoot = xamlRoot,
            Title = "자격 증명 관리자 서비스 오류",
            Content =
                "윈도우 자격 증명 관리자 서비스 접근에 실패하여 비밀번호 관련 작업에 실패하였습니다.\n\n" +
                "해결 방법을 확인하시겠습니까?",
            PrimaryButtonText = "해결 방법 보기",
            CloseButtonText = "확인",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowManagedAsync() == ContentDialogResult.Primary &&
            App.CurrentWindow is MainWindow window)
        {
            window.NavigateToWebViewPageByLinkKey(AppLinkKeys.HelpCredentialVault);
        }
    }
}
