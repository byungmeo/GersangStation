using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Core.Models;

// AccountGroupInfoList class definition:
public class AccountGroupInfoList(IEnumerable<Account> items) : List<Account>(items)
{
    public string Key { get; set; } = "";
}

public class Account(string id, string nickname = "", string groupName = "")
{
    [JsonPropertyName("Id")]
    public string Id { get; set; } = id;

    [JsonPropertyName("Nickname")]
    public string Nickname { get; set; } = nickname;

    [JsonPropertyName("GroupName")]
    public string GroupName { get; set; } = groupName;

    public override string ToString() => $"[Id: {Id}, Nickname: {Nickname}, GroupName: {GroupName}]";

    private static ObservableCollection<Account> GetAccounts()
    {
        ObservableCollection<Account> accounts = new(AppDataManager.LoadAccounts());
        return accounts;
    }

    public static ObservableCollection<AccountGroupInfoList> GetAccountsGrouped()
    {
        var query = from item in GetAccounts()
                    let groupKey = string.IsNullOrWhiteSpace(item.GroupName) ? "그룹 미지정" : item.GroupName
                    group item by groupKey into g
                    orderby (g.Key == "그룹 미지정"), g.Key
                    select new AccountGroupInfoList(g) { Key = g.Key };

        return new ObservableCollection<AccountGroupInfoList>(query);
    }
}