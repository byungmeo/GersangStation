using GersangStation.Main.Setting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Core.Models;
using System;
using System.Threading.Tasks;

namespace GersangStation.Controls;

/// <summary>기존 클라이언트 설정 폼과 설치 진행 상태를 서버별로 함께 표시합니다.</summary>
public sealed partial class GameInstallPanel : UserControl
{
    public GameInstallPanel() => InitializeComponent();

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(GameInstallSettingPage), typeof(GameInstallPanel), new PropertyMetadata(null));

    public GameInstallSettingPage? State
    {
        get => (GameInstallSettingPage?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public ServerInstallPathSettingPage? SettingsEditor => ClientSettingsFrame.Content as ServerInstallPathSettingPage;

    public async Task LoadSettingsAsync(GameServer server, GameInstallSettingPage owner)
    {
        State = owner;
        if (!ClientSettingsFrame.Navigate(typeof(ServerInstallPathSettingPage),
            new GameClientSettingsNavigationParameter { Server = server, InstallPage = owner },
            new SuppressNavigationTransitionInfo()))
            throw new InvalidOperationException("클라이언트 설정 화면을 열지 못했습니다.");
        if (SettingsEditor is { } editor)
            await editor.Initialization;
    }

    public event RoutedEventHandler? CancelRequested;

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => CancelRequested?.Invoke(sender, e);
}
