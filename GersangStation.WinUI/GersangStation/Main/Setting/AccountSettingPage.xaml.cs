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
        // TODO: 추가 모드, 편집 모드 구분해서 저장하고 추가 모드로 전환
    }

    private void EditButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // TODO: 계정 입력 컨트롤을 모두 선택한 계정의 값들로 바꾼 뒤 편집 모드로 전환
    }

    private void DeleteButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // TODO: 선택한 계정들 모두 삭제 (PasswordVault에 있는 비밀번호까지 지울 것)
    }
}
