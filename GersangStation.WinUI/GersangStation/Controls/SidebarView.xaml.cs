using Core;
using Core.Models;
using GersangStation.Main;
using GersangStation.Main.Setting;
using GersangStation.Diagnostics;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GersangStation.Controls;

public sealed class SidebarSettingRequestedEventArgs(SettingSection section) : EventArgs
{
    public SettingSection Section { get; } = section;
}

public sealed class SidebarBrowserLoginRequestedEventArgs(Account account) : EventArgs
{
    public Account Account { get; } = account;
}

public sealed partial class SidebarView : UserControl
{
    private StationPage? _homePage;
    private bool _suppressSelection;
    private Action? _multiClientResultAction;

    public event EventHandler? HomeRequested;
    public event EventHandler? BrowserRequested;
    public event EventHandler<SidebarBrowserLoginRequestedEventArgs>? BrowserLoginRequested;
    public event EventHandler<SidebarSettingRequestedEventArgs>? SettingRequested;

    public FrameworkElement CurrentAppVersionTarget => CurrentAppVersionTextBlock;
    public GameLaunchCoordinator? Launches { get; private set; }

    public SidebarView() => InitializeComponent();

    public void SetLaunchCoordinator(GameLaunchCoordinator coordinator)
    {
        Launches = coordinator;
        Bindings.Update();
    }

    public void SetHomePage(StationPage homePage)
    {
        if (_homePage is not null)
            _homePage.MultiClientCreationCompleted -= HomePage_MultiClientCreationCompleted;
        _homePage = homePage;
        _homePage.MultiClientCreationCompleted += HomePage_MultiClientCreationCompleted;
        DataContext = homePage;
    }

    private void HomePage_MultiClientCreationCompleted(object? sender, MultiClientCreationCompletedEventArgs e)
    {
        // The result belongs to the always-loaded sidebar, not the possibly hidden home page.
        _multiClientResultAction = null;
        MultiClientResultTeachingTip.ActionButtonContent = null;
        MultiClientResultTeachingTip.IconSource = e.Success || e.Canceled ? null : new FontIconSource
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Glyph = "\uEA39"
        };
        MultiClientResultTeachingTip.Title = e.Canceled ? "다클라 생성 취소"
            : e.Success ? "다클라 생성 완료" : "다클라 생성 실패";
        MultiClientResultTeachingTip.Subtitle = $"{GameServerHelper.GetServerDisplayName(e.Server)}\n{e.Message}";

        if (!e.Success && !e.Canceled)
        {
            GameClientHelper.MultiClientSymbolicDirectoryConflictException? conflict = null;
            for (Exception? current = e.Exception; current is not null; current = current.InnerException)
            {
                if (current is GameClientHelper.MultiClientSymbolicDirectoryConflictException matched)
                {
                    conflict = matched;
                    break;
                }
            }

            if (conflict is not null)
            {
                string? parentPath = Path.GetDirectoryName(conflict.DestinationPath);
                MultiClientResultTeachingTip.Subtitle =
                    $"{conflict.DestinationPath}\n위 폴더를 삭제하신 후 다시 다클라를 생성해주세요.";
                if (!string.IsNullOrWhiteSpace(parentPath))
                {
                    MultiClientResultTeachingTip.ActionButtonContent = "상위 폴더 열기";
                    _multiClientResultAction = () =>
                    {
                        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                        start.ArgumentList.Add(parentPath);
                        Process.Start(start);
                    };
                }
            }
            else if (PathPermissionDialog.IsPermissionFailure(e.Exception))
            {
                MultiClientResultTeachingTip.ActionButtonContent = "해결 방법 확인하기";
                _multiClientResultAction = () =>
                {
                    if (App.CurrentWindow is MainWindow window)
                        window.NavigateToWebViewPageByLinkKey(AppLinkKeys.HelpPermissionMultiClient);
                };
            }
        }

        MultiClientResultTeachingTip.IsOpen = true;
    }

    private async void MultiClientResultTeachingTip_ActionButtonClick(TeachingTip sender, object args)
    {
        Action? action = _multiClientResultAction;
        sender.IsOpen = false;
        if (action is not null)
            await SafeExecution.RunHandledAsync(action, "SidebarView.MultiClientResultAction");
    }

    public void ShowControl(bool browserSelected = false)
    {
        SidebarControlPanel.Visibility = Visibility.Visible;
        SidebarSettingPanel.Visibility = Visibility.Collapsed;
        _suppressSelection = true;
        NavListView.SelectedIndex = browserSelected ? 1 : 0;
        _suppressSelection = false;
    }

    public void ShowSettings(SettingSection? section = null)
    {
        SidebarControlPanel.Visibility = Visibility.Collapsed;
        SidebarSettingPanel.Visibility = Visibility.Visible;
        DeveloperSettingsListViewItem.Visibility = AppDataManager.IsDeveloperToolEnabled
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (section is SettingSection selected)
        {
            string tag = selected switch
            {
                SettingSection.BrowserSettings => "Browser",
                SettingSection.DeveloperTool => "Developer",
                SettingSection.InstallPath => "GameInstall",
                _ => selected.ToString()
            };
            _suppressSelection = true;
            try
            {
                SettingNavListView.SelectedItem = SettingNavListView.Items.OfType<ListViewItem>()
                    .FirstOrDefault(item => item.Tag as string == tag);
            }
            finally { _suppressSelection = false; }
        }
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
        if (_suppressSelection) return;
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
    private async void AccountLaunch_ExecuteRequested(object? sender, EventArgs e)
    {
        if (sender is AccountLaunchControl control)
            await InvokeHomeAsync(page => page.ExecuteClientAsync(control.ClientIndex));
    }

    private void AccountLaunch_BrowserRequested(object? sender, EventArgs e)
    {
        if (sender is AccountLaunchControl control)
            RequestBrowserLogin(control.ClientIndex);
    }

    private void RequestBrowserLogin(int comboBoxIndex)
    {
        if (Launches?.CanLogin != true) return;
        string? selectedAccountId = comboBoxIndex switch
        {
            0 => _homePage?.SelectedAccount1Id,
            1 => _homePage?.SelectedAccount2Id,
            2 => _homePage?.SelectedAccount3Id,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(selectedAccountId) || _homePage is null)
            return;

        Account? account = _homePage.Accounts.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, selectedAccountId, StringComparison.OrdinalIgnoreCase));
        if (account is not null)
            BrowserLoginRequested?.Invoke(this, new SidebarBrowserLoginRequestedEventArgs(account));
    }

    private async Task InvokeHomeAsync(Func<StationPage, Task> operation)
    {
        if (_homePage is not null)
            await SafeExecution.RunHandledAsync(() => operation(_homePage), "SidebarView.HomeOperation");
    }

    private void RequestSetting(SettingSection section)
        => SettingRequested?.Invoke(this, new SidebarSettingRequestedEventArgs(section));
}
