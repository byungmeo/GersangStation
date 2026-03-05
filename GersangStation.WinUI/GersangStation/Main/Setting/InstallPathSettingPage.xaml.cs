using Core.Models;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Text;

namespace GersangStation.Main.Setting;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
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

    public async Task<bool> ConfirmLeaveAsync()
    {
        if (ContentFrame.Content is IConfirmLeave confirm)
            return await confirm.ConfirmLeaveAsync();

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

        SelectorBarItem selectedItem = sender.SelectedItem;
        foreach (var item in sender.Items.OfType<SelectorBarItem>())
        {
            item.FontWeight = FontWeights.Normal;
            item.FontSize = 14;
        }
        if (sender.SelectedItem is SelectorBarItem selected)
        {
            selected.FontWeight = FontWeights.SemiBold;
            selected.FontSize = 20;
        }

        int currentSelectedIndex = sender.Items.IndexOf(selectedItem);

        var effect = currentSelectedIndex - _previousSelectedIndex > 0
            ? SlideNavigationTransitionEffect.FromRight
            : SlideNavigationTransitionEffect.FromLeft;

        ContentFrame.Navigate(
                typeof(ServerInstallPathSettingPage),
                currentSelectedIndex,
                new SlideNavigationTransitionInfo { Effect = effect });

        _previousSelectedIndex = currentSelectedIndex;
        _previousSelectedItem = sender.SelectedItem;
    }
}
