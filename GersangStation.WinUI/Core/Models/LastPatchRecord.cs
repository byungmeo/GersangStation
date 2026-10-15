using System.IO;

namespace Core.Models;

/// <summary>The last successfully applied patch range for one server and installation.</summary>
public sealed record LastPatchRecord(int FromVersion, int ToVersion, string InstallPath)
{
    public bool IsValid => FromVersion > 0 && ToVersion > FromVersion
        && !string.IsNullOrWhiteSpace(InstallPath) && Path.IsPathFullyQualified(InstallPath);

    public bool CanReapply(string installPath, int? currentVersion, int? latestVersion)
    {
        if (!IsValid || currentVersion is not int current || latestVersion is not int latest
            || current < ToVersion || latest < current || !Path.IsPathFullyQualified(installPath))
            return false;

        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(InstallPath)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(installPath)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
