using Core;
using GersangStation.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace GersangStation.Main.Setting;

/// <summary>
/// 앱 실행 방식과 Windows 시작 프로그램 등록을 편집합니다.
/// </summary>
public sealed partial class ExecutionSettingPage : Page, INotifyPropertyChanged
{
    private static readonly StartupRegistrationService StartupRegistrationService = new();
    private static readonly DesktopShortcutService DesktopShortcutService = new(
        $"{Package.Current.Id.FamilyName}!App",
        StartupRegistrationService.DesktopShortcutIconPath,
        "GersangStation.lnk");

    private bool _isUpdatingStartupRegistration;
    private bool _isStartupRegistrationEnabled;
    private string _executionRegistrationMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ExecutionRegistrationMessage
    {
        get => _executionRegistrationMessage;
        private set
        {
            if (string.Equals(_executionRegistrationMessage, value, StringComparison.Ordinal))
                return;

            _executionRegistrationMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExecutionRegistrationMessageVisibility));
        }
    }

    public Visibility ExecutionRegistrationMessageVisibility
        => string.IsNullOrWhiteSpace(ExecutionRegistrationMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public ExecutionSettingPage()
    {
        InitializeComponent();
        Button_CreateDesktopShortcut.IsEnabled = true;
    }

    private async void ExecutionSettingPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadStartupRegistrationStateAsync();
    }

    private async void ToggleSwitch_StartupRegistration_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingStartupRegistration)
            return;

        await ApplyStartupSelectionAsync(ToggleSwitch_StartupRegistration.IsOn);
    }

    private void Button_CreateDesktopShortcut_Click(object sender, RoutedEventArgs e)
    {
        Button_CreateDesktopShortcut.IsEnabled = false;

        try
        {
            DesktopShortcutCreationResult result = DesktopShortcutService.CreateShortcut();
            ExecutionRegistrationMessage = result.Success
                ? $"바탕화면에 바로가기를 만들었습니다: {result.ShortcutPath}"
                : result.Message;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to create desktop shortcut: {ex}");
            ExecutionRegistrationMessage = "바로가기를 만들지 못했습니다. 다시 시도해도 안 되면 바탕화면 쓰기 권한과 Windows 바로가기 구성을 확인해주세요.";
        }
        finally
        {
            Button_CreateDesktopShortcut.IsEnabled = true;
        }
    }

    private async Task LoadStartupRegistrationStateAsync()
    {
        StartupRegistrationState startupState = await StartupRegistrationService.GetStateAsync();
        if (startupState.IsEnabled)
        {
            AppDataManager.IsStartupAutoRunEnabled = true;
            ApplyStartupStateToControls(enabled: true);
            ExecutionRegistrationMessage = string.Empty;
            return;
        }

        AppDataManager.IsStartupAutoRunEnabled = false;
        ApplyStartupStateToControls(enabled: false);
        ExecutionRegistrationMessage = startupState.Message;
    }

    private async Task ApplyStartupSelectionAsync(bool enabled)
    {
        bool previousEnabled = _isStartupRegistrationEnabled;

        SetStartupToggleInteractivity(isInteractive: false);
        try
        {
            StartupRegistrationOperationResult result = await ConfigureStartupRegistrationAsync(enabled);

            if (result.Success)
            {
                ApplyStartupStateToControls(enabled);
                ExecutionRegistrationMessage = result.Message;
                return;
            }

            ApplyStartupStateToControls(previousEnabled);
            ExecutionRegistrationMessage = result.Message;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply startup selection: {ex}");
            ApplyStartupRegistrationFailure(
                previousEnabled,
                "자동 실행 설정을 변경하지 못했습니다. 다시 시도해도 안 되면 Windows 시작 앱 설정에서 직접 확인해주세요.");
        }
        finally
        {
            SetStartupToggleInteractivity(isInteractive: true);
        }
    }

    private async Task<StartupRegistrationOperationResult> ConfigureStartupRegistrationAsync(bool enabled)
    {
        if (!enabled)
        {
            StartupRegistrationOperationResult disableResult = await StartupRegistrationService.DisableAsync();
            if (!disableResult.Success)
                return disableResult;

            AppDataManager.IsStartupAutoRunEnabled = false;
            return new StartupRegistrationOperationResult(true, string.Empty);
        }

        StartupRegistrationOperationResult enableResult = await StartupRegistrationService.EnableAsync();
        if (!enableResult.Success)
            return enableResult;

        AppDataManager.IsStartupAutoRunEnabled = true;
        return new StartupRegistrationOperationResult(true, string.Empty);
    }

    private void ApplyStartupRegistrationFailure(bool enabled, string message)
    {
        ApplyStartupStateToControls(enabled);
        ExecutionRegistrationMessage = message;
    }

    private void ApplyStartupStateToControls(bool enabled)
    {
        _isUpdatingStartupRegistration = true;
        try
        {
            _isStartupRegistrationEnabled = enabled;

            ToggleSwitch_StartupRegistration.IsOn = enabled;
            Button_CreateDesktopShortcut.IsEnabled = true;
        }
        finally
        {
            _isUpdatingStartupRegistration = false;
        }
    }

    private void SetStartupToggleInteractivity(bool isInteractive)
    {
        ToggleSwitch_StartupRegistration.IsEnabled = isInteractive;
        Button_CreateDesktopShortcut.IsEnabled = isInteractive;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
