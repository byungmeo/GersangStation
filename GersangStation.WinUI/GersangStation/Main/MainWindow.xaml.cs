using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace GersangStation.Main;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public WebViewManager? WebViewManager { get; private set; }

    private int previousSelectedIndex = 0;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;

        // WebViewPage 초기화를 위해 강제로 Navigate 호출
        ContentFrame.Navigate(typeof(WebViewPage), this);
        ContentFrame.Navigate(typeof(StationPage));
    }

    internal void RegisterWebViewManager(WebViewManager webviewManager)
    {
        WebViewManager = webviewManager;
    }

    private void MainSelectorBar_SelectionChanged(Microsoft.UI.Xaml.Controls.SelectorBar sender, Microsoft.UI.Xaml.Controls.SelectorBarSelectionChangedEventArgs args)
    {
        SelectorBarItem selectedItem = sender.SelectedItem;
        int currentSelectedIndex = sender.Items.IndexOf(selectedItem);
        System.Type pageType;

        switch (currentSelectedIndex)
        {
            case 0:
                pageType = typeof(StationPage);
                break;
            //case 1:
            //    pageType = typeof(WebViewPage);
            //    break;
            //case 2:
            //    pageType = typeof(SamplePage3);
            //    break;
            //case 3:
            //    pageType = typeof(SamplePage4);
            //    break;
            default:
                pageType = typeof(WebViewPage);
                break;
        }

        var slideNavigationTransitionEffect = currentSelectedIndex - previousSelectedIndex > 0 ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft;

        ContentFrame.Navigate(pageType, null, new SlideNavigationTransitionInfo() { Effect = slideNavigationTransitionEffect });

        previousSelectedIndex = currentSelectedIndex;
    }
}
