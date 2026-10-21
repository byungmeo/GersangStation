using Core;
using Core.Models;
using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GersangStation.Main.Setting;

public sealed partial class DeveloperToolPage : Page
{
    private bool _isDowngrading;
    private bool _resultTipClosing;
    private (string Title, string Message)? _pendingResult;

    public DeveloperToolPage()
    {
        InitializeComponent();
        Unloaded += (_, _) =>
        {
            _pendingResult = null;
            try { DowngradeResultTip.IsOpen = false; }
            catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "DeveloperToolPage.CloseResult"); }
        };
    }

    /// <summary>
    /// 선택한 서버의 메인 클라이언트 버전을 목표 값으로 강제로 기록합니다.
    /// </summary>
    private async void Button_Downgrade_Click(object sender, RoutedEventArgs e)
        => await SafeExecution.RunHandledAsync(DowngradeAsync, "DeveloperToolPage.Downgrade");

    private async Task DowngradeAsync()
    {
        if (_isDowngrading) return;
        if (ComboBox_Server.SelectedItem is not GameServerOption serverOption)
        {
            ShowDowngradeResult("서버 미선택", "변경할 서버를 선택해 주세요.");
            return;
        }

        if (!int.TryParse(TextBox_TargetDowngradeVersion.Text?.Trim(), out int targetVersion) || targetVersion < 0)
        {
            ShowDowngradeResult("잘못된 버전", "0 이상의 숫자를 입력해 주세요.");
            return;
        }

        _isDowngrading = true;
        try
        {
            Button_Downgrade.IsEnabled = false;
            var (clientSettings, result) = await AppDataManager.LoadServerClientSettingsAsync(serverOption.Server);
            if (!result.Success)
                throw new IOException("다운그레이드용 서버 설정을 불러오지 못했습니다.", result.Exception);
            if (!IsLoaded) return;
            string installPath = clientSettings.InstallPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(installPath))
            {
                ShowDowngradeResult("설치 경로 미설정", "게임 설치 경로가 없습니다.");
                return;
            }
            PatchManager.WriteClientVersion(installPath, targetVersion);
            ShowDowngradeResult("다운그레이드 완료",
                $"{GameServerHelper.GetServerDisplayName(serverOption.Server)} 게임 버전을 v{targetVersion}로 변경했습니다.");
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
                ex = new IOException("게임 버전 변경이 예기치 않게 중단되었습니다.", ex);
            App.ExceptionHandler.RecordRecoverable(ex, "DeveloperToolPage.Downgrade");
            ShowDowngradeResult("다운그레이드 실패", "게임 버전을 변경하지 못했습니다.");
        }
        finally
        {
            _isDowngrading = false;
            Button_Downgrade.IsEnabled = true;
        }
    }

    /// <summary>
    /// 검증 및 작업 결과를 실행 버튼 위치에 표시합니다.
    /// </summary>
    private void ShowDowngradeResult(string title, string content)
    {
        if (!IsLoaded) return;
        _pendingResult = (title, content);
        OpenPendingDowngradeResult();
    }

    private void OpenPendingDowngradeResult()
    {
        if (_resultTipClosing || !IsLoaded || _pendingResult is not { } result) return;
        _pendingResult = null;
        DowngradeResultTip.Title = result.Title;
        DowngradeResultTip.Subtitle = result.Message;
        DowngradeResultTip.IsOpen = true;
    }

    private void DowngradeResultTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args) => _resultTipClosing = true;

    private void DowngradeResultTip_Closed(TeachingTip sender, TeachingTipClosedEventArgs args)
    {
        _resultTipClosing = false;
        if (_pendingResult is null) return;
        try
        {
            if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                try { OpenPendingDowngradeResult(); }
                catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "DeveloperToolPage.ReopenResult"); }
            }))
            {
                _pendingResult = null;
                App.ExceptionHandler.RecordRecoverable(new InvalidOperationException("개발자 도구 안내 재표시를 UI에 예약하지 못했습니다."),
                    "DeveloperToolPage.ReopenResult");
            }
        }
        catch (Exception ex)
        {
            _pendingResult = null;
            App.ExceptionHandler.RecordRecoverable(ex, "DeveloperToolPage.ReopenResult");
        }
    }
}
