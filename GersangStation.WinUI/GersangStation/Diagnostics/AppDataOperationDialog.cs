using Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace GersangStation.Diagnostics;

/// <summary>
/// AppDataManager 결과를 사용자에게 이해 가능한 안내 문구로 변환해 표시합니다.
/// </summary>
public static class AppDataOperationDialog
{
    /// <summary>
    /// 작업 실패 결과를 파일 정보, 가능한 원인, 해결 방법과 함께 대화상자로 표시합니다.
    /// </summary>
    public static async Task ShowFailureAsync(
        XamlRoot? xamlRoot,
        string title,
        string summary,
        AppDataManager.AppDataOperationResult result)
    {
        xamlRoot ??= (App.CurrentWindow?.Content as FrameworkElement)?.XamlRoot;
        if (xamlRoot is null)
            return;

        if (result.ErrorKind == AppDataManager.AppDataErrorKind.CredentialVault &&
            await CredentialVaultGuidanceDialog.TryShowAsync(xamlRoot, result.Exception))
        {
            return;
        }

        ContentDialog dialog = new()
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = BuildMessage(summary, result),
            CloseButtonText = "확인",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    private static string BuildMessage(string summary, AppDataManager.AppDataOperationResult result)
    {
        string target = GetTargetDisplayName(result.Target);
        string cause = result.ErrorKind switch
        {
            AppDataManager.AppDataErrorKind.Storage =>
                $"데이터 파일({target})에 접근할 수 없었습니다. 다른 프로그램이 파일을 사용 중이거나 쓰기 권한이 부족할 수 있습니다.",
            AppDataManager.AppDataErrorKind.Serialization =>
                $"데이터 파일({target}) 형식이 올바르지 않거나 저장 중 내용이 손상되었습니다.",
            AppDataManager.AppDataErrorKind.CredentialVault =>
                "Windows 자격 증명 저장소에 접근하지 못했습니다. 저장된 비밀번호를 읽거나 쓰는 중 문제가 발생했습니다.",
            AppDataManager.AppDataErrorKind.Validation =>
                "저장하거나 불러오려는 데이터가 현재 규칙과 맞지 않습니다.",
            _ =>
                "데이터 처리 중 예상하지 못한 문제가 발생했습니다."
        };

        string solution = result.ErrorKind switch
        {
            AppDataManager.AppDataErrorKind.Storage =>
                "메모장, 백업 또는 동기화 프로그램, 보안 프로그램이 해당 파일을 열고 있지 않은지 확인한 뒤 다시 시도해주세요.",
            AppDataManager.AppDataErrorKind.Serialization =>
                "앱을 다시 실행한 뒤 다시 시도해주세요. 문제가 계속되면 상세 오류 문구를 복사해 문의해주세요.",
            AppDataManager.AppDataErrorKind.CredentialVault =>
                "Windows 자격 증명 관리자 상태를 확인하거나 앱을 다시 실행한 뒤 다시 시도해주세요.",
            AppDataManager.AppDataErrorKind.Validation =>
                "입력값과 현재 설정을 다시 확인한 뒤 다시 시도해주세요.",
            _ =>
                "같은 문제가 반복되면 앱을 다시 실행한 뒤 다시 시도해주세요."
        };

        string detail = string.IsNullOrWhiteSpace(result.Exception?.Message)
            ? "세부 오류 메시지를 확인할 수 없습니다."
            : result.Exception!.Message;

        return string.Join(
            Environment.NewLine,
            [
                summary,
                string.Empty,
                $"원인: {cause}",
                $"해결 방법: {solution}",
                string.Empty,
                $"파일: {target}",
                $"작업: {GetOperationDisplayName(result.Operation)}",
                $"오류 종류: {GetErrorKindDisplayName(result.ErrorKind)}",
                $"세부 오류: {detail}"
            ]);
    }

    private static string GetTargetDisplayName(string? target)
        => target switch
        {
            "accounts.json" => "accounts.json",
            "client-settings.json" => "client-settings.json",
            "preset-list.json" => "preset-list.json",
            "browser-favorites.json" => "browser-favorites.json",
            null or "" => "알 수 없는 파일",
            _ => target
        };

    private static string GetOperationDisplayName(string? operation)
        => operation switch
        {
            nameof(AppDataManager.SaveAccountsAsync) => "계정 저장",
            nameof(AppDataManager.SaveAccountsWithCredentialsAsync) => "계정 및 비밀번호 저장",
            nameof(AppDataManager.LoadAccountsAsync) => "계정 불러오기",
            "LoadAllServerClientSettingsAsync" => "클라이언트 설정 불러오기",
            nameof(AppDataManager.LoadServerClientSettingsAsync) => "서버별 설정 불러오기",
            "SaveAllServerClientSettingsAsync" => "클라이언트 설정 저장",
            nameof(AppDataManager.SaveServerClientSettingsAsync) => "서버별 설정 저장",
            nameof(AppDataManager.SavePresetListAsync) => "프리셋 저장",
            nameof(AppDataManager.LoadPresetListAsync) => "프리셋 불러오기",
            nameof(AppDataManager.SaveBrowserFavoritesAsync) => "즐겨찾기 저장",
            nameof(AppDataManager.LoadBrowserFavoritesAsync) => "즐겨찾기 불러오기",
            "WriteTextToLocalFolderAsync" => "파일 쓰기",
            "ReadTextFromLocalFolderAsync" => "파일 읽기",
            "NormalizePresetListAgainstAccountsAsync" => "프리셋 정리",
            "SyncAccountsAndPresetsAfterSaveAsync" => "계정 후처리 동기화",
            null or "" => "알 수 없는 작업",
            _ => operation
        };

    private static string GetErrorKindDisplayName(AppDataManager.AppDataErrorKind errorKind)
        => errorKind switch
        {
            AppDataManager.AppDataErrorKind.Storage => "저장소 접근 오류",
            AppDataManager.AppDataErrorKind.Serialization => "데이터 형식 오류",
            AppDataManager.AppDataErrorKind.CredentialVault => "자격 증명 저장소 오류",
            AppDataManager.AppDataErrorKind.Validation => "입력 또는 상태 검증 오류",
            _ => "예상하지 못한 오류"
        };
}
