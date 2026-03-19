using Windows.ApplicationModel;

namespace Core.Test;

[TestClass]
public sealed class PackageVersionHelperTest
{
    [TestMethod]
    public void CompareMajorMinorBuild_ReturnsEqual_WhenOnlyRevisionDiffers()
    {
        PackageVersion left = new(1, 2, 3, 4);
        PackageVersion right = new(1, 2, 3, 99);

        PackageVersionCompareResult result = PackageVersionComparer.CompareMajorMinorBuild(left, right);

        Assert.AreEqual(PackageVersionCompareResult.Equal, result);
    }

    [TestMethod]
    public void IsNewerMajorMinorBuild_ReturnsTrue_WhenBuildIsHigher()
    {
        PackageVersion candidate = new(1, 2, 4, 0);
        PackageVersion current = new(1, 2, 3, 999);

        bool isNewer = PackageVersionComparer.IsNewerMajorMinorBuild(candidate, current);

        Assert.IsTrue(isNewer);
    }

    [TestMethod]
    public void IsNewerMajorMinorBuild_ReturnsFalse_WhenOnlyRevisionIsHigher()
    {
        PackageVersion candidate = new(1, 2, 3, 10);
        PackageVersion current = new(1, 2, 3, 0);

        bool isNewer = PackageVersionComparer.IsNewerMajorMinorBuild(candidate, current);

        Assert.IsFalse(isNewer);
    }
}
