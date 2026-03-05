using ABI.System;
using Core.Patch;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Core.Models;

public sealed class PatchReadmeInfoItem(DateTime date, int version, List<string> details)
{
    public DateTime Date { get; } = date;
    public int Version { get; } = version;
    public List<string> Details { get; } = details;
    public string Display => $"v{Version}   {Date:yyyy-MM-dd}";
}

public static partial class PatchReadmeHelper
{
    [GeneratedRegex(@"-(?<date>\d{4}\.\d{2}\.\d{2})-\s*\r?\n\[거상 패치 V(?<version>\d+)\]\s*\r?\n(?<body>.*?)(?=(?:\r?\n-\d{4}\.\d{2}\.\d{2}-)|\z)", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex GetBlockRegex();
    private static readonly Regex BlockRegex = GetBlockRegex();

    public static async Task<List<PatchReadmeInfoItem>> GetPatchInfoList(GameServer server)
    {
        string readmeText = await DownloadReadMeAsync(GameServerHelper.GetReadMeUrl(server));
        return Parse(readmeText);
    }

    private static List<PatchReadmeInfoItem> Parse(string readmeText)
    {
        if (string.IsNullOrWhiteSpace(readmeText))
            return [];

        List<PatchReadmeInfoItem> result = [];

        MatchCollection matches = BlockRegex.Matches(readmeText);

        foreach (Match match in matches.Cast<Match>())
        {
            string dateText = match.Groups["date"].Value;
            string versionText = match.Groups["version"].Value;
            string bodyText = match.Groups["body"].Value;

            DateTime date = DateTime.ParseExact(
                dateText,
                "yyyy.MM.dd",
                CultureInfo.InvariantCulture);

            int version = int.Parse(versionText, CultureInfo.InvariantCulture);

            List<string> details = bodyText
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            result.Add(new PatchReadmeInfoItem(date, version, details));
        }

        return result;
    }

    private static readonly HttpClient _http = new();
    private static async Task<string> DownloadReadMeAsync(string readmeUrl, CancellationToken ct = default)
    {
        byte[] bytes = await _http.GetByteArrayAsync(readmeUrl, ct);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string result = Encoding.GetEncoding(949).GetString(bytes);
        return result;
    }
}