using GersangStation.Main.Setting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;

namespace GersangStation.Main;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public WebViewManager? WebViewManager { get; private set; }

    private bool _suppressNavSelectionChanged = false;
    private int _previousSelectedIndex = 0;
    private SelectorBarItem _previousSelectedItem;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;

        // WebViewPage 초기화를 위해 강제로 Navigate 호출
        ContentFrame.Navigate(typeof(WebViewPage), this);
        ContentFrame.Navigate(typeof(StationPage));

        _previousSelectedItem = MainSelectorBar.SelectedItem;
    }

    internal void RegisterWebViewManager(WebViewManager webviewManager)
    {
        WebViewManager = webviewManager;
    }

    private async void MainSelectorBar_SelectionChanged(Microsoft.UI.Xaml.Controls.SelectorBar sender, Microsoft.UI.Xaml.Controls.SelectorBarSelectionChangedEventArgs args)
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
        int currentSelectedIndex = sender.Items.IndexOf(selectedItem);
        System.Type pageType;

        switch (currentSelectedIndex)
        {
            case 0:
                pageType = typeof(StationPage);
                break;
            case 1:
                pageType = typeof(WebViewPage);
                break;
            //case 2:
            //    pageType = typeof(SamplePage3);
            //    break;
            case 3:
                pageType = typeof(Setting.SettingPage);
                break;
            default:
                throw new System.Exception("invalid selectorbar selected index");
        }

        var slideNavigationTransitionEffect = currentSelectedIndex - _previousSelectedIndex > 0 ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft;

        ContentFrame.Navigate(pageType, null, new SlideNavigationTransitionInfo() { Effect = slideNavigationTransitionEffect });

        _previousSelectedIndex = currentSelectedIndex;
        _previousSelectedItem = sender.SelectedItem;
    }

    // 강제로 패치 설정 페이지로 이동
    public void NavigateToSettingPage(SettingSection section)
    {
        ContentFrame.Navigate(typeof(SettingPage), 
            new SettingPageNavigationParameter{ Section = section }, 
            new SlideNavigationTransitionInfo{ Effect = SlideNavigationTransitionEffect.FromLeft });

        _suppressNavSelectionChanged = true;
        _previousSelectedIndex = MainSelectorBar.Items.IndexOf(MainSelectorBar.SelectedItem);
        _previousSelectedItem = MainSelectorBar.SelectedItem;
        MainSelectorBar.SelectedItem = SelectorBarItem_Setting;
        _suppressNavSelectionChanged = false;
    }
}
