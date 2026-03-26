using Windows.ApplicationModel;

namespace Core.Test;

[TestClass]
public sealed class AppUpdateVersionManifestEvaluatorTest
{
    [TestMethod]
    public void EvaluateRequiredManualUpdate_ReturnsTrue_WhenAnyHigherVersionIsRequired()
    {
        string[] entries =
        [
            "{2.0.5, false}",
            "{2.0.6, false}",
            "{2.0.7, true}",
            "{2.0.8, false}"
        ];

        AppUpdateRequirementEvaluation result = AppUpdateVersionManifestEvaluator.EvaluateRequiredManualUpdate(
            entries,
            new PackageVersion(2, 0, 5, 0));

        Assert.IsTrue(result.HasRequiredUpdate);
        CollectionAssert.AreEqual(new[] { "2.0.6", "2.0.7", "2.0.8" }, result.HigherVersions.ToArray());
        CollectionAssert.AreEqual(new[] { "2.0.7" }, result.RequiredVersions.ToArray());
        Assert.AreEqual(0, result.InvalidEntries.Count);
    }

    [TestMethod]
    public void EvaluateRequiredManualUpdate_ReturnsFalse_WhenHigherVersionsAreOptionalOnly()
    {
        string[] entries =
        [
            "{2.0.5, false}",
            "{2.0.6, false}",
            "{2.0.7, false}"
        ];

        AppUpdateRequirementEvaluation result = AppUpdateVersionManifestEvaluator.EvaluateRequiredManualUpdate(
            entries,
            new PackageVersion(2, 0, 5, 0));

        Assert.IsFalse(result.HasRequiredUpdate);
        CollectionAssert.AreEqual(new[] { "2.0.6", "2.0.7" }, result.HigherVersions.ToArray());
        Assert.AreEqual(0, result.RequiredVersions.Count);
    }

    [TestMethod]
    public void EvaluateRequiredManualUpdate_IgnoresInvalidEntries_AndTreatsThreePartVersionAsRevisionZero()
    {
        string[] entries =
        [
            "{2.0.5.0, false}",
            "{2.0.6, true}",
            "invalid",
            "{2.0, false}"
        ];

        AppUpdateRequirementEvaluation result = AppUpdateVersionManifestEvaluator.EvaluateRequiredManualUpdate(
            entries,
            new PackageVersion(2, 0, 5, 0));

        Assert.IsTrue(result.HasRequiredUpdate);
        CollectionAssert.AreEqual(new[] { "2.0.6" }, result.RequiredVersions.ToArray());
        CollectionAssert.AreEqual(new[] { "invalid", "{2.0, false}" }, result.InvalidEntries.ToArray());
    }

    [TestMethod]
    public void EvaluateRequiredManualUpdate_ReturnsFalse_WhenCurrentVersionIsHigherThanManifestEntries()
    {
        string[] entries =
        [
            "{2.0.5, false}",
            "{2.0.6, true}"
        ];

        AppUpdateRequirementEvaluation result = AppUpdateVersionManifestEvaluator.EvaluateRequiredManualUpdate(
            entries,
            new PackageVersion(2, 0, 7, 0));

        Assert.IsFalse(result.HasRequiredUpdate);
        Assert.AreEqual(0, result.HigherVersions.Count);
        Assert.AreEqual(0, result.RequiredVersions.Count);
    }
}
