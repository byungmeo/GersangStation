using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace GersangStation.Main.Setting;

public sealed partial class SettingPage : Page
{
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
    }

    private void SettingNavationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var selectedItem = (Microsoft.UI.Xaml.Controls.NavigationViewItem)args.SelectedItem;
        if (selectedItem != null)
        {
            string selectedItemTag = ((string)selectedItem.Tag);
            string pageName = "GersangStation.Main.Setting." + selectedItemTag;
            PageDictionary.TryGetValue(pageName, out Type? pageType);
            if (pageType is not null)
                ContentFrame.Navigate(pageType);
        }
    }
}
