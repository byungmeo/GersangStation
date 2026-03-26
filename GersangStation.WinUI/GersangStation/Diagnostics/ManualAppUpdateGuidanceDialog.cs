using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GersangStation.Diagnostics;

/// <summary>
/// Store 자동 업데이트가 정상 동작하지 않을 때 수동 업데이트 안내 페이지 이동 여부를 묻습니다.
/// </summary>
internal static class ManualAppUpdateGuidanceDialog
{
    /// <summary>
    /// 수동 업데이트 안내 대화상자를 표시하고 안내 페이지 오픈 여부를 반환합니다.
    /// </summary>
    public static async Task<bool> ShowAsync(
        XamlRoot? xamlRoot,
        StoreUpdateManualFallbackContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        xamlRoot ??= (App.CurrentWindow?.Content as FrameworkElement)?.XamlRoot;
        if (xamlRoot is null)
            return false;

        AppContentDialog dialog = new()
        {
            XamlRoot = xamlRoot,
            Title = "수동 업데이트 안내",
            Content = BuildMessage(context),
            PrimaryButtonText = "안내 페이지 열기",
            CloseButtonText = "닫기",
            DefaultButton = ContentDialogButton.Primary
        };

        return await dialog.ShowManagedAsync() == ContentDialogResult.Primary;
    }

    private static string BuildMessage(StoreUpdateManualFallbackContext context)
    {
        string summary = context.Reason switch
        {
            StoreUpdateManualFallbackReason.NoStoreUpdatesReported =>
                "현재 버전 이후 필수 업데이트가 존재하지만, Microsoft Store를 통해 업데이트를 불러오는데 실패하였습니다.",
            StoreUpdateManualFallbackReason.StoreCheckFailed =>
                "현재 버전 이후 필수 업데이트가 존재하지만, Microsoft Store를 통해 업데이트를 불러오는데 실패하였습니다.",
            StoreUpdateManualFallbackReason.StoreInstallFailed =>
                "Microsoft Store에서 필수 업데이트를 설치하던 중 실패하였습니다.",
            _ =>
                "현재 버전 이후 필수 업데이트가 확인되었습니다."
        };

        string requiredVersions = context.RequiredVersions.Count == 0
            ? "확인된 필수 업데이트 버전을 표시할 수 없습니다."
            : $"확인된 필수 업데이트 버전: v{string.Join(", v", context.RequiredVersions)}";

        return string.Join(
            Environment.NewLine,
            [
                summary,
                string.Empty,
                requiredVersions,
                "수동 업데이트 안내를 통해 직접 업데이트를 진행해주세요."
            ]);
    }
}

/// <summary>
/// Store 자동 업데이트 fallback이 필요한 시점을 구분합니다.
/// </summary>
internal enum StoreUpdateManualFallbackReason
{
    NoStoreUpdatesReported,
    StoreCheckFailed,
    StoreInstallFailed
}

/// <summary>
/// 수동 업데이트 안내에 필요한 이유와 필수 버전 목록을 묶습니다.
/// </summary>
internal sealed record StoreUpdateManualFallbackContext(
    StoreUpdateManualFallbackReason Reason,
    IReadOnlyList<string> RequiredVersions);
