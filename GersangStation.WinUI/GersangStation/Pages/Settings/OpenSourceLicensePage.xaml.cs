using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GersangStation.Main.Setting;

public sealed partial class OpenSourceLicensePage : Page
{
    public OpenSourceLicensePage() => InitializeComponent();

    private void LinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { Tag: string linkKey }
            && App.CurrentWindow is MainWindow window)
        {
            window.NavigateToWebViewPageByLinkKey(linkKey);
        }
    }
}
