namespace Core.Test;

[TestClass]
public sealed class LoginIdNormalizationTest
{
    [TestMethod]
    public void StrictComparison_Fails_ForCaseAndPercentEncodedUnderscore()
    {
        const string selectedAccountId = "Abcd1234_";
        const string cookieMemberId = "abcd1234%5F";

        bool same = string.Equals(selectedAccountId, cookieMemberId, StringComparison.Ordinal);

        Assert.IsFalse(same);
    }

    [TestMethod]
    public void DecodedCaseInsensitiveComparison_Matches_ForCaseAndPercentEncodedUnderscore()
    {
        const string selectedAccountId = "Abcd1234_";
        const string cookieMemberId = "abcd1234%5F";

        bool same = LoginIdComparer.EqualsForComparison(selectedAccountId, cookieMemberId);

        Assert.IsTrue(same);
    }

    [TestMethod]
    public void DecodedCaseInsensitiveComparison_Matches_ForRepeatedEscapingCandidate()
    {
        const string selectedAccountId = "Abcd1234_";
        const string cookieMemberId = "AbCd1234%5f";

        bool same = LoginIdComparer.EqualsForComparison(selectedAccountId, cookieMemberId);

        Assert.IsTrue(same);
    }

    [TestMethod]
    public void DecodedCaseInsensitiveComparison_DoesNotMatch_ForDifferentId()
    {
        const string selectedAccountId = "Abcd1234_";
        const string cookieMemberId = "Abcd12345%5F";

        bool same = LoginIdComparer.EqualsForComparison(selectedAccountId, cookieMemberId);

        Assert.IsFalse(same);
    }

    [TestMethod]
    public void DecodedCaseInsensitiveComparison_Matches_ForUtf8PercentEncodedKorean()
    {
        const string selectedAccountId = "테스트";
        const string cookieMemberId = "%ED%85%8C%EC%8A%A4%ED%8A%B8";

        bool same = LoginIdComparer.EqualsForComparison(selectedAccountId, cookieMemberId);

        Assert.IsTrue(same);
    }
}
