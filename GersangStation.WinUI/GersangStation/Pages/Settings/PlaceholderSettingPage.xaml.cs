using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace GersangStation.Main.Setting;

public sealed partial class PlaceholderSettingPage : Page
{
    public PlaceholderSettingPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        TitleText.Text = e.Parameter as string ?? "준비 중";
    }
}
