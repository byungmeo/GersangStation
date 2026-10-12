using Core.Models;

namespace Core;

public static class GameClientHelper
{
    public const int MultiClientLayoutBoundaryVersion = 34100;
    public enum MultiClientLayoutPolicy
    {
        Legacy = 0,
        V34100OrLater = 1
    }

    /// <summary>
    /// 다클라 생성 실패 지점을 구분합니다.
    /// </summary>
    public enum MultiClientCreationFailureStage
    {
        ValidateMainInstallPath,
        ValidateDestinationPath,
        PrepareDestinationRoot,
        CopyOnlineFiles,
        LinkOnlineDirectories,
        CopyXigncode,
        CopyTopLevelDirectories,
        CopyTopLevelFiles
    }

    public enum SymbolProbeFailureStage
    {
        ResolveDriveRoot,
        ReadDriveFormat,
        ReadDirectoryAttributes,
        ReadFileAttributes
    }

    public enum InstallPathValidationFailureReason
    {
        EmptyPath,
        InvalidPathFormat,
        MissingDirectory,
        MissingRunExe,
        MissingOnlineMapDirectory,
        MissingVsnDat,
        ClonePathUsedAsMainPath,
        ServerFileMismatch,
        SymbolDirectoryProbeFailed
    }

    /// <summary>
    /// 다클라 생성 중 기술적 실패가 발생했을 때 단계와 경로 문맥을 함께 보존합니다.
    /// </summary>
    public sealed class MultiClientCreationException : IOException
    {
        public int ClientNumber { get; }
        public string SourcePath { get; }
        public string DestinationPath { get; }
        public MultiClientCreationFailureStage Stage { get; }

        public MultiClientCreationException(
            string message,
            int clientNumber,
            string sourcePath,
            string destinationPath,
            MultiClientCreationFailureStage stage,
            Exception innerException)
            : base(message, innerException)
        {
            ClientNumber = clientNumber;
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            Stage = stage;
        }
    }

    /// <summary>
    /// 다클라 대상에 심볼릭 링크로 대체해야 하는 폴더가 일반 폴더로 존재할 때 발생합니다.
    /// </summary>
    public sealed class MultiClientSymbolicDirectoryConflictException : IOException
    {
        public string SourcePath { get; }
        public string DestinationPath { get; }

        public MultiClientSymbolicDirectoryConflictException(string sourcePath, string destinationPath)
            : base($"심볼릭 링크로 생성해야 하는 폴더가 일반 폴더로 존재합니다. Path={destinationPath}")
        {
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
        }
    }

    /// <summary>
    /// 심볼릭 링크 지원 가능 여부 확인 결과를 포맷 및 실패 문맥과 함께 반환합니다.
    /// </summary>
    public sealed record SymbolSupportResult(
        bool Success,
        bool CanUseSymbol,
        string ResolvedFormat,
        SymbolProbeFailureStage? FailureStage,
        Exception? Exception);

    /// <summary>
    /// 디렉터리 심볼릭 링크 여부 확인 결과를 실패 문맥과 함께 반환합니다.
    /// </summary>
    public sealed record SymbolDirectoryProbeResult(
        bool ProbeSuccess,
        bool IsSymbolDirectory,
        SymbolProbeFailureStage? FailureStage,
        Exception? Exception);

    /// <summary>
    /// 파일 심볼릭 링크 여부 확인 결과를 실패 문맥과 함께 반환합니다.
    /// </summary>
    public sealed record SymbolFileProbeResult(
        bool Success,
        bool IsSymbolFile,
        SymbolProbeFailureStage? FailureStage,
        Exception? Exception);

    /// <summary>
    /// 설치 경로 유효성 검사 결과를 실패 사유와 예외 문맥까지 포함해 반환합니다.
    /// </summary>
    public sealed record InstallPathValidationResult(
        bool Success,
        string Reason,
        InstallPathValidationFailureReason? FailureReason,
        Exception? Exception);

    /// <summary>
    /// 현재 경로가 속해있는 드라이브의 포맷이 SymbolicLink를 사용할 수 있는지 여부를 반환합니다.
    /// </summary>
    /// <param name="anyPathInDrive">SymbolicLink 사용 가능 여부를 알고싶은 경로 문자열</param>
    /// <param name="resolvedFormat">드라이브 포맷 문자열</param>
    /// <returns>SymbolicLink 사용이 가능하면 true를 반환합니다.</returns>
    public static bool CanUseSymbol(string anyPathInDrive, out string resolvedFormat)
    {
        SymbolSupportResult result = TryCanUseSymbol(anyPathInDrive);
        resolvedFormat = result.ResolvedFormat;
        return result.CanUseSymbol;
    }

    /// <summary>
    /// 특정 폴더가 SymbolicLink 방식으로 생성된 것인지 여부를 반환합니다.
    /// </summary>
    /// <param name="dirPath">확인하고 싶은 폴더 경로</param>
    /// <returns>SymbolicLink라면 true를 반환합니다.</returns>
    public static bool IsSymbolDirectory(string dirPath)
    {
        return TryIsSymbolDirectory(dirPath).IsSymbolDirectory;
    }

    /// <summary>
    /// 특정 파일이 SymbolicLink 방식으로 생성된 것인지 여부를 반환합니다.
    /// </summary>
    /// <param name="filePath">확인하고 싶은 파일 경로</param>
    /// <returns>SymbolicLink라면 true를 반환합니다.</returns>
    public static bool IsSymbolFile(string filePath)
    {
        return TryIsSymbolFile(filePath).IsSymbolFile;
    }

    /// <summary>
    /// 현재 경로가 속한 드라이브에서 심볼릭 링크를 사용할 수 있는지 상세 결과와 함께 확인합니다.
    /// </summary>
    public static SymbolSupportResult TryCanUseSymbol(string anyPathInDrive)
    {
        const string unknownFormat = "알 수 없음";

        try
        {
            string normalizedPath = Path.GetFullPath((anyPathInDrive ?? string.Empty).Trim());
            string root = Path.GetPathRoot(normalizedPath) ?? string.Empty;
            if (root.Length == 0)
            {
                return new SymbolSupportResult(
                    Success: false,
                    CanUseSymbol: false,
                    ResolvedFormat: unknownFormat,
                    FailureStage: SymbolProbeFailureStage.ResolveDriveRoot,
                    Exception: null);
            }

            try
            {
                var di = new DriveInfo(root);
                string resolvedFormat = (di.DriveFormat ?? string.Empty).ToUpperInvariant();
                bool canUseSymbol = resolvedFormat == "NTFS" || resolvedFormat == "UDF";
                return new SymbolSupportResult(true, canUseSymbol, resolvedFormat, null, null);
            }
            catch (Exception ex)
            {
                return new SymbolSupportResult(
                    Success: false,
                    CanUseSymbol: false,
                    ResolvedFormat: unknownFormat,
                    FailureStage: SymbolProbeFailureStage.ReadDriveFormat,
                    Exception: ex);
            }
        }
        catch (Exception ex)
        {
            return new SymbolSupportResult(
                Success: false,
                CanUseSymbol: false,
                ResolvedFormat: unknownFormat,
                FailureStage: SymbolProbeFailureStage.ResolveDriveRoot,
                Exception: ex);
        }
    }

    /// <summary>
    /// 특정 폴더가 심볼릭 링크 방식으로 생성된 것인지 상세 결과와 함께 반환합니다.
    /// </summary>
    public static SymbolDirectoryProbeResult TryIsSymbolDirectory(string dirPath)
    {
        SymbolSupportResult symbolSupportResult = TryCanUseSymbol(dirPath);
        if (!symbolSupportResult.Success)
        {
            return new SymbolDirectoryProbeResult(
                ProbeSuccess: false,
                IsSymbolDirectory: false,
                FailureStage: symbolSupportResult.FailureStage,
                Exception: symbolSupportResult.Exception);
        }

        if (!symbolSupportResult.CanUseSymbol)
            return new SymbolDirectoryProbeResult(true, false, null, null);

        try
        {
            var di = new DirectoryInfo(dirPath);
            if (!di.Exists)
                return new SymbolDirectoryProbeResult(false, false, SymbolProbeFailureStage.ReadDirectoryAttributes, null);
            bool isSymbolDirectory = (di.Attributes & FileAttributes.ReparsePoint) != 0;
            return new SymbolDirectoryProbeResult(true, isSymbolDirectory, null, new DirectoryNotFoundException(dirPath));
        }
        catch (Exception ex)
        {
            return new SymbolDirectoryProbeResult(
                ProbeSuccess: false,
                IsSymbolDirectory: false,
                FailureStage: SymbolProbeFailureStage.ReadDirectoryAttributes,
                Exception: ex);
        }
    }

    /// <summary>
    /// 특정 파일이 심볼릭 링크 방식으로 생성된 것인지 상세 결과와 함께 반환합니다.
    /// </summary>
    public static SymbolFileProbeResult TryIsSymbolFile(string filePath)
    {
        SymbolSupportResult symbolSupportResult = TryCanUseSymbol(filePath);
        if (!symbolSupportResult.Success)
        {
            return new SymbolFileProbeResult(
                Success: false,
                IsSymbolFile: false,
                FailureStage: symbolSupportResult.FailureStage,
                Exception: symbolSupportResult.Exception);
        }

        if (!symbolSupportResult.CanUseSymbol)
            return new SymbolFileProbeResult(true, false, null, null);

        try
        {
            var fi = new FileInfo(filePath);
            bool isSymbolFile = (fi.Attributes & FileAttributes.ReparsePoint) != 0;
            return new SymbolFileProbeResult(true, isSymbolFile, null, null);
        }
        catch (Exception ex)
        {
            return new SymbolFileProbeResult(
                Success: false,
                IsSymbolFile: false,
                FailureStage: SymbolProbeFailureStage.ReadFileAttributes,
                Exception: ex);
        }
    }

    /// <summary>
    /// 특정 원본 클라이언트 경로가 현재 문맥에서 유효한지 여부를 반환합니다.
    /// </summary>
    /// <param name="server">게임 서버 종류</param>
    /// <param name="installPath">클라이언트 경로(원본만 가능)</param>
    /// <param name="reason">유효성 검사 성공 또는 실패 사유</param>
    public static bool IsValidInstallPath(GameServer server, string installPath, out string reason)
    {
        InstallPathValidationResult result = TryValidateInstallPath(server, installPath);
        reason = result.Reason;
        return result.Success;
    }

    /// <summary>
    /// 서버 구분 없이 메인 클라이언트 경로 유효성을 검사합니다.
    /// </summary>
    public static bool IsValidInstallPath(string installPath, out string reason)
    {
        InstallPathValidationResult result = TryValidateInstallPath(installPath);
        reason = result.Reason;
        return result.Success;
    }

    /// <summary>
    /// 복제 클라이언트 경로가 현재 문맥에서 유효한지 여부를 반환합니다.
    /// </summary>
    public static bool IsValidCloneInstallPath(GameServer server, string installPath, out string reason)
    {
        InstallPathValidationResult result = TryValidateCloneInstallPath(server, installPath);
        reason = result.Reason;
        return result.Success;
    }

    /// <summary>
    /// 메인 클라이언트 경로 유효성을 상세 결과와 함께 검사합니다.
    /// </summary>
    public static InstallPathValidationResult TryValidateInstallPath(GameServer server, string installPath)
        => ValidateInstallPathCore(server, installPath, allowSymbolDirectory: false);

    /// <summary>
    /// 서버 구분 없이 메인 클라이언트 경로 유효성을 상세 결과와 함께 검사합니다.
    /// </summary>
    public static InstallPathValidationResult TryValidateInstallPath(string installPath)
        => ValidateInstallPathCore(server: null, installPath, allowSymbolDirectory: false);

    /// <summary>
    /// 복제 클라이언트 경로 유효성을 상세 결과와 함께 검사합니다.
    /// </summary>
    public static InstallPathValidationResult TryValidateCloneInstallPath(GameServer server, string installPath)
        => ValidateInstallPathCore(server, installPath, allowSymbolDirectory: true);

    private static InstallPathValidationResult ValidateInstallPathCore(GameServer? server, string installPath, bool allowSymbolDirectory)
    {
        string rawPath = (installPath ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(rawPath))
        {
            return CreateInstallPathFailure("❌ 설치 경로가 비어있습니다.", InstallPathValidationFailureReason.EmptyPath);
        }

        if (!Path.IsPathFullyQualified(rawPath))
        {
            return CreateInstallPathFailure(
                "❌ 설치 경로 형식이 올바르지 않습니다.",
                InstallPathValidationFailureReason.InvalidPathFormat);
        }

        string path;
        try
        {
            path = Path.GetFullPath(rawPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CreateInstallPathFailure(
                "❌ 설치 경로 형식이 올바르지 않습니다.",
                InstallPathValidationFailureReason.InvalidPathFormat,
                ex);
        }

        if (!Directory.Exists(path))
        {
            return CreateInstallPathFailure("❌ 존재하지 않는 폴더입니다.", InstallPathValidationFailureReason.MissingDirectory);
        }

        string runExe = Path.Combine(path, "Run.exe");
        if (!File.Exists(runExe))
        {
            return CreateInstallPathFailure("❌ 거상 실행 파일(Run.exe)을 찾지 못했습니다.", InstallPathValidationFailureReason.MissingRunExe);
        }

        string onlineMapDir = Path.Combine(path, "Online", "Map");
        if (!Directory.Exists(onlineMapDir))
        {
            return CreateInstallPathFailure(
                @"❌ 거상 기본 폴더(\Online\Map)를 찾지 못했습니다.",
                InstallPathValidationFailureReason.MissingOnlineMapDirectory);
        }

        string vsnPath = Path.Combine(path, "Online", "vsn.dat");
        if (!File.Exists(vsnPath))
        {
            return CreateInstallPathFailure(
                @"❌ 거상 버전 파일(\Online\vsn.dat)을 찾지 못했습니다.",
                InstallPathValidationFailureReason.MissingVsnDat);
        }

        if (!allowSymbolDirectory)
        {
            SymbolDirectoryProbeResult symbolDirectoryResult = TryIsSymbolDirectory(onlineMapDir);
            if (!symbolDirectoryResult.ProbeSuccess)
            {
                return CreateInstallPathFailure(
                    "❌ 설치 경로의 심볼릭 링크 상태를 확인하지 못했습니다.",
                    InstallPathValidationFailureReason.SymbolDirectoryProbeFailed,
                    symbolDirectoryResult.Exception);
            }

            if (symbolDirectoryResult.IsSymbolDirectory)
            {
                return CreateInstallPathFailure(
                    "❌ 메인 거상 경로가 아닙니다. 다클라 경로는 메인 경로가 될 수 없습니다.",
                    InstallPathValidationFailureReason.ClonePathUsedAsMainPath);
            }
        }

        if (server is GameServer gameServer)
        {
            string serverIni = Path.Combine(path, GameServerHelper.GetServerFileName(gameServer));
            if (!File.Exists(serverIni))
            {
                return CreateInstallPathFailure(
                    $"❌ 선택한 경로는 {GameServerHelper.GetServerDisplayName(gameServer)} 경로가 아닙니다.",
                    InstallPathValidationFailureReason.ServerFileMismatch);
            }
        }

        return new InstallPathValidationResult(true, string.Empty, null, null);
    }

    private static InstallPathValidationResult CreateInstallPathFailure(
        string reason,
        InstallPathValidationFailureReason failureReason,
        Exception? exception = null)
        => new(false, reason, failureReason, exception);

}

/// <summary>
/// 다클라 생성 시도 결과를 UI와 상위 오케스트레이션 계층으로 전달합니다.
/// </summary>
public sealed record CreateSymbolMultiClientResult(
    bool Success,
    string Reason,
    int? ClientNumber,
    GameClientHelper.MultiClientCreationFailureStage? FailureStage,
    Exception? Exception);

public sealed class CreateSymbolMultiClientArgs
{
    public string InstallPath { get; set; } = string.Empty;
    public string DestPath2 { get; set; } = string.Empty;
    public string DestPath3 { get; set; } = string.Empty;
    public bool OverwriteConfig { get; set; }
    public GameClientHelper.MultiClientLayoutPolicy LayoutPolicy { get; set; } = GameClientHelper.MultiClientLayoutPolicy.Legacy;
}
