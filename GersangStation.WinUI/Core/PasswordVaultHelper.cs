using System;
using Windows.ApplicationModel;
using Windows.Security.Credentials;

namespace Core;

public static class PasswordVaultHelper
{
    public enum PasswordVaultOperationStatus
    {
        Succeeded,
        NotFound,
        IgnoredInvalidInput,
        Failed
    }

    public enum PasswordVaultReadStatus
    {
        Found,
        Missing,
        IgnoredInvalidInput,
        Failed
    }

    public enum PasswordVaultMoveFailureStage
    {
        ValidateArguments,
        SourceCredentialMissing,
        ReadExistingCredential,
        SaveNewCredential,
        DeleteOldCredential
    }

    /// <summary>
    /// 자격 증명 저장소 작업 결과를 나타냅니다.
    /// </summary>
    public readonly record struct PasswordVaultOperationResult(
        bool Success,
        PasswordVaultOperationStatus Status,
        Exception? Exception = null)
    {
        public bool NotFound => Status == PasswordVaultOperationStatus.NotFound;
        public bool IgnoredInvalidInput => Status == PasswordVaultOperationStatus.IgnoredInvalidInput;

        public static PasswordVaultOperationResult Ok()
            => new(true, PasswordVaultOperationStatus.Succeeded, null);

        public static PasswordVaultOperationResult Missing()
            => new(true, PasswordVaultOperationStatus.NotFound, null);

        public static PasswordVaultOperationResult Ignored()
            => new(true, PasswordVaultOperationStatus.IgnoredInvalidInput, null);

        public static PasswordVaultOperationResult Fail(Exception exception)
            => new(false, PasswordVaultOperationStatus.Failed, exception);
    }

    /// <summary>
    /// 자격 증명 조회 결과를 나타냅니다.
    /// </summary>
    public readonly record struct PasswordVaultReadResult(
        bool Success,
        PasswordVaultReadStatus Status,
        bool HasCredential,
        string? Password,
        Exception? Exception = null)
    {
        public static PasswordVaultReadResult Found(string password)
            => new(true, PasswordVaultReadStatus.Found, true, password);

        public static PasswordVaultReadResult Missing()
            => new(true, PasswordVaultReadStatus.Missing, false, null);

        public static PasswordVaultReadResult Ignored()
            => new(true, PasswordVaultReadStatus.IgnoredInvalidInput, false, null);

        public static PasswordVaultReadResult Fail(Exception exception)
            => new(false, PasswordVaultReadStatus.Failed, false, null, exception);
    }

    /// <summary>
    /// 자격 증명 ID 변경 결과를 단계별 실패 문맥과 함께 반환합니다.
    /// </summary>
    public readonly record struct PasswordVaultMoveResult(
        bool Success,
        PasswordVaultMoveFailureStage? FailureStage = null,
        Exception? Exception = null)
    {
        public static PasswordVaultMoveResult Ok()
            => new(true);

        public static PasswordVaultMoveResult Fail(PasswordVaultMoveFailureStage stage, Exception? exception = null)
            => new(false, stage, exception);
    }

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
        PasswordVaultOperationResult result = TrySave(userName, password);
        if (!result.Success)
            throw result.Exception ?? new InvalidOperationException("Failed to save password.");
    }

    public static string? GetPassword(string userName)
    {
        PasswordVaultReadResult result = TryGetPassword(userName);
        return result.Success && result.HasCredential ? result.Password : null;
    }

    /// <summary>
    /// 비밀번호를 저장하고 실패 원인을 결과로 반환합니다.
    /// </summary>
    public static PasswordVaultOperationResult TrySave(string userName, string password)
    {
        userName = userName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            return PasswordVaultOperationResult.Ignored();

        try
        {
            // 동일한 userName이 이미 있으면 덮어쓰기 위해 기존 항목을 먼저 삭제합니다.
            PasswordVaultOperationResult deleteResult = TryDelete(userName);
            if (!deleteResult.Success)
                return deleteResult;

            var credential = new PasswordCredential(ResourceName, userName, password);
            _vault.Add(credential);
            return PasswordVaultOperationResult.Ok();
        }
        catch (Exception ex)
        {
            return PasswordVaultOperationResult.Fail(ex);
        }
    }

    /// <summary>
    /// 비밀번호를 조회하고, 미존재와 접근 실패를 구분해 반환합니다.
    /// </summary>
    public static PasswordVaultReadResult TryGetPassword(string userName)
    {
        userName = userName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userName))
            return PasswordVaultReadResult.Ignored();

        try
        {
            var credential = _vault.Retrieve(ResourceName, userName);
            credential.RetrievePassword();
            return PasswordVaultReadResult.Found(credential.Password);
        }
        catch (Exception ex) when (IsCredentialNotFound(ex))
        {
            return PasswordVaultReadResult.Missing();
        }
        catch (Exception ex)
        {
            return PasswordVaultReadResult.Fail(ex);
        }
    }

    public static List<string> GetAllUserNames()
    {
        (IReadOnlyList<string> userNames, PasswordVaultOperationResult result) = TryGetAllUserNames();
        return result.Success ? [.. userNames] : [];
    }

    /// <summary>
    /// 현재 저장소에 있는 모든 계정 ID를 읽고 실패 원인을 결과로 반환합니다.
    /// </summary>
    public static (IReadOnlyList<string> UserNames, PasswordVaultOperationResult Result) TryGetAllUserNames()
    {
        try
        {
            List<string> userNames = _vault.FindAllByResource(ResourceName)
                .Select(c => c.UserName?.Trim() ?? string.Empty)
                .Where(userName => userName.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (userNames, PasswordVaultOperationResult.Ok());
        }
        catch (Exception ex) when (IsCredentialNotFound(ex))
        {
            return ([], PasswordVaultOperationResult.Missing());
        }
        catch (Exception ex)
        {
            return ([], PasswordVaultOperationResult.Fail(ex));
        }
    }

    public static bool Move(string currentUserName, string newUserName)
    {
        PasswordVaultMoveResult result = TryMove(currentUserName, newUserName);
        return result.Success;
    }

    /// <summary>
    /// 자격 증명 ID를 변경하고 실패 단계를 결과로 반환합니다.
    /// </summary>
    public static PasswordVaultMoveResult TryMove(string currentUserName, string newUserName)
    {
        currentUserName = currentUserName?.Trim() ?? string.Empty;
        newUserName = newUserName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(currentUserName) || string.IsNullOrWhiteSpace(newUserName))
            return PasswordVaultMoveResult.Fail(PasswordVaultMoveFailureStage.ValidateArguments);

        if (string.Equals(currentUserName, newUserName, StringComparison.OrdinalIgnoreCase))
            return PasswordVaultMoveResult.Ok();

        PasswordVaultReadResult readResult = TryGetPassword(currentUserName);
        if (!readResult.Success)
            return PasswordVaultMoveResult.Fail(
                PasswordVaultMoveFailureStage.ReadExistingCredential,
                readResult.Exception);

        if (!readResult.HasCredential || string.IsNullOrWhiteSpace(readResult.Password))
            return PasswordVaultMoveResult.Fail(PasswordVaultMoveFailureStage.SourceCredentialMissing);

        PasswordVaultOperationResult saveResult = TrySave(newUserName, readResult.Password);
        if (!saveResult.Success)
            return PasswordVaultMoveResult.Fail(
                PasswordVaultMoveFailureStage.SaveNewCredential,
                saveResult.Exception);

        PasswordVaultOperationResult deleteResult = TryDelete(currentUserName);
        return deleteResult.Success
            ? PasswordVaultMoveResult.Ok()
            : PasswordVaultMoveResult.Fail(
                PasswordVaultMoveFailureStage.DeleteOldCredential,
                deleteResult.Exception);
    }

    public static bool Delete(string userName)
    {
        PasswordVaultOperationResult result = TryDelete(userName);
        return result.Success;
    }

    /// <summary>
    /// 자격 증명을 삭제하고 실패 원인을 결과로 반환합니다.
    /// </summary>
    public static PasswordVaultOperationResult TryDelete(string userName)
    {
        userName = userName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userName))
            return PasswordVaultOperationResult.Ignored();

        try
        {
            var credential = _vault.Retrieve(ResourceName, userName);
            _vault.Remove(credential);
            return PasswordVaultOperationResult.Ok();
        }
        catch (Exception ex) when (IsCredentialNotFound(ex))
        {
            return PasswordVaultOperationResult.Missing();
        }
        catch (Exception ex)
        {
            return PasswordVaultOperationResult.Fail(ex);
        }
    }

    /// <summary>
    /// 현재 계정 목록에 없는 자격 증명을 제거해 계정-비밀번호 1:1 관계를 유지합니다.
    /// </summary>
    public static void RemoveOrphans(IEnumerable<string> validUserNames)
    {
        PasswordVaultOperationResult result = TryRemoveOrphans(validUserNames);
        if (!result.Success)
            throw result.Exception ?? new InvalidOperationException("Failed to remove orphan credentials.");
    }

    /// <summary>
    /// 현재 계정 목록에 없는 자격 증명을 제거하고 실패 원인을 결과로 반환합니다.
    /// </summary>
    public static PasswordVaultOperationResult TryRemoveOrphans(IEnumerable<string> validUserNames)
    {
        HashSet<string> validIds = validUserNames
            .Where(userName => !string.IsNullOrWhiteSpace(userName))
            .Select(userName => userName.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        (IReadOnlyList<string> userNames, PasswordVaultOperationResult getAllResult) = TryGetAllUserNames();
        if (!getAllResult.Success)
            return getAllResult;

        foreach (string userName in userNames)
        {
            if (!validIds.Contains(userName))
            {
                PasswordVaultOperationResult deleteResult = TryDelete(userName);
                if (!deleteResult.Success)
                    return deleteResult;
            }
        }

        return PasswordVaultOperationResult.Ok();
    }

    private static bool IsCredentialNotFound(Exception exception)
        => exception.HResult == unchecked((int)0x80070490);
}
