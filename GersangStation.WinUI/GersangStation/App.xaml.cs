using Microsoft.UI.Xaml;
using System;

namespace GersangStation
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Windows.Storage.ApplicationData.Current.LocalSettings.Values.Clear();

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

            _window = setupWindow;
            _window.Activate();
        }

        private void OpenMainWindow()
        {
            var mainWindow = new MainWindow();
            mainWindow.Closed += CurrentWindow_Closed;

            _window = mainWindow;
            _window.Activate();
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
            if (ReferenceEquals(sender, _window))
            {
                _window = null;
            }
        }
    }
}
