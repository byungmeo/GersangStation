using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;

namespace Core;

/// <summary>
/// 원격 앱 버전 매니페스트의 항목 문자열을 해석하고 필수 수동 업데이트 여부를 계산합니다.
/// </summary>
public static partial class AppUpdateVersionManifestEvaluator
{
    /// <summary>
    /// 전달된 버전 항목 목록에서 현재 버전보다 높은 버전들을 조사해 필수 업데이트 존재 여부를 계산합니다.
    /// </summary>
    public static AppUpdateRequirementEvaluation EvaluateRequiredManualUpdate(
        IEnumerable<string>? versionEntries,
        PackageVersion currentVersion)
    {
        List<ParsedAppUpdateVersionEntry> parsedEntries = [];
        List<string> invalidEntries = [];

        if (versionEntries is not null)
        {
            foreach (string? rawEntry in versionEntries)
            {
                if (string.IsNullOrWhiteSpace(rawEntry))
                    continue;

                if (TryParseEntry(rawEntry, out ParsedAppUpdateVersionEntry entry))
                {
                    parsedEntries.Add(entry);
                    continue;
                }

                invalidEntries.Add(rawEntry.Trim());
            }
        }

        List<ParsedAppUpdateVersionEntry> higherEntries = parsedEntries
            .Where(entry => PackageVersionComparer.IsNewer(entry.Version, currentVersion))
            .OrderBy(entry => entry.Version.Major)
            .ThenBy(entry => entry.Version.Minor)
            .ThenBy(entry => entry.Version.Build)
            .ThenBy(entry => entry.Version.Revision)
            .ToList();

        List<string> higherVersions = higherEntries
            .Select(static entry => entry.VersionText)
            .ToList();

        List<string> requiredVersions = higherEntries
            .Where(static entry => entry.IsRequired)
            .Select(static entry => entry.VersionText)
            .ToList();

        return new AppUpdateRequirementEvaluation(
            requiredVersions.Count > 0,
            higherVersions,
            requiredVersions,
            invalidEntries);
    }

    /// <summary>
    /// "{2.0.6, false}" 형식의 항목 문자열을 파싱합니다.
    /// </summary>
    public static bool TryParseEntry(string? rawEntry, out ParsedAppUpdateVersionEntry entry)
    {
        entry = default;

        if (string.IsNullOrWhiteSpace(rawEntry))
            return false;

        Match match = EntryPattern().Match(rawEntry.Trim());
        if (!match.Success)
            return false;

        string versionText = match.Groups["version"].Value.Trim();
        if (!TryParsePackageVersion(versionText, out PackageVersion version))
            return false;

        if (!bool.TryParse(match.Groups["required"].Value, out bool isRequired))
            return false;

        entry = new ParsedAppUpdateVersionEntry(versionText, isRequired, version);
        return true;
    }

    private static bool TryParsePackageVersion(string versionText, out PackageVersion version)
    {
        version = default;

        string[] parts = versionText.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 3 or > 4)
            return false;

        Span<ushort> parsedParts = stackalloc ushort[4];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!ushort.TryParse(parts[i], out parsedParts[i]))
                return false;
        }

        if (parts.Length == 3)
            parsedParts[3] = 0;

        version = new PackageVersion(parsedParts[0], parsedParts[1], parsedParts[2], parsedParts[3]);
        return true;
    }

    [GeneratedRegex(@"^\{\s*(?<version>\d+(?:\.\d+){2,3})\s*,\s*(?<required>true|false)\s*\}$", RegexOptions.IgnoreCase)]
    private static partial Regex EntryPattern();
}

/// <summary>
/// 원격 앱 버전 매니페스트의 개별 항목을 나타냅니다.
/// </summary>
public readonly record struct ParsedAppUpdateVersionEntry(
    string VersionText,
    bool IsRequired,
    PackageVersion Version);

/// <summary>
/// 현재 버전 기준 필수 수동 업데이트 필요 여부를 요약합니다.
/// </summary>
public sealed record AppUpdateRequirementEvaluation(
    bool HasRequiredUpdate,
    IReadOnlyList<string> HigherVersions,
    IReadOnlyList<string> RequiredVersions,
    IReadOnlyList<string> InvalidEntries);
