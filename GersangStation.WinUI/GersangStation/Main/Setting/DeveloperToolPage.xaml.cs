using Core;
using Core.Models;
using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Main.Setting;

public sealed partial class DeveloperToolPage : Page
{
    private static readonly TimeSpan TestDelay = TimeSpan.FromMilliseconds(150);
    private Timer? _testTimer;

    public DeveloperToolPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 선택한 서버의 메인 클라이언트 버전을 목표 값으로 강제로 기록합니다.
    /// </summary>
    private async void Button_Downgrade_Click(object sender, RoutedEventArgs e)
    {
        if (ComboBox_Server.SelectedItem is not GameServerOption serverOption)
        {
            await ShowDialogAsync("서버를 선택해 주세요.", "다운그레이드할 서버가 선택되지 않았습니다.");
            return;
        }

        if (!int.TryParse(TextBox_TargetDowngradeVersion.Text?.Trim(), out int targetVersion) || targetVersion < 0)
        {
            await ShowDialogAsync("버전 값이 올바르지 않습니다.", "0 이상의 숫자 버전을 입력해 주세요.");
            return;
        }

        ClientSettings clientSettings = AppDataManager.LoadServerClientSettings(serverOption.Server);
        string installPath = clientSettings.InstallPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(installPath))
        {
            await ShowDialogAsync("메인 클라이언트 경로가 없습니다.", "선택한 서버의 메인 클라이언트 경로를 먼저 설정해 주세요.");
            return;
        }

        string vsnPath = Path.Combine(installPath, "Online", "vsn.dat");

        try
        {
            PatchManager.WriteClientVersion(installPath, targetVersion);

            int? currentVersion = PatchManager.GetCurrentClientVersion(installPath);
            await ShowDialogAsync(
                "다운그레이드 완료",
                $"{GameServerHelper.GetServerDisplayName(serverOption.Server)} 메인 클라이언트 버전을 v{currentVersion ?? targetVersion}로 기록했습니다.\n\n{vsnPath}");
        }
        catch (Exception ex)
        {
            await ShowDialogAsync("다운그레이드 실패", ex.Message);
        }
    }

    /// <summary>
    /// 일반 예외 상세 창을 강제로 표시합니다.
    /// </summary>
    private async void Button_RaiseHandledError_Click(object sender, RoutedEventArgs e)
    {
        await App.ExceptionHandler.ShowRecoverableAsync(
            new InvalidOperationException("DeveloperToolPage에서 수동으로 발생시킨 일반 오류입니다."),
            "DeveloperToolPage.HandledSyncError");
    }

    /// <summary>
    /// 치명적 예외 상세 창을 강제로 표시합니다.
    /// </summary>
    private async void Button_RaiseFatalError_Click(object sender, RoutedEventArgs e)
    {
        await App.ExceptionHandler.HandleFatalUiExceptionAsync(
            new InvalidOperationException("DeveloperToolPage에서 수동으로 발생시킨 치명적 오류입니다."),
            "DeveloperToolPage.HandledFatalError");
    }

    /// <summary>
    /// UI 이벤트 핸들러의 비동기 지점 이후에 처리되지 않은 예외를 발생시킵니다.
    /// </summary>
    private async void Button_RaiseAsyncUiError_Click(object sender, RoutedEventArgs e)
    {
        await Task.Delay(TestDelay);
        throw new InvalidOperationException("async UI 이벤트 핸들러에서 발생한 테스트 예외입니다.");
    }

    /// <summary>
    /// DispatcherQueue로 예약된 UI 콜백에서 처리되지 않은 예외를 발생시킵니다.
    /// </summary>
    private void Button_RaiseDispatcherQueueError_Click(object sender, RoutedEventArgs e)
    {
        _ = DispatcherQueue.TryEnqueueHandled(() =>
        {
            throw new InvalidOperationException("DispatcherQueue 콜백에서 발생한 테스트 예외입니다.");
        }, "DeveloperToolPage.DispatcherQueueError", isFatal: false);
    }

    /// <summary>
    /// 비동기 경로에서 일반 예외 창을 표시합니다.
    /// </summary>
    private async void Button_RaiseHandledAsyncError_Click(object sender, RoutedEventArgs e)
    {
        await Task.Delay(TestDelay);
        await App.ExceptionHandler.ShowRecoverableAsync(
            new IOException("비동기 흐름에서 수동으로 발생시킨 일반 오류입니다."),
            "DeveloperToolPage.HandledAsyncError");
    }

    /// <summary>
    /// Task.Run과 fire-and-forget 조합에서 발생한 예외를 중앙 예외 처리기로 전달합니다.
    /// </summary>
    private void Button_RaiseHandledTaskRunError_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(async () =>
        {
            await Task.Delay(TestDelay);
            throw new InvalidOperationException("Task.Run 백그라운드 작업에서 발생한 테스트 예외입니다.");
        }).FireAndForgetHandled("DeveloperToolPage.TaskRunError", isFatal: false);
    }

    /// <summary>
    /// Timer 콜백에서 발생한 예외를 중앙 예외 처리기로 전달합니다.
    /// </summary>
    private void Button_RaiseHandledTimerError_Click(object sender, RoutedEventArgs e)
    {
        Interlocked.Exchange(
            ref _testTimer,
            SafeExecution.StartHandledTimer(
                () =>
                {
                    throw new TimeoutException("Timer 콜백에서 발생한 테스트 예외입니다.");
                },
                TestDelay,
                "DeveloperToolPage.TimerError",
                isFatal: false))
            ?.Dispose();
    }

    /// <summary>
    /// WinUI 창을 거치지 않고 Win32 fallback 메시지 박스를 바로 테스트합니다.
    /// </summary>
    private async void Button_RaiseNativeFallbackError_Click(object sender, RoutedEventArgs e)
    {
        await App.ExceptionHandler.HandleWithNativeFallbackAsync(
            new InvalidOperationException("네이티브 fallback 경로를 테스트하기 위한 오류입니다."),
            "DeveloperToolPage.NativeFallbackError",
            isFatal: false);
    }

    /// <summary>
    /// 관찰되지 않은 Task 예외 경로를 최대한 재현합니다.
    /// </summary>
    private async void Button_RaiseUnobservedTaskError_Click(object sender, RoutedEventArgs e)
    {
        CreateFaultedBackgroundTask();

        await Task.Delay(TimeSpan.FromMilliseconds(400));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await ShowDialogAsync(
            "백그라운드 Task 오류 예약",
            "관찰되지 않은 Task 예외를 발생시키고 GC를 강제했습니다.\n\n환경에 따라 전역 오류 창이 즉시 나타나거나 약간 늦게 나타날 수 있습니다.");
    }

    /// <summary>
    /// 개발자 도구 작업 결과를 단순 확인 대화상자로 표시합니다.
    /// </summary>
    private async Task ShowDialogAsync(string title, string content)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = "확인",
            DefaultButton = ContentDialogButton.Primary
        };

        await dialog.ShowManagedAsync();
    }

    /// <summary>
    /// 참조를 남기지 않는 백그라운드 Task를 만들어 UnobservedTaskException을 유도합니다.
    /// </summary>
    private static void CreateFaultedBackgroundTask()
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TestDelay);
            throw new ApplicationException("관찰되지 않은 백그라운드 Task 테스트 예외입니다.");
        });
    }

    /// <summary>
    /// 페이지를 벗어날 때 예약된 테스트 타이머를 정리합니다.
    /// </summary>
    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        Interlocked.Exchange(ref _testTimer, null)?.Dispose();
    }
}
