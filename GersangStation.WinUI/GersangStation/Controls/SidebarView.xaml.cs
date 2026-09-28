using Core;
using GersangStation.Main;
using GersangStation.Main.Setting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace GersangStation.Controls;

public sealed class SidebarSettingRequestedEventArgs(SettingSection section) : EventArgs
{
    public SettingSection Section { get; } = section;
}

public sealed partial class SidebarView : UserControl
{
    private StationPage? _homePage;
    private bool _suppressSelection;

    public event EventHandler? HomeRequested;
    public event EventHandler? BrowserRequested;
    public event EventHandler<SidebarSettingRequestedEventArgs>? SettingRequested;

    public FrameworkElement CurrentAppVersionTarget => CurrentAppVersionTextBlock;

    public SidebarView() => InitializeComponent();

    public void SetHomePage(StationPage homePage)
    {
        _homePage = homePage;
        DataContext = homePage;
    }

    public void ShowControl(bool browserSelected = false)
    {
        SidebarControlPanel.Visibility = Visibility.Visible;
        SidebarSettingPanel.Visibility = Visibility.Collapsed;
        _suppressSelection = true;
        NavListView.SelectedIndex = browserSelected ? 1 : 0;
        _suppressSelection = false;
    }

    public void ShowSettings() 
    {
        SidebarControlPanel.Visibility = Visibility.Collapsed;
        SidebarSettingPanel.Visibility = Visibility.Visible;
        DeveloperSettingsListViewItem.Visibility = AppDataManager.IsDeveloperToolEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void NavListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection || NavListView.SelectedItem is not ListViewItem item)
            return;
        if (item.Tag as string == "Browser") BrowserRequested?.Invoke(this, EventArgs.Empty);
        else HomeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SettingPageButton_Click(object sender, RoutedEventArgs e)
        => RequestSetting(SettingSection.Account);

    private void ReturnToControlButton_Click(object sender, RoutedEventArgs e)
        => HomeRequested?.Invoke(this, EventArgs.Empty);

    private void SettingNavListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SettingNavListView.SelectedItem is not ListViewItem { Tag: string tag }) return;
        SettingSection? section = tag switch
        {
            "Account" => SettingSection.Account,
            "GameInstall" => SettingSection.GameInstall,
            "InstallPath" => SettingSection.InstallPath,
            "GamePatch" => SettingSection.GamePatch,
            "GameExtension" => SettingSection.GameExtension,
            "General" => SettingSection.General,
            "Notification" => SettingSection.Notification,
            "Appearance" => SettingSection.Appearance,
            "Browser" => SettingSection.BrowserSettings,
            "Developer" => SettingSection.DeveloperTool,
            "Advanced" => SettingSection.Advanced,
            "ProgramInfo" => SettingSection.ProgramInfo,
            "OpenSourceLicense" => SettingSection.OpenSourceLicense,
            _ => null
        };
        if (section is SettingSection selected) RequestSetting(selected);
    }

    private async void ServerSelectRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => await InvokeHomeAsync(page => page.RefreshServerAsync());
    private async void RefreshVersionButton_Click(object sender, RoutedEventArgs e)
        => await InvokeHomeAsync(page => page.RefreshServerAsync());
    private void PatchButton_Click(object sender, RoutedEventArgs e)
        => _homePage?.NavigateToPatchSetting();
    private void ClientSettingPageNavigateButton_Click(object sender, RoutedEventArgs e)
        => _homePage?.NavigateToInstallPathSetting();
    private async void AppUpdateInstallButton_Click(object sender, RoutedEventArgs e)
        => await InvokeHomeAsync(page => page.InstallStoreUpdateAsync());
    private async void Account1ExecuteButton_Click(object sender, RoutedEventArgs e)
        => await InvokeHomeAsync(page => page.ExecuteClientAsync(0));
    private async void Account2ExecuteButton_Click(object sender, RoutedEventArgs e)
        => await InvokeHomeAsync(page => page.ExecuteClientAsync(1));
    private async void Account3ExecuteButton_Click(object sender, RoutedEventArgs e)
        => await InvokeHomeAsync(page => page.ExecuteClientAsync(2));

    private async Task InvokeHomeAsync(Func<StationPage, Task> operation)
    {
        if (_homePage is not null) await operation(_homePage);
    }

    private void RequestSetting(SettingSection section)
        => SettingRequested?.Invoke(this, new SidebarSettingRequestedEventArgs(section));
}
