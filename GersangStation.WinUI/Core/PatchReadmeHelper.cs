using Core.Models;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Core;

public enum PatchReadmeFailureStage
{
    DownloadReadme,
    DecodeReadme,
    ParseReadme
}

/// <summary>
/// 패치 readme 조회/파싱 실패 시 단계와 URL 문맥을 함께 보존합니다.
/// </summary>
public sealed class PatchReadmeException : InvalidOperationException
{
    public GameServer Server { get; }
    public string ReadmeUrl { get; }
    public PatchReadmeFailureStage Stage { get; }

    public PatchReadmeException(
        string message,
        GameServer server,
        string readmeUrl,
        PatchReadmeFailureStage stage,
        Exception innerException)
        : base(message, innerException)
    {
        Server = server;
        ReadmeUrl = readmeUrl;
        Stage = stage;
    }
}

/// <summary>
/// 패치 readme 다운로드와 파싱을 담당합니다.
/// </summary>
public static partial class PatchReadmeHelper
{
    [GeneratedRegex(@"-(?<date>\d{4}\.\d{2}\.\d{2})-\s*\r?\n\[거상 패치 V(?<version>\d+)\]\s*\r?\n(?<body>.*?)(?=(?:\r?\n-\d{4}\.\d{2}\.\d{2}-)|\z)", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex GetBlockRegex();

    private static readonly Regex BlockRegex = GetBlockRegex();

    /// <summary>
    /// 패치 readme 전체를 읽어 버전 정보 목록으로 변환합니다.
    /// </summary>
    public static async Task<List<PatchReadmeInfoItem>> GetPatchInfoList(GameServer server, CancellationToken ct = default)
    {
        string readmeUrl = GameServerHelper.GetReadMeUrl(server);
        string readmeText = await DownloadReadMeAsync(server, readmeUrl, ct);
        return Parse(server, readmeUrl, readmeText);
    }

    /// <summary>
    /// 패치 readme에서 최신 버전 목록만 추려 반환합니다.
    /// </summary>
    public static async Task<List<int>> GetLatestVersionList(GameServer server, int count, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        string readmeUrl = GameServerHelper.GetReadMeUrl(server);
        string readmeText = await DownloadReadMeAsync(server, readmeUrl, ct);
        List<PatchReadmeInfoItem> items = Parse(server, readmeUrl, readmeText, count);
        return items.Select(i => i.Version).ToList();
    }

    private static List<PatchReadmeInfoItem> Parse(GameServer server, string readmeUrl, string readmeText, int count = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(readmeText))
        {
            throw new PatchReadmeException(
                $"Patch readme content was empty. url='{readmeUrl}'",
                server,
                readmeUrl,
                PatchReadmeFailureStage.ParseReadme,
                new InvalidDataException("Patch readme content was empty."));
        }

        List<PatchReadmeInfoItem> result = [];

        MatchCollection matches;
        try
        {
            matches = BlockRegex.Matches(readmeText);
        }
        catch (Exception ex)
        {
            throw new PatchReadmeException(
                $"Failed to parse patch readme blocks from '{readmeUrl}'.",
                server,
                readmeUrl,
                PatchReadmeFailureStage.ParseReadme,
                ex);
        }

        if (matches.Count == 0)
        {
            throw new PatchReadmeException(
                $"Patch readme did not contain any recognizable patch blocks. url='{readmeUrl}'",
                server,
                readmeUrl,
                PatchReadmeFailureStage.ParseReadme,
                new InvalidDataException("No recognizable patch blocks were found in the patch readme."));
        }

        foreach (Match match in matches.Cast<Match>())
        {
            string dateText = match.Groups["date"].Value;
            string versionText = match.Groups["version"].Value;
            string bodyText = match.Groups["body"].Value;

            try
            {
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
                if (result.Count >= count)
                    break;
            }
            catch (Exception ex)
            {
                throw new PatchReadmeException(
                    $"Failed to parse patch readme entry. url='{readmeUrl}', date='{dateText}', version='{versionText}'",
                    server,
                    readmeUrl,
                    PatchReadmeFailureStage.ParseReadme,
                    ex);
            }
        }

        return result;
    }

    private static async Task<string> DownloadReadMeAsync(GameServer server, string readmeUrl, CancellationToken ct = default)
    {
        byte[] bytes;
        try
        {
            bytes = await HttpClientProvider.Http.GetByteArrayAsync(readmeUrl, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PatchReadmeException(
                $"Failed to download patch readme from '{readmeUrl}'.",
                server,
                readmeUrl,
                PatchReadmeFailureStage.DownloadReadme,
                ex);
        }

        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(949).GetString(bytes);
        }
        catch (Exception ex)
        {
            throw new PatchReadmeException(
                $"Failed to decode patch readme from '{readmeUrl}' with code page 949.",
                server,
                readmeUrl,
                PatchReadmeFailureStage.DecodeReadme,
                ex);
        }
    }
}
