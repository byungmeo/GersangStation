using Windows.Security.Credentials;
using Windows.ApplicationModel;

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
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password)) return;

        var credential = new PasswordCredential(ResourceName, userName, password);
        _vault.Add(credential);
    }

    public static string? GetPassword(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return null;

        try
        {
            var credential = _vault.Retrieve(ResourceName, userName);
            return credential.Password;
        }
        catch { return null; }
    }

    public static List<string> GetAllUserNames()
    {
        try
        {
            return _vault.FindAllByResource(ResourceName).Select(c => c.UserName).ToList();
        }
        catch { return []; }
    }

    public static void Delete(string userName)
    {
        try
        {
            var credential = _vault.Retrieve(ResourceName, userName);
            _vault.Remove(credential);
        }
        catch { }
    }
}