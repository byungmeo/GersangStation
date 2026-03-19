using Windows.ApplicationModel;

namespace Core;

public enum PackageVersionCompareResult
{
    Older = -1,
    Equal = 0,
    Newer = 1
}

public static class PackageVersionComparer
{
    public static PackageVersionCompareResult Compare(PackageVersion left, PackageVersion right)
    {
        int result = left.Major.CompareTo(right.Major);
        if (result != 0)
            return result < 0 ? PackageVersionCompareResult.Older : PackageVersionCompareResult.Newer;

        result = left.Minor.CompareTo(right.Minor);
        if (result != 0)
            return result < 0 ? PackageVersionCompareResult.Older : PackageVersionCompareResult.Newer;

        result = left.Build.CompareTo(right.Build);
        if (result != 0)
            return result < 0 ? PackageVersionCompareResult.Older : PackageVersionCompareResult.Newer;

        result = left.Revision.CompareTo(right.Revision);
        if (result != 0)
            return result < 0 ? PackageVersionCompareResult.Older : PackageVersionCompareResult.Newer;

        return PackageVersionCompareResult.Equal;
    }

    public static bool IsNewer(PackageVersion candidate, PackageVersion current)
        => Compare(candidate, current) == PackageVersionCompareResult.Newer;

    public static bool IsOlder(PackageVersion candidate, PackageVersion current)
        => Compare(candidate, current) == PackageVersionCompareResult.Older;

    public static bool IsEqual(PackageVersion a, PackageVersion b)
        => Compare(a, b) == PackageVersionCompareResult.Equal;

    /// <summary>
    /// Major/Minor/Build까지만 비교하고 Revision 차이는 무시합니다.
    /// </summary>
    public static PackageVersionCompareResult CompareMajorMinorBuild(PackageVersion left, PackageVersion right)
    {
        int result = left.Major.CompareTo(right.Major);
        if (result != 0)
            return result < 0 ? PackageVersionCompareResult.Older : PackageVersionCompareResult.Newer;

        result = left.Minor.CompareTo(right.Minor);
        if (result != 0)
            return result < 0 ? PackageVersionCompareResult.Older : PackageVersionCompareResult.Newer;

        result = left.Build.CompareTo(right.Build);
        if (result != 0)
            return result < 0 ? PackageVersionCompareResult.Older : PackageVersionCompareResult.Newer;

        return PackageVersionCompareResult.Equal;
    }

    /// <summary>
    /// Revision을 제외한 Major/Minor/Build 기준으로 candidate가 더 높은지 판단합니다.
    /// </summary>
    public static bool IsNewerMajorMinorBuild(PackageVersion candidate, PackageVersion current)
        => CompareMajorMinorBuild(candidate, current) == PackageVersionCompareResult.Newer;
}
