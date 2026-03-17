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
}
