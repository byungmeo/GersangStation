using Microsoft.UI.Xaml.Controls;

namespace GersangStation.Main.Setting;

public sealed partial class GeneralSettingPage : Page
{
    public GeneralSettingPage()
    {
        InitializeComponent();
        ContentFrame.Navigate(typeof(BehaviorSettingPage));
    }

    private void GeneralSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        => ContentFrame.Navigate(sender.SelectedItem == sender.Items[1]
            ? typeof(ExecutionSettingPage)
            : typeof(BehaviorSettingPage));
}
