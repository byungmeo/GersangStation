using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Package = Windows.ApplicationModel.Package;
using PackageVersion = Windows.ApplicationModel.PackageVersion;

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

        // 보조 인스턴스는 이전 오류 안내 표식을 읽거나 변경하지 않습니다.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledProcessException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        try
        {
            InitializeFatalErrorHandling();
            Application.Start(static _ =>
            {
                try
                {
                    var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                    SynchronizationContext.SetSynchronizationContext(context);
                    new App();
                }
                catch (Exception ex)
                {
                    FatalErrorHandler.Current.Terminate(ex, "Program.Application.Start");
                }
            });
            FatalErrorHandler.Current.CompleteSession();
        }
        catch (Exception ex)
        {
            FatalErrorHandler.Current.Terminate(ex, "Program.Main");
        }
    }

    private static void InitializeFatalErrorHandling()
    {
        string localFolder;
        try { localFolder = ApplicationData.Current.LocalFolder.Path; }
        catch
        {
            localFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GersangStation");
        }

        string? version = null;
        try
        {
            PackageVersion packageVersion = Package.Current.Id.Version;
            version = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}.{packageVersion.Revision}";
        }
        catch { /* 패키지 정보가 없어도 치명적 오류 경계는 사용할 수 있어야 합니다. */ }

        FatalErrorHandler.Current.Initialize(localFolder, version);
    }

    private static void OnUnhandledProcessException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.IsTerminating)
        {
            Exception exception = e.ExceptionObject as Exception ?? new Exception("A non-Exception object was thrown.");
            FatalErrorHandler.Current.RecordProcessFailure(exception, "AppDomain.CurrentDomain.UnhandledException");
        }
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        if (Environment.ExitCode == 0)
            FatalErrorHandler.Current.CompleteSession();
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
