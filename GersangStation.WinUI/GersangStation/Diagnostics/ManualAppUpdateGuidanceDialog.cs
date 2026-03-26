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
                "Microsoft Store에서 업데이트 가능한 목록이 없다고 응답했지만, 원격 버전 매니페스트 기준으로 현재 버전 이후 필수 업데이트가 확인되었습니다.",
            StoreUpdateManualFallbackReason.StoreCheckFailed =>
                "Microsoft Store 업데이트 확인을 완료하지 못했고, 원격 버전 매니페스트 기준으로 현재 버전 이후 필수 업데이트가 확인되었습니다.",
            StoreUpdateManualFallbackReason.StoreInstallFailed =>
                "Microsoft Store 업데이트 설치를 완료하지 못했고, 원격 버전 매니페스트 기준으로 현재 버전 이후 필수 업데이트가 확인되었습니다.",
            _ =>
                "원격 버전 매니페스트 기준으로 현재 버전 이후 필수 업데이트가 확인되었습니다."
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
                "수동 업데이트 안내 페이지를 열어 직접 업데이트를 진행해주세요."
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
