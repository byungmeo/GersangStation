using Core.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.Diagnostics;

namespace GersangStation.Main.Setting;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AccountSettingPage : Page
{
    private readonly List<Account> _selectedAccounts = [];

    public bool Editable => _selectedAccounts.Count == 1;
    public bool Deletable => _selectedAccounts.Count > 0;

    public AccountSettingPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        AccountsCVS.Source = Account.GetAccountsGrouped();
    }

    private void AccountListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (var addedAccount in e.AddedItems)
        {
            Debug.WriteLine(addedAccount.GetType());
            if(addedAccount is Account account)
            {
                _selectedAccounts.Add(account);
            }
        }

        foreach (var removedAccount in e.RemovedItems)
        {
            Debug.WriteLine(removedAccount.GetType());
            if (removedAccount is Account account)
            {
                _selectedAccounts.Remove(account);
            }
        }

        Bindings.Update();
    }

    private void SaveButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // TODO: 추�? 모드, ?�집 모드 구분?�서 ?�?�하�?추�? 모드�??�환
    }

    private void EditButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // TODO: 계정 ?�력 컨트롤을 모두 ?�택??계정??값들�?바꾼 ???�집 모드�??�환
    }

    private void DeleteButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // TODO: ?�택??계정??모두 ??�� (PasswordVault???�는 비�?번호까�? 지??�?
    }
}
