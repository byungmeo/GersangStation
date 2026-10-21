using Core.Models;
using GersangStation.Diagnostics;
using GersangStation.Main.Setting;
using GersangStation.Services;
using System;
using System.Threading.Tasks;
using Windows.System;

namespace GersangStation.Main;

public sealed partial class MainWindow
{
    internal void ClearGameLaunchWarnings() => Sidebar.ClearLaunchWarnings();

    internal void ShowGameLaunchWarning(int clientIndex, string title, string message)
        => Sidebar.ShowLaunchWarning(clientIndex, title, message);

    internal void ShowBrowserLoginFailure(string message, bool retryBlocked)
    {
        if (BrowserFrame.Content is WebViewPage page)
            page.ShowBrowserLoginFailure(message, retryBlocked);
    }

    internal void ShowPhoneVerificationGuide(GameLaunchAttempt? attempt)
    {
        if (attempt is null)
        {
            if (BrowserFrame.Content is WebViewPage page) page.ShowBrowserPhoneVerificationGuide();
            return;
        }
        if (!GameLaunchCoordinator.IsCurrentAttempt(attempt)) return;
        Sidebar.ShowLaunchNotice(attempt.ClientIndex, new("휴대폰 본인 인증 필요",
            "거상 웹페이지에서 휴대폰 본인 인증을 완료해 주세요.", GameLaunchNoticeSeverity.Caution,
            new GameLaunchNoticeAction("본인 인증 하기", () =>
            {
                NavigateToWebViewPage();
                return Task.CompletedTask;
            })));
    }

    internal void ShowGameLaunchSettingsNotice(int clientIndex, string title, string message,
        SettingSection section, GameServer? server = null, string actionLabel = "설정 열기", string? actionLeadingText = null)
        => Sidebar.ShowLaunchNotice(clientIndex, new(title, message, GameLaunchNoticeSeverity.Caution,
            SettingsAction(actionLabel, section, server) with { LeadingText = actionLeadingText }));

    private GameLaunchNoticeAction SettingsAction(string label, SettingSection section, GameServer? server = null)
        => new(label, () =>
        {
            object? parameter = server is { } selected
                ? section == SettingSection.GamePatch
                    ? new GamePatchSettingNavigationParameter { Server = selected }
                    : new GameServerSettingNavigationParameter { Server = selected }
                : null;
            NavigateToSettingPage(section, parameter);
            return Task.CompletedTask;
        });

    internal void ReportGameLaunchCredentialFailure(int clientIndex, Exception exception, string context)
    {
        App.ExceptionHandler.RecordRecoverable(exception, context);
        string message = CredentialVaultGuidanceDialog.IsServiceUnavailable(exception)
            ? "Windows 비밀번호 저장소에 접근할 수 없습니다."
            : "저장된 비밀번호를 불러올 수 없습니다.";
        Sidebar.ShowLaunchNotice(clientIndex, new("비밀번호 불러오기 실패", message,
            GameLaunchNoticeSeverity.Critical,
            SettingsAction("계정 설정", SettingSection.Account),
            new("해결 방법 보기", async () =>
            {
                if (!App.LinkManager.TryGetUrl(AppLinkKeys.HelpCredentialVault, out string url) ||
                    !await Launcher.LaunchUriAsync(new Uri(url)))
                    throw new InvalidOperationException("자격 증명 관리자 도움말을 외부 브라우저에서 열지 못했습니다.");
            })));
    }

    internal void ShowMissingGameLaunchCredential(int clientIndex, string accountName)
        => ShowGameLaunchSettingsNotice(clientIndex, "비밀번호가 필요합니다",
            $"'{accountName}' 계정에 저장된 비밀번호가 없습니다.",
            SettingSection.Account, actionLabel: "계정 설정");

    internal void ShowGameLaunchLoginFailure(GameLaunchAttempt attempt, string message, bool retryBlocked = false)
    {
        // 실패 처리로 브라우저의 캐시가 지워져도, 원래 시도에 해당하는 슬롯만 안내합니다.
        if (!GameLaunchCoordinator.IsCurrentAttempt(attempt)) return;
        GameLaunchNoticeAction browserAction = new("브라우저에서 확인", () =>
        {
            NavigateToWebViewPage();
            return Task.CompletedTask;
        });
        Sidebar.ShowLaunchNotice(attempt.ClientIndex, new("로그인 실패", message,
            retryBlocked ? GameLaunchNoticeSeverity.Caution : GameLaunchNoticeSeverity.Critical,
            retryBlocked ? [SettingsAction("계정 설정", SettingSection.Account), browserAction] : [browserAction]));
    }
}
