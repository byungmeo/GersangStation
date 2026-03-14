using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation;

/// <summary>
/// WinUI 앱 시작 전 단일 인스턴스 리디렉션과 XAML 부트스트랩을 담당합니다.
/// </summary>
public static class Program
{
    private const string MainInstanceKey = "main";

    /// <summary>
    /// WinUI 애플리케이션을 시작하고, 보조 인스턴스는 기존 인스턴스로 리디렉션합니다.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (DecideRedirection())
            return;

        Application.Start(static _ =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }

    /// <summary>
    /// 현재 프로세스가 메인 인스턴스인지 확인하고 필요하면 활성화를 기존 인스턴스로 전달합니다.
    /// </summary>
    private static bool DecideRedirection()
    {
        AppActivationArguments activationArguments = AppInstance.GetCurrent().GetActivatedEventArgs();

        AppInstance mainInstance = AppInstance.FindOrRegisterForKey(MainInstanceKey);
        if (mainInstance.IsCurrent)
        {
            mainInstance.Activated += OnMainInstanceActivated;
            return false;
        }

        using Semaphore redirectSemaphore = new(initialCount: 0, maximumCount: 1);
        Exception? redirectException = null;

        Task.Run(() =>
        {
            try
            {
                mainInstance.RedirectActivationToAsync(activationArguments).AsTask().Wait();
            }
            catch (Exception ex)
            {
                redirectException = ex;
            }
            finally
            {
                redirectSemaphore.Release();
            }
        });

        redirectSemaphore.WaitOne();
        if (redirectException is not null)
            throw new InvalidOperationException("기존 앱 인스턴스로 활성화를 전달하지 못했습니다.", redirectException);

        return true;
    }

    /// <summary>
    /// 기존 인스턴스가 다시 활성화되면 메인 창을 복원하고 전면에 표시합니다.
    /// </summary>
    private static void OnMainInstanceActivated(object? sender, AppActivationArguments args)
    {
        if (App.UiDispatcherQueue is null)
            return;

        _ = App.UiDispatcherQueue.TryEnqueueHandled(
            App.BringCurrentWindowToForeground,
            "Program.OnMainInstanceActivated");
    }
}
