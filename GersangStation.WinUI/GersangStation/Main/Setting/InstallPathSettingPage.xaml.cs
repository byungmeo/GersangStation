using Core.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Text;

namespace GersangStation.Main.Setting;

/// <summary>
/// 서버별 메인 및 복제 클라이언트 경로 설정 화면을 호스팅하는 페이지입니다.
/// </summary>
public sealed partial class InstallPathSettingPage : Page, IConfirmLeave
{
    private bool _suppressNavSelectionChanged;
    private int _previousSelectedIndex = -1;
    private SelectorBarItem _previousSelectedItem;

    public InstallPathSettingPage()
    {
        InitializeComponent();

        _previousSelectedItem = GameServerSelectorBar.SelectedItem;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        GameServer initialServer = e.Parameter switch
        {
            GameServerSettingNavigationParameter parameter => parameter.Server,
            GameServer server => server,
            int index => (GameServer)index,
            _ => GameServer.Korea_Live
        };

        NavigateToServer(initialServer, useTransition: false);
    }

    public async Task<bool> ConfirmLeaveAsync(LeaveReason reason = LeaveReason.Navigation)
    {
        if (ContentFrame.Content is IConfirmLeave confirm)
            return await confirm.ConfirmLeaveAsync(reason);

        if (reason == LeaveReason.AppExit && XamlRoot is not null)
            return await ExitConfirmationDialog.ShowAsync(XamlRoot);

        return true;
    }

    private async void GameServerSelectorBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_suppressNavSelectionChanged)
            return;

        if (ContentFrame.Content is IConfirmLeave confirm)
        {
            bool canLeave = await confirm.ConfirmLeaveAsync();
            if (!canLeave)
            {
                _suppressNavSelectionChanged = true;
                sender.SelectedItem = _previousSelectedItem;
                _suppressNavSelectionChanged = false;
                return;
            }
        }

        if (sender.SelectedItem is not SelectorBarItem selectedItem)
            return;

        int currentSelectedIndex = sender.Items.IndexOf(selectedItem);
        if (currentSelectedIndex < 0)
            return;

        NavigateToServer((GameServer)currentSelectedIndex, useTransition: true);
    }

    /// <summary>
    /// 지정한 서버에 맞게 SelectorBar와 하위 경로 설정 페이지를 동기화합니다.
    /// </summary>
    private void NavigateToServer(GameServer server, bool useTransition)
    {
        SelectorBarItem selectedItem = GetSelectorBarItem(server);

        _suppressNavSelectionChanged = true;
        GameServerSelectorBar.SelectedItem = selectedItem;
        UpdateSelectorBarVisualState(GameServerSelectorBar, selectedItem);
        _suppressNavSelectionChanged = false;

        int currentSelectedIndex = (int)server;
        SlideNavigationTransitionEffect effect = currentSelectedIndex - _previousSelectedIndex > 0
            ? SlideNavigationTransitionEffect.FromRight
            : SlideNavigationTransitionEffect.FromLeft;

        ContentFrame.Navigate(
            typeof(ServerInstallPathSettingPage),
            server,
            new SlideNavigationTransitionInfo { Effect = useTransition ? effect : SlideNavigationTransitionEffect.FromRight });

        _previousSelectedIndex = currentSelectedIndex;
        _previousSelectedItem = selectedItem;
    }

    /// <summary>
    /// 서버 값에 대응하는 SelectorBarItem을 반환합니다.
    /// </summary>
    private SelectorBarItem GetSelectorBarItem(GameServer server)
        => server switch
        {
            GameServer.Korea_Live => SelectorBarItemPage1,
            GameServer.Korea_Test => SelectorBarItemPage2,
            GameServer.Korea_RnD => SelectorBarItemPage3,
            _ => throw new ArgumentOutOfRangeException(nameof(server), server, null),
        };

    /// <summary>
    /// 현재 선택된 서버 항목만 강조되도록 SelectorBar 시각 상태를 갱신합니다.
    /// </summary>
    private static void UpdateSelectorBarVisualState(SelectorBar selectorBar, SelectorBarItem selectedItem)
    {
        foreach (SelectorBarItem item in selectorBar.Items.OfType<SelectorBarItem>())
        {
            item.FontWeight = FontWeights.Normal;
            item.FontSize = 14;
        }

        selectedItem.FontWeight = FontWeights.SemiBold;
        selectedItem.FontSize = 20;
    }
}
