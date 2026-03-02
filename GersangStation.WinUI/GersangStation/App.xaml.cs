using Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
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

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            Debug.WriteLine($"PFN: {Package.Current.Id.FamilyName}");
            Debug.WriteLine($"LocalFolder Path: {ApplicationData.Current.LocalFolder.Path}");
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Windows.Storage.ApplicationData.Current.LocalSettings.Values.Clear();

            if (AppDataManager.IsSetupCompleted)
            {
                OpenMainWindow();
                return;
            }

            OpenSetupWindow();
        }

        private void OpenSetupWindow()
        {
            var setupWindow = new Setup.SetupWindow();
            setupWindow.SetupCompleted += SetupWindow_SetupCompleted;
            setupWindow.Closed += CurrentWindow_Closed;

            CurrentWindow = setupWindow;
            CenterAndActivateWindow(CurrentWindow);
        }

        private void OpenMainWindow()
        {
            var mainWindow = new Main.MainWindow();
            mainWindow.Closed += CurrentWindow_Closed;

            CurrentWindow = mainWindow;
            CenterAndActivateWindow(CurrentWindow);
        }

        private static void CenterAndActivateWindow(Window window)
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
                presenter.PreferredMaximumWidth = 1600;
                presenter.PreferredMaximumHeight = 900;
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = true;
            }

            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

            window.Activate();
        }

        private void SetupWindow_SetupCompleted(object? sender, EventArgs e)
        {
            AppDataManager.IsSetupCompleted = true;

            // 1) 먼저 MainWindow 열기
            OpenMainWindow();

            // 2) 그 다음 SetupWindow 닫기
            if (sender is Setup.SetupWindow setupWindow)
            {
                setupWindow.SetupCompleted -= SetupWindow_SetupCompleted;
                setupWindow.Close();
            }
        }

        private void CurrentWindow_Closed(object sender, WindowEventArgs args)
        {
            if (ReferenceEquals(sender, CurrentWindow))
            {
                CurrentWindow = null;
            }
        }
    }
}
