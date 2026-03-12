using System;
using Windows.ApplicationModel;
using Windows.Security.Credentials;

namespace Core;

public static class PasswordVaultHelper
{
    private static readonly PasswordVault _vault = new();
    private static string? _resourceName;

    /// <summary>
    /// 앱의 고유 패키지 패밀리 이름(PFN)을 ResourceName으로 사용합니다.
    /// </summary>
    private static string ResourceName
    {
        get
        {
            if (string.IsNullOrEmpty(_resourceName))
            {
                try
                {
                    // MSIX 패키징된 경우 시스템이 부여한 고유 이름 사용
                    _resourceName = Package.Current.Id.FamilyName;
                }
                catch (InvalidOperationException)
                {
                    // 패키징되지 않은 경우(Unpackaged/Debug)를 위한 고유 ID
                    _resourceName = "Byungmeo.642537A4A3EB7_caw905xh8pwgp";
                }
            }

            return _resourceName;
        }
    }

    public static void Save(string userName, string password)
    {
        userName = userName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return;

        // 동일한 userName이 이미 있으면 덮어쓰기 위해 기존 항목을 먼저 삭제합니다.
        Delete(userName);

        var credential = new PasswordCredential(ResourceName, userName, password);
        _vault.Add(credential);
    }

    public static string? GetPassword(string userName)
    {
        userName = userName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userName))
            return null;

        try
        {
            var credential = _vault.Retrieve(ResourceName, userName);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch
        {
            return null;
        }
    }

    public static List<string> GetAllUserNames()
    {
        try
        {
            return _vault.FindAllByResource(ResourceName)
                .Select(c => c.UserName?.Trim() ?? string.Empty)
                .Where(userName => userName.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static bool Move(string currentUserName, string newUserName)
    {
        currentUserName = currentUserName?.Trim() ?? string.Empty;
        newUserName = newUserName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(currentUserName) || string.IsNullOrWhiteSpace(newUserName))
            return false;

        if (string.Equals(currentUserName, newUserName, StringComparison.OrdinalIgnoreCase))
            return true;

        string? password = GetPassword(currentUserName);
        if (string.IsNullOrWhiteSpace(password))
            return false;

        Save(newUserName, password);
        Delete(currentUserName);
        return true;
    }

    public static bool Delete(string userName)
    {
        userName = userName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userName))
            return true;

        try
        {
            var credential = _vault.Retrieve(ResourceName, userName);
            _vault.Remove(credential);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 현재 계정 목록에 없는 자격 증명을 제거해 계정-비밀번호 1:1 관계를 유지합니다.
    /// </summary>
    public static void RemoveOrphans(IEnumerable<string> validUserNames)
    {
        HashSet<string> validIds = validUserNames
            .Where(userName => !string.IsNullOrWhiteSpace(userName))
            .Select(userName => userName.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string userName in GetAllUserNames())
        {
            if (!validIds.Contains(userName))
                Delete(userName);
        }
    }
}
