using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Core.Models;

public class AccountGroupInfoList(IEnumerable<Account> items) : List<Account>(items)
{
    public string Key { get; set; } = "";
}

public sealed class Account(string id, string nickname = "", string groupName = "")
{
    private const string UnknownGroupName = "그룹 미지정";

    [JsonPropertyName("Id")]
    public string Id { get; set; } = id;

    [JsonPropertyName("Nickname")]
    public string Nickname { get; set; } = nickname;

    [JsonPropertyName("GroupName")]
    public string GroupName { get; set; } = groupName;

    [JsonIgnore]
    public string DisplayNickname => string.IsNullOrWhiteSpace(Nickname) ? Id : Nickname;

    [JsonIgnore]
    public string GroupKey => string.IsNullOrWhiteSpace(GroupName) ? UnknownGroupName : GroupName.Trim();

    public override string ToString() => $"[Id: {Id}, Nickname: {Nickname}, GroupName: {GroupName}]";

    public Account Clone() => new(Id, Nickname, GroupName);

    /// <summary>
    /// 저장된 계정 목록을 그룹화하고, 로드 결과 메타데이터를 함께 반환합니다.
    /// </summary>
    public static (ObservableCollection<AccountGroupInfoList> Groups, AppDataManager.AppDataOperationResult Result) TryGetAccountsGrouped()
    {
        (IList<Account> accounts, AppDataManager.AppDataOperationResult result) = AppDataManager.TryLoadAccounts();
        return (GetAccountsGrouped(accounts), result);
    }

    /// <summary>
    /// 그룹화된 계정 목록만 필요한 기존 호출부를 위한 편의 래퍼입니다.
    /// </summary>
    public static ObservableCollection<AccountGroupInfoList> GetAccountsGrouped()
        => TryGetAccountsGrouped().Groups;

    public static ObservableCollection<AccountGroupInfoList> GetAccountsGrouped(IEnumerable<Account>? accounts,
        IEnumerable<string>? groupOrder = null)
    {
        Dictionary<string, int> priorities = (groupOrder ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim()).Distinct(StringComparer.Ordinal)
            .Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index, StringComparer.Ordinal);
        var query = from item in accounts ?? []
                    let groupKey = item.GroupKey
                    group item by groupKey into g
                    orderby priorities.GetValueOrDefault(g.Key, int.MaxValue), (g.Key == UnknownGroupName), g.Key
                    select new AccountGroupInfoList(g) { Key = g.Key };

        return new ObservableCollection<AccountGroupInfoList>(query);
    }
}
