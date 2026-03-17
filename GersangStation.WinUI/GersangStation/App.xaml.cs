using GersangStation.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.ApplicationModel.WindowsAppRuntime;
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace GersangStation
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static Window? CurrentWindow { get; private set; }
        public static Microsoft.UI.Dispatching.DispatcherQueue? UiDispatcherQueue { get; private set; }
        public static AppExceptionHandler ExceptionHandler { get; } = new();
        public static bool IsRunningAsAdministrator { get; private set; }
        public static bool IsWindowsAppRuntimeDeploymentReady { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            IsRunningAsAdministrator = DetectIsRunningAsAdministrator();
            UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            RegisterGlobalExceptionHandlers();
            Debug.WriteLine($"PFN: {Package.Current.Id.FamilyName}");
            Debug.WriteLine($"LocalFolder Path: {ApplicationData.Current.LocalFolder.Path}");
        }

        /// <summary>
        /// 현재 프로세스가 관리자 권한으로 실행 중인지 판별합니다.
        /// </summary>
        private static bool DetectIsRunningAsAdministrator()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                OpenMainWindow();
                StartWindowsAppRuntimeDeploymentInitialization();
            }
            catch (Exception ex)
            {
                await ExceptionHandler.HandleFatalUiExceptionAsync(ex, "App.OnLaunched");
            }
        }

        /// <summary>
        /// Main/Singleton 패키지가 필요한 Windows App SDK 기능을 위해 배포 상태를 백그라운드에서 준비합니다.
        /// </summary>
        private static void StartWindowsAppRuntimeDeploymentInitialization()
        {
            EnsureWindowsAppRuntimeDeploymentReadyAsync()
                .FireAndForgetHandled("App.StartWindowsAppRuntimeDeploymentInitialization");
        }

        /// <summary>
        /// Windows App SDK 추가 패키지의 준비 상태를 확인하고 필요하면 명시적으로 초기화합니다.
        /// </summary>
        private static async Task EnsureWindowsAppRuntimeDeploymentReadyAsync()
        {
            try
            {
                DeploymentResult statusResult = DeploymentManager.GetStatus();
                if (statusResult.Status is DeploymentStatus.Ok)
                {
                    IsWindowsAppRuntimeDeploymentReady = true;
                    return;
                }

                DeploymentResult initializeResult = DeploymentManager.Initialize();
                if (initializeResult.Status is DeploymentStatus.Ok)
                {
                    IsWindowsAppRuntimeDeploymentReady = true;
                    return;
                }

                IsWindowsAppRuntimeDeploymentReady = false;
                await ExceptionHandler.ShowRecoverableAsync(
                    CreateDeploymentInitializationException(
                        initializeResult,
                        "Windows App SDK 추가 런타임 패키지를 준비하지 못했습니다. 알림 기능이 제한될 수 있습니다."),
                    "App.EnsureWindowsAppRuntimeDeploymentReadyAsync").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                IsWindowsAppRuntimeDeploymentReady = false;
                await ExceptionHandler.ShowRecoverableAsync(
                    new InvalidOperationException(
                        "Windows App SDK 추가 런타임 패키지 초기화 중 예외가 발생했습니다. 알림 기능이 제한될 수 있습니다.",
                        ex),
                    "App.EnsureWindowsAppRuntimeDeploymentReadyAsync").ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Deployment API의 상태와 확장 오류를 사용자에게 전달할 예외 객체로 정리합니다.
        /// </summary>
        private static Exception CreateDeploymentInitializationException(DeploymentResult result, string message)
        {
            string detail = $"{message} 상태: {result.Status}.";
            return result.ExtendedError is null
                ? new InvalidOperationException(detail)
                : new InvalidOperationException(detail, result.ExtendedError);
        }

        private void OpenMainWindow()
        {
            var mainWindow = new Main.MainWindow();
            mainWindow.Closed += CurrentWindow_Closed;

            CurrentWindow = mainWindow;
            PrepareMainWindow(CurrentWindow);
            BringCurrentWindowToForeground();
        }

        /// <summary>
        /// 메인 창 최초 표시 전에 크기와 위치, 프레젠터 구성을 적용합니다.
        /// </summary>
        private static void PrepareMainWindow(Window window)
        {
            // 타이틀바 더블 클릭으로 인한 최대화 방지
            window.PreventMaximizeOnTitleBarDoubleClick();

            AppWindow appWindow = window.AppWindow;
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea is not null)
            {
                int centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
                int centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
                appWindow.Move(new Windows.Graphics.PointInt32(centeredX, centeredY));
            }

            /*
            HD:             1280 × 720
            HD+:            1600 × 900
            FHD (Full HD):  1920 × 1080
            QHD (Quad HD):  2560 × 1440
            4K UHD:         3840 × 2160
            8K UHD:         7680 × 4320 
            */
            appWindow.Resize(new Windows.Graphics.SizeInt32(1600, 900));

            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = 1600;
                presenter.PreferredMinimumHeight = 900;
                // presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = true;
            }

            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        }

        /// <summary>
        /// 현재 메인 창을 복원하고 전면에 표시합니다.
        /// </summary>
        public static void BringCurrentWindowToForeground()
        {
            if (CurrentWindow is null)
                return;

            if (CurrentWindow is Main.MainWindow mainWindow)
                mainWindow.EnsureWindowVisible();

            if (CurrentWindow.AppWindow.Presenter is OverlappedPresenter presenter &&
                presenter.State == OverlappedPresenterState.Minimized)
            {
                presenter.Restore();
            }

            CurrentWindow.Activate();
        }

        private void CurrentWindow_Closed(object sender, WindowEventArgs args)
        {
            if (ReferenceEquals(sender, CurrentWindow))
                CurrentWindow = null;
        }

        /// <summary>
        /// 앱 전역 예외 이벤트를 등록합니다.
        /// </summary>
        private void RegisterGlobalExceptionHandlers()
        {
            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
        }

        /// <summary>
        /// WinUI UI 스레드에서 처리되지 않은 예외를 마지막 crash UI로 표시한 뒤 종료합니다.
        /// </summary>
        private async void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            // 창을 보여줄 마지막 기회를 확보하기 위해 handled로 전환하되, 복구 시도는 하지 않습니다.
            e.Handled = true;
            await ExceptionHandler.HandleFatalUiExceptionAsync(
                e.Exception,
                "Microsoft.UI.Xaml.Application.UnhandledException");
        }

        /// <summary>
        /// 앱 도메인 수준의 처리되지 않은 예외는 WinUI를 거치지 않고 저수준 fallback으로만 처리합니다.
        /// </summary>
        private void OnCurrentDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception
                ?? new Exception($"Non-Exception object was thrown: {e.ExceptionObject}");

            if (e.IsTerminating)
            {
                ExceptionHandler.HandleFatalProcessException(
                    exception,
                    "AppDomain.CurrentDomain.UnhandledException");
                return;
            }

            _ = ExceptionHandler.ShowRecoverableAsync(
                exception,
                "AppDomain.CurrentDomain.UnhandledException");
        }

        /// <summary>
        /// 관찰되지 않은 Task 예외를 공통 처리기로 전달하고 GC 종료 크래시를 방지합니다.
        /// </summary>
        private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            _ = ExceptionHandler.ShowRecoverableAsync(
                e.Exception,
                "TaskScheduler.UnobservedTaskException");
        }
    }
}
