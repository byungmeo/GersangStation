namespace GersangStationMiniUpdator;

internal sealed class UpdateArguments
{
    public string PackageSource { get; private set; } = string.Empty;

    public string TargetDirectory { get; private set; } = string.Empty;

    public string TargetExecutablePath { get; private set; } = string.Empty;

    public string? TargetVersion { get; private set; }

    public string? ReleaseNotesUrl { get; private set; }

    public int? MainProcessId { get; private set; }

    public bool RestartAfterUpdate { get; private set; } = true;

    public static bool TryParse(string[] args, out UpdateArguments? options, out string errorMessage)
    {
        options = new UpdateArguments();
        errorMessage = string.Empty;

        for (int i = 0; i < args.Length; i++)
        {
            string current = args[i];
            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                errorMessage = $"지원하지 않는 인자 형식입니다: {current}";
                options = null;
                return false;
            }

            if (i + 1 >= args.Length)
            {
                errorMessage = $"인자 값이 누락되었습니다: {current}";
                options = null;
                return false;
            }

            string value = args[++i];
            switch (current)
            {
                case "--package":
                    options.PackageSource = value.Trim();
                    break;
                case "--target-dir":
                    options.TargetDirectory = Path.GetFullPath(value.Trim());
                    break;
                case "--target-exe":
                    options.TargetExecutablePath = Path.GetFullPath(value.Trim());
                    break;
                case "--version":
                    options.TargetVersion = value.Trim();
                    break;
                case "--release-url":
                    string releaseNotesUrl = value.Trim();
                    if (!Uri.IsWellFormedUriString(releaseNotesUrl, UriKind.Absolute))
                    {
                        errorMessage = $"유효하지 않은 릴리즈 URL입니다: {value}";
                        options = null;
                        return false;
                    }

                    options.ReleaseNotesUrl = releaseNotesUrl;
                    break;
                case "--pid":
                    if (!int.TryParse(value, out int processId) || processId <= 0)
                    {
                        errorMessage = $"유효하지 않은 프로세스 ID입니다: {value}";
                        options = null;
                        return false;
                    }

                    options.MainProcessId = processId;
                    break;
                case "--restart":
                    if (!bool.TryParse(value, out bool restart))
                    {
                        errorMessage = $"유효하지 않은 restart 값입니다: {value}";
                        options = null;
                        return false;
                    }

                    options.RestartAfterUpdate = restart;
                    break;
                default:
                    errorMessage = $"지원하지 않는 인자입니다: {current}";
                    options = null;
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(options.PackageSource))
        {
            errorMessage = "패키지 경로 또는 URL(--package)이 필요합니다.";
            options = null;
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.TargetDirectory))
        {
            errorMessage = "대상 폴더(--target-dir)가 필요합니다.";
            options = null;
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.TargetExecutablePath))
        {
            errorMessage = "재실행할 실행 파일 경로(--target-exe)가 필요합니다.";
            options = null;
            return false;
        }

        if (!Directory.Exists(options.TargetDirectory))
        {
            errorMessage = $"대상 폴더가 존재하지 않습니다: {options.TargetDirectory}";
            options = null;
            return false;
        }

        if (!Path.IsPathRooted(options.TargetExecutablePath))
        {
            errorMessage = "대상 실행 파일 경로는 절대 경로여야 합니다.";
            options = null;
            return false;
        }

        return true;
    }

    public static string BuildUsage()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "사용법:",
            "  GersangStationMiniUpdator.exe --package <zip URL 또는 zip 경로> --target-dir <프로그램 폴더> --target-exe <실행 파일 경로> [--version <버전>] [--release-url <릴리즈 URL>] [--pid <메인앱 PID>] [--restart true|false]"
        });
    }
}
