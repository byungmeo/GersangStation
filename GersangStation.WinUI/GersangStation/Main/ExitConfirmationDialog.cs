using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace GersangStation.Main;

/// <summary>
/// 앱 종료 여부를 묻는 공통 대화 상자를 표시합니다.
/// </summary>
internal static class ExitConfirmationDialog
{
    /// <summary>
    /// 현재 XamlRoot 위에 종료 확인 대화 상자를 표시합니다.
    /// </summary>
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = xamlRoot,
            Title = "프로그램 종료",
            Content = "정말로 종료하시겠습니까?",
            PrimaryButtonText = "종료",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close
        };

        return await dialog.ShowManagedAsync() == ContentDialogResult.Primary;
    }
}
