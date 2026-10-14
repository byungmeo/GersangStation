using Core;
using Core.Models;
using GersangStation.Diagnostics;
using Microsoft.UI.Xaml.Controls;

namespace GersangStation.Main.Setting;

public sealed partial class AppearanceSettingPage : Page
{
    private bool _synchronizingSelection = true;

    public AppearanceSettingPage()
    {
        InitializeComponent();
        ThemeComboBox.SelectedIndex = (int)AppDataManager.AppThemePreference;
        _synchronizingSelection = false;
    }

    private async void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => await SafeExecution.RunHandledAsync(() =>
        {
            if (_synchronizingSelection || ThemeComboBox.SelectedIndex is < 0 or > 2) return;
            try
            {
                AppDataManager.AppThemePreference = (AppThemePreference)ThemeComboBox.SelectedIndex;
                (App.CurrentWindow as MainWindow)?.ApplyAppTheme(AppDataManager.AppThemePreference);
            }
            finally
            {
                // A failed save must not leave the selector displaying an unsaved preference.
                _synchronizingSelection = true;
                try { ThemeComboBox.SelectedIndex = (int)AppDataManager.AppThemePreference; }
                finally { _synchronizingSelection = false; }
            }
        }, "AppearanceSettingPage.ThemeChanged");
}
