using System.Diagnostics;

namespace Core.Test;

[DoNotParallelize]
[TestClass]
[Ignore("Extractor benchmark only run mode")]
public class PasswordVaultHelperTest
{
    private const string TestUser = "TestUser_AutoLogin";
    private const string TestPass = "TestPassword123!";

    [TestInitialize]
    [TestCleanup]
    public void TestCleanup()
    {
        // 테스트 데이터가 시스템에 남지 않도록 전후로 정리
        PasswordVaultHelper.Delete(TestUser);
    }

    [TestMethod]
    public void Save_And_GetPassword_Success_Test()
    {
        // Arrange & Act
        PasswordVaultHelper.Save(TestUser, TestPass);
        string? retrieved = PasswordVaultHelper.GetPassword(TestUser);
        Debug.WriteLine($"Save_And_GetPassword_Success_Test retrived: {retrieved}");

        // Assert
        Assert.AreEqual(TestPass, retrieved, "저장된 비밀번호와 복호화된 비밀번호가 일치해야 합니다.");
    }

    [TestMethod]
    public void Delete_Password_Should_Return_Null()
    {
        // Arrange
        PasswordVaultHelper.Save(TestUser, TestPass);

        // Act
        PasswordVaultHelper.Delete(TestUser);
        string? retrieved = PasswordVaultHelper.GetPassword(TestUser);
        Debug.WriteLine($"Delete_Password_Should_Return_Null retrived: {retrieved}");

        // Assert
        Assert.IsNull(retrieved, "삭제된 계정은 조회 시 null을 반환해야 합니다.");
    }

    [TestMethod]
    public void GetAllUserNames_Should_Contain_Saved_User()
    {
        // Arrange
        PasswordVaultHelper.Save(TestUser, TestPass);

        // Act
        List<string> users = PasswordVaultHelper.GetAllUserNames();
        Debug.WriteLine($"GetAllUserNames_Should_Contain_Saved_User users:");
        foreach (var user in users)
        {
            Debug.WriteLine($"\t- {user}");
        }

        // Assert
        Assert.IsTrue(users.Contains(TestUser), "저장된 유저 아이디가 목록에 포함되어야 합니다.");
    }

    [TestMethod]
    public void GetPassword_NonExistentUser_Returns_Null()
    {
        // Act
        string? retrieved = PasswordVaultHelper.GetPassword("NonExistent_User_ID");
        Debug.WriteLine($"GetPassword_NonExistentUser_Returns_Null retrieved: {retrieved}");

        // Assert
        Assert.IsNull(retrieved);
    }
}