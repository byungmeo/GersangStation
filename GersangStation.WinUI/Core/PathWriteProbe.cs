namespace Core;

/// <summary>
/// 디렉터리 쓰기 가능 여부 사전 점검 결과를 대상/실제 확인 경로와 함께 반환합니다.
/// </summary>
public sealed record DirectoryWriteProbeResult(
    bool Success,
    bool CanWrite,
    string TargetPath,
    string ProbePath,
    Exception? Exception);

/// <summary>
/// 파일 작업 전에 현재 사용자로 대상 디렉터리에 쓸 수 있는지 실제 쓰기 시도로 확인합니다.
/// </summary>
public static class PathWriteProbe
{
    /// <summary>
    /// 지정한 디렉터리 경로에 대해 현재 프로세스가 파일을 생성할 수 있는지 점검합니다.
    /// </summary>
    public static DirectoryWriteProbeResult TryProbeDirectoryWriteAccess(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new DirectoryWriteProbeResult(
                Success: false,
                CanWrite: false,
                TargetPath: path ?? string.Empty,
                ProbePath: string.Empty,
                Exception: new ArgumentException("path is required.", nameof(path)));
        }

        string normalizedTargetPath;
        try
        {
            normalizedTargetPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex)
        {
            return new DirectoryWriteProbeResult(
                Success: false,
                CanWrite: false,
                TargetPath: path,
                ProbePath: string.Empty,
                Exception: ex);
        }

        string probePath;
        try
        {
            probePath = ResolveExistingProbeDirectory(normalizedTargetPath);
        }
        catch (Exception ex)
        {
            return new DirectoryWriteProbeResult(
                Success: false,
                CanWrite: false,
                TargetPath: normalizedTargetPath,
                ProbePath: string.Empty,
                Exception: ex);
        }

        try
        {
            string probeFilePath = Path.Combine(probePath, $".gs-write-probe-{Guid.NewGuid():N}.tmp");
            using (FileStream stream = new(
                probeFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough))
            {
                stream.WriteByte(0);
            }

            File.Delete(probeFilePath);
            return new DirectoryWriteProbeResult(
                Success: true,
                CanWrite: true,
                TargetPath: normalizedTargetPath,
                ProbePath: probePath,
                Exception: null);
        }
        catch (Exception ex)
        {
            return new DirectoryWriteProbeResult(
                Success: false,
                CanWrite: false,
                TargetPath: normalizedTargetPath,
                ProbePath: probePath,
                Exception: ex);
        }
    }

    /// <summary>
    /// 실제 쓰기 테스트를 수행할 기존 디렉터리를 찾습니다.
    /// 대상이 아직 없으면 가장 가까운 기존 부모를 사용합니다.
    /// </summary>
    private static string ResolveExistingProbeDirectory(string normalizedTargetPath)
    {
        string currentPath = normalizedTargetPath;
        while (!string.IsNullOrWhiteSpace(currentPath))
        {
            if (Directory.Exists(currentPath))
                return currentPath;

            string? parent = Path.GetDirectoryName(currentPath);
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, currentPath, StringComparison.Ordinal))
                break;

            currentPath = parent;
        }

        string? root = Path.GetPathRoot(normalizedTargetPath);
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            return root;

        throw new DirectoryNotFoundException($"No existing parent directory could be resolved for '{normalizedTargetPath}'.");
    }
}
