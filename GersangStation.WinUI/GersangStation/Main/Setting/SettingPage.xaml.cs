using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GersangStation.Main.Setting;

public sealed partial class SettingPage : Page, IConfirmLeave
{
    private object _previousSelectedItem;
    private bool _suppressNavSelectionChanged = false;

    public static Dictionary<string, Type> PageDictionary { get; } = new Dictionary<string, Type>
    {
        {"GersangStation.Main.Setting.AccountSettingPage", typeof(GersangStation.Main.Setting.AccountSettingPage)},
        {"GersangStation.Main.Setting.InstallPathSettingPage", typeof(GersangStation.Main.Setting.InstallPathSettingPage)},
        {"GersangStation.Main.Setting.GamePatchSettingPage", typeof(GersangStation.Main.Setting.GamePatchSettingPage)},
        // {"GersangStation.Main.Setting.BrowserSettingPage", typeof(GersangStation.Main.Setting.BrowserSettingPage)},
        // {"GersangStation.Main.Setting.NotificationSettingPage", typeof(GersangStation.Main.Setting.NotificationSettingPage)},
        // {"GersangStation.Main.Setting.AppearanceSettingPage", typeof(GersangStation.Main.Setting.AppearanceSettingPage)},
        // {"GersangStation.Main.Setting.AdvancedSettingPage", typeof(GersangStation.Main.Setting.AdvancedSettingPage)},
        // {"GersangStation.Main.Setting.HelpPage", typeof(GersangStation.Main.Setting.HelpPage)},
        // {"GersangStation.Main.Setting.SponsorPage", typeof(GersangStation.Main.Setting.SponsorPage)},
        // {"GersangStation.Main.Setting.ProgramInfoPage", typeof(GersangStation.Main.Setting.ProgramInfoPage)},
    };

    public SettingPage()
    {
        InitializeComponent();

        _previousSelectedItem = SettingNavationView.SelectedItem;
    }

    public async Task<bool> ConfirmLeaveAsync()
    {
        if (ContentFrame.Content is IConfirmLeave confirm)
            return await confirm.ConfirmLeaveAsync();

        return true;
    }

    private async void SettingNavationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if(_suppressNavSelectionChanged)
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

        var selectedItem = (NavigationViewItem)args.SelectedItem;

        if (selectedItem != null)
        {
            string selectedItemTag = ((string)selectedItem.Tag);
            string pageName = "GersangStation.Main.Setting." + selectedItemTag;
            PageDictionary.TryGetValue(pageName, out Type? pageType);
            if (pageType is not null)
                ContentFrame.Navigate(pageType);

            _previousSelectedItem = selectedItem;
        }
    }
}
