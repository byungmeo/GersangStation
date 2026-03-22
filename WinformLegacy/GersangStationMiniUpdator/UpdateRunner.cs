using System.Diagnostics;
using System.IO.Compression;

namespace GersangStationMiniUpdator;

internal enum UpdateStage
{
    Preparing,
    WaitingForMainProcess,
    Downloading,
    Extracting,
    ApplyingFiles,
    Restarting,
    Completed
}

internal sealed class UpdateProgressInfo
{
    public UpdateStage Stage { get; init; }

    public string Message { get; init; } = string.Empty;

    public int Percent { get; init; }
}

internal sealed class UpdateRunner
{
    private static readonly HttpClient HttpClient = new();

    private readonly UpdateArguments options;
    private readonly IProgress<UpdateProgressInfo> progress;
    private readonly CancellationToken cancellationToken;

    public UpdateRunner(UpdateArguments options, IProgress<UpdateProgressInfo> progress, CancellationToken cancellationToken)
    {
        this.options = options;
        this.progress = progress;
        this.cancellationToken = cancellationToken;
    }

    public async Task RunAsync()
    {
        string tempRoot = Path.Combine(options.TargetDirectory, "_MiniUpdatorTemp");
        string workingRoot = Path.Combine(tempRoot, Guid.NewGuid().ToString("N"));
        string packagePath = Path.Combine(workingRoot, "package.zip");
        string extractRoot = Path.Combine(workingRoot, "extracted");

        Directory.CreateDirectory(workingRoot);

        try
        {
            Report(UpdateStage.Preparing, "업데이트 준비 중...", 0);
            await WaitForMainProcessExitAsync();

            Report(UpdateStage.Downloading, "업데이트 파일을 준비하는 중...", 5);
            await AcquirePackageAsync(packagePath);

            Report(UpdateStage.Extracting, "압축을 해제하는 중...", 55);
            Directory.CreateDirectory(extractRoot);
            ZipFile.ExtractToDirectory(packagePath, extractRoot, true);

            Report(UpdateStage.ApplyingFiles, "파일을 적용하는 중...", 70);
            ApplyExtractedFiles(GetEffectiveExtractRoot(extractRoot, Path.GetFileName(options.TargetExecutablePath)));

            if (options.RestartAfterUpdate)
            {
                Report(UpdateStage.Restarting, "프로그램을 다시 실행하는 중...", 95);
                RestartTargetApplication();
            }

            OpenReleaseNotesIfNeeded();

            Report(UpdateStage.Completed, "업데이트가 완료되었습니다.", 100);
        }
        finally
        {
            TryDeleteDirectory(workingRoot);
            TryDeleteDirectory(tempRoot, deleteIfEmptyOnly: true);
        }
    }

    private async Task WaitForMainProcessExitAsync()
    {
        if (!options.MainProcessId.HasValue)
        {
            return;
        }

        try
        {
            using Process process = Process.GetProcessById(options.MainProcessId.Value);
            if (process.HasExited)
            {
                return;
            }

            Report(UpdateStage.WaitingForMainProcess, "거상 스테이션 미니 종료를 기다리는 중...", 2);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (ArgumentException)
        {
            // Already exited.
        }
    }

    private async Task AcquirePackageAsync(string packagePath)
    {
        if (Uri.TryCreate(options.PackageSource, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            await DownloadPackageAsync(uri, packagePath);
            return;
        }

        string sourcePath = Path.GetFullPath(options.PackageSource);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("업데이트 zip 파일을 찾을 수 없습니다.", sourcePath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(packagePath)!);
        File.Copy(sourcePath, packagePath, true);
        Report(UpdateStage.Downloading, "로컬 업데이트 파일 준비 완료", 50);
    }

    private async Task DownloadPackageAsync(Uri sourceUri, string packagePath)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, sourceUri);
        using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        long? contentLength = response.Content.Headers.ContentLength;
        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream fileStream = new(packagePath, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;

            if (contentLength.HasValue && contentLength.Value > 0)
            {
                int percent = 5 + (int)Math.Min(45, totalRead * 45 / contentLength.Value);
                Report(UpdateStage.Downloading, $"업데이트 파일 다운로드 중... ({percent - 5}%)", percent);
            }
        }

        Report(UpdateStage.Downloading, "업데이트 파일 다운로드 완료", 50);
    }

    private void ApplyExtractedFiles(string extractRoot)
    {
        string currentUpdaterName = Path.GetFileName(Application.ExecutablePath);
        string currentUpdaterBaseName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
        string[] files = Directory.GetFiles(extractRoot, "*", SearchOption.AllDirectories);
        int totalFiles = Math.Max(files.Length, 1);
        int processedFiles = 0;

        foreach (string sourceFile in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = Path.GetRelativePath(extractRoot, sourceFile);
            processedFiles++;

            if (ShouldSkipFile(relativePath, currentUpdaterName, currentUpdaterBaseName))
            {
                int skipPercent = 70 + (processedFiles * 25 / totalFiles);
                Report(UpdateStage.ApplyingFiles, $"건너뜀: {relativePath}", skipPercent);
                continue;
            }

            string destinationPath = Path.Combine(options.TargetDirectory, relativePath);
            string? destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            File.Copy(sourceFile, destinationPath, true);

            int percent = 70 + (processedFiles * 25 / totalFiles);
            Report(UpdateStage.ApplyingFiles, $"적용 중: {relativePath}", percent);
        }
    }

    private static bool ShouldSkipFile(string relativePath, string currentUpdaterName, string currentUpdaterBaseName)
    {
        string fileName = Path.GetFileName(relativePath);
        if (fileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (fileName.Equals(currentUpdaterName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (fileName.StartsWith(currentUpdaterBaseName + ".", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string GetEffectiveExtractRoot(string extractRoot, string targetExecutableName)
    {
        string[] executableMatches = Directory.GetFiles(extractRoot, targetExecutableName, SearchOption.AllDirectories);
        if (executableMatches.Length == 1)
        {
            return Path.GetDirectoryName(executableMatches[0]) ?? extractRoot;
        }

        string[] files = Directory.GetFiles(extractRoot, "*", SearchOption.TopDirectoryOnly);
        string[] directories = Directory.GetDirectories(extractRoot, "*", SearchOption.TopDirectoryOnly);
        if (files.Length == 0 && directories.Length == 1)
        {
            return directories[0];
        }

        return extractRoot;
    }

    private void RestartTargetApplication()
    {
        if (!File.Exists(options.TargetExecutablePath))
        {
            throw new FileNotFoundException("재실행할 실행 파일을 찾을 수 없습니다.", options.TargetExecutablePath);
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = options.TargetExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(options.TargetExecutablePath) ?? options.TargetDirectory,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    private void OpenReleaseNotesIfNeeded()
    {
        if (string.IsNullOrWhiteSpace(options.ReleaseNotesUrl))
        {
            return;
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = options.ReleaseNotesUrl,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    private void Report(UpdateStage stage, string message, int percent)
    {
        progress.Report(new UpdateProgressInfo
        {
            Stage = stage,
            Message = message,
            Percent = Math.Clamp(percent, 0, 100)
        });
    }

    private static void TryDeleteDirectory(string path, bool deleteIfEmptyOnly = false)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            if (deleteIfEmptyOnly && Directory.EnumerateFileSystemEntries(path).Any())
            {
                return;
            }

            Directory.Delete(path, true);
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }
}
