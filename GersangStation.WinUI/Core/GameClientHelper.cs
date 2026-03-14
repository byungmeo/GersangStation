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
        ReadDirectoryAttributes
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
        bool Success,
        bool IsSymbolDirectory,
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
    /// 특정 폴더가 SymbolicLink 방식으로 생선된 것인지 여부를 반환합니다.
    /// </summary>
    /// <param name="dirPath">확인하고 싶은 폴더 경로</param>
    /// <returns>SymbolicLink라면 true를 반환합니다.</returns>
    public static bool IsSymbolDirectory(string dirPath)
    {
        return TryIsSymbolDirectory(dirPath).IsSymbolDirectory;
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
        try
        {
            var di = new DirectoryInfo(dirPath);
            bool isSymbolDirectory = (di.Attributes & FileAttributes.ReparsePoint) != 0;
            return new SymbolDirectoryProbeResult(true, isSymbolDirectory, null, null);
        }
        catch (Exception ex)
        {
            return new SymbolDirectoryProbeResult(
                Success: false,
                IsSymbolDirectory: false,
                FailureStage: SymbolProbeFailureStage.ReadDirectoryAttributes,
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
            if (!symbolDirectoryResult.Success)
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

    /// <summary>
    /// 특정 폴더를 목적 경로에 복사합니다.
    /// </summary>
    private static void HardCopyDirectory(string sourceDirPath, string destDirPath, bool copySubDirs)
        => HardCopyDirectory(sourceDirPath, destDirPath, copySubDirs, overwriteExistingFiles: true);

    /// <summary>
    /// 특정 폴더를 목적 경로에 복사하되 파일 덮어쓰기 정책을 선택적으로 적용합니다.
    /// </summary>
    private static void HardCopyDirectory(string sourceDirPath, string destDirPath, bool copySubDirs, bool overwriteExistingFiles)
    {
        DirectoryInfo dir = new(sourceDirPath);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException(
                "Source directory does not exist or could not be found: "
                + sourceDirPath);
        }

        DirectoryInfo[] dirs = dir.GetDirectories();
        EnsureRealDirectory(destDirPath);

        FileInfo[] files = dir.GetFiles();
        foreach (FileInfo file in files)
        {
            string tempPath = Path.Combine(destDirPath, file.Name);
            if (!overwriteExistingFiles && File.Exists(tempPath))
                continue;

            file.CopyTo(tempPath, overwriteExistingFiles);
        }

        if (!copySubDirs)
            return;

        foreach (DirectoryInfo subdir in dirs)
        {
            string tempPath = Path.Combine(destDirPath, subdir.Name);
            HardCopyDirectory(subdir.FullName, tempPath, copySubDirs, overwriteExistingFiles);
        }
    }

    /// <summary>
    /// 실 디렉터리가 필요할 때 기존 심볼릭 링크를 제거하고 실제 디렉터리를 보장합니다.
    /// </summary>
    private static void EnsureRealDirectory(string dirPath)
    {
        if (Directory.Exists(dirPath))
        {
            SymbolDirectoryProbeResult symbolDirectoryResult = TryIsSymbolDirectory(dirPath);
            if (!symbolDirectoryResult.Success)
            {
                throw new IOException(
                    $"Failed to inspect whether '{dirPath}' is a symbolic directory.",
                    symbolDirectoryResult.Exception);
            }

            if (symbolDirectoryResult.IsSymbolDirectory)
                Directory.Delete(dirPath);
        }
        else if (File.Exists(dirPath))
            File.Delete(dirPath);

        Directory.CreateDirectory(dirPath);
    }

    /// <summary>
    /// 기존 대상이 있으면 정리한 뒤 디렉터리 심볼릭 링크를 다시 만듭니다.
    /// </summary>
    private static void RecreateSymbolicDirectory(string sourceDirPath, string destDirPath)
    {
        if (Directory.Exists(destDirPath))
            Directory.Delete(destDirPath);
        else if (File.Exists(destDirPath))
            File.Delete(destDirPath);

        Directory.CreateSymbolicLink(destDirPath, sourceDirPath);
    }

    /// <summary>
    /// 34100 이후 레이아웃에서 Assets\Config만 설정파일 덮어쓰기 정책을 적용해 재구성합니다.
    /// </summary>
    private static void CopyAssetsDirectoryWithConfigPolicy(string sourceAssetsPath, string destAssetsPath, bool overwriteConfig)
    {
        EnsureRealDirectory(destAssetsPath);

        foreach (string sourceFilePath in Directory.GetFiles(sourceAssetsPath))
        {
            string fileName = Path.GetFileName(sourceFilePath);
            string destFilePath = Path.Combine(destAssetsPath, fileName);
            File.Copy(sourceFilePath, destFilePath, overwrite: true);
        }

        foreach (string sourceDirPath in Directory.GetDirectories(sourceAssetsPath))
        {
            string dirName = new DirectoryInfo(sourceDirPath).Name;
            string destDirPath = Path.Combine(destAssetsPath, dirName);

            if (dirName.Equals("Config", StringComparison.OrdinalIgnoreCase))
            {
                HardCopyDirectory(sourceDirPath, destDirPath, copySubDirs: true, overwriteExistingFiles: overwriteConfig);
                continue;
            }

            RecreateSymbolicDirectory(sourceDirPath, destDirPath);
        }
    }

    /*
     * Symbol 방식 다클라 생성 정책
     * 1. 목적지 경로가 비어있지 않고 Online\Map이 Symbol이 아니라면 실패(삭제 안내)
     * 2. 최상위 폴더 내 파일들은 모두 HardCopy(.tmp, .bmp, .dmp 확장자 파일은 아예 무시)
     * 3. XIGONCODE 폴더는 안의 내용물 포함 모두 HardCopy
     * 4. Legacy 정책: Online 직접 파일은 설정파일만 overwriteConfig를 따르고, 하위 폴더는 Symbol
     * 5. 34100+ 정책: Online 직접 파일은 모두 HardCopy 덮어쓰기, Assets\Config 전체는 overwriteConfig 정책으로 HardCopy
    */

    private static CreateSymbolMultiClientResult TryCreateSymbolClient(
        string orgInstallPath,
        string destPath,
        bool overwriteConfig,
        MultiClientLayoutPolicy layoutPolicy,
        int clientNumber,
        string clientPrefix)
    {
        orgInstallPath = (orgInstallPath ?? string.Empty).Trim();
        InstallPathValidationResult mainPathValidation = TryValidateInstallPath(orgInstallPath);
        if (!mainPathValidation.Success)
        {
            return CreateFailureResult(
                $"{clientPrefix}유효하지 않은 메인 클라 경로: {mainPathValidation.Reason}",
                clientNumber,
                MultiClientCreationFailureStage.ValidateMainInstallPath,
                mainPathValidation.Exception,
                sourcePath: orgInstallPath,
                destinationPath: destPath);
        }

        destPath = (destPath ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(destPath))
        {
            return CreateFailureResult(
                $"{clientPrefix}유효하지 않은 목적지 경로",
                clientNumber,
                MultiClientCreationFailureStage.ValidateDestinationPath,
                sourcePath: orgInstallPath,
                destinationPath: destPath);
        }

        try
        {
            destPath = Path.GetFullPath(destPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CreateFailureResult(
                $"{clientPrefix}목적지 경로 형식이 올바르지 않습니다.",
                clientNumber,
                MultiClientCreationFailureStage.ValidateDestinationPath,
                ex,
                sourcePath: orgInstallPath,
                destinationPath: destPath);
        }

        SymbolSupportResult symbolSupportResult = TryCanUseSymbol(destPath);
        if (!symbolSupportResult.Success)
        {
            return CreateFailureResult(
                $"{clientPrefix}목적지 드라이브의 심볼릭 링크 지원 여부를 확인하지 못했습니다.",
                clientNumber,
                MultiClientCreationFailureStage.ValidateDestinationPath,
                symbolSupportResult.Exception,
                sourcePath: orgInstallPath,
                destinationPath: destPath);
        }

        if (!symbolSupportResult.CanUseSymbol)
        {
            return CreateFailureResult(
                $"{clientPrefix}Symbol을 지원하지 않는 드라이브입니다. Format={symbolSupportResult.ResolvedFormat}",
                clientNumber,
                MultiClientCreationFailureStage.ValidateDestinationPath,
                sourcePath: orgInstallPath,
                destinationPath: destPath);
        }

        string destOnlineMapPath = Path.Combine(destPath, "Online", "Map");
        if (Directory.Exists(destOnlineMapPath))
        {
            SymbolDirectoryProbeResult destMapProbeResult = TryIsSymbolDirectory(destOnlineMapPath);
            if (!destMapProbeResult.Success)
            {
                return CreateFailureResult(
                    $"{clientPrefix}기존 다클라 경로의 심볼릭 링크 상태를 확인하지 못했습니다.",
                    clientNumber,
                    MultiClientCreationFailureStage.ValidateDestinationPath,
                    destMapProbeResult.Exception,
                    sourcePath: orgInstallPath,
                    destinationPath: destPath);
            }

            if (!destMapProbeResult.IsSymbolDirectory)
            {
                return CreateFailureResult(
                    $"{clientPrefix}이미 경로에 복사-붙여넣기로 생성한 클라이언트 존재",
                    clientNumber,
                    MultiClientCreationFailureStage.ValidateDestinationPath,
                    sourcePath: orgInstallPath,
                    destinationPath: destPath);
            }
        }

        string orgOnlinePath = orgInstallPath + @"\Online";
        if (!Directory.Exists(orgOnlinePath))
        {
            return CreateFailureResult(
                $"{clientPrefix}메인 클라 경로에 Online 폴더가 없음",
                clientNumber,
                MultiClientCreationFailureStage.ValidateMainInstallPath,
                sourcePath: orgInstallPath,
                destinationPath: destPath);
        }

        try
        {
            Directory.CreateDirectory(destPath);
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                $"{clientPrefix}다클라 대상 폴더를 준비하지 못했습니다.",
                clientNumber,
                MultiClientCreationFailureStage.PrepareDestinationRoot,
                ex,
                orgInstallPath,
                destPath);
        }

        string destOnlinePath = destPath + @"\Online";

        try
        {
            Directory.CreateDirectory(destOnlinePath);

            foreach (string orgFilePath in Directory.GetFiles(orgOnlinePath))
            {
                string fileName = orgFilePath.Substring(orgFilePath.LastIndexOf('\\'));
                string destFilePath = destOnlinePath + fileName;
                bool overwrite = layoutPolicy == MultiClientLayoutPolicy.V34100OrLater;
                if (layoutPolicy == MultiClientLayoutPolicy.Legacy)
                {
                    bool isConfig = fileName is @"\CombineInfo.txt.txt" or @"\PetSetting.dat" or @"\AKinteractive.cfg";
                    overwrite = !isConfig || overwriteConfig;
                }

                if (!overwrite && File.Exists(destFilePath))
                    continue;

                File.Copy(orgFilePath, destFilePath, overwrite);
            }
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                $"{clientPrefix}Online 폴더 파일 복사 중 오류가 발생했습니다.",
                clientNumber,
                MultiClientCreationFailureStage.CopyOnlineFiles,
                ex,
                orgInstallPath,
                destPath);
        }

        try
        {
            foreach (string eachDirPath in Directory.GetDirectories(orgOnlinePath))
            {
                string? dirName = new DirectoryInfo(eachDirPath).Name;
                string destDirPath = $"{destOnlinePath}\\{dirName}";
                RecreateSymbolicDirectory(eachDirPath, destDirPath);
            }
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                $"{clientPrefix}Online 하위 폴더 심볼릭 링크 생성 중 오류가 발생했습니다.",
                clientNumber,
                MultiClientCreationFailureStage.LinkOnlineDirectories,
                ex,
                orgInstallPath,
                destPath);
        }

        try
        {
            HardCopyDirectory(orgInstallPath + @"\XIGNCODE", destPath + @"\XIGNCODE", true);
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                $"{clientPrefix}XIGNCODE 폴더 복사 중 오류가 발생했습니다.",
                clientNumber,
                MultiClientCreationFailureStage.CopyXigncode,
                ex,
                orgInstallPath,
                destPath);
        }

        try
        {
            foreach (string eachDirPath in Directory.GetDirectories(orgInstallPath))
            {
                string? dirName = new DirectoryInfo(eachDirPath).Name;
                if (dirName == "XIGNCODE" || dirName == "Online")
                    continue;

                string destDirPath = $"{destPath}\\{dirName}";
                if (layoutPolicy == MultiClientLayoutPolicy.V34100OrLater
                    && dirName == "Assets")
                {
                    CopyAssetsDirectoryWithConfigPolicy(eachDirPath, destDirPath, overwriteConfig);
                    continue;
                }

                RecreateSymbolicDirectory(eachDirPath, destDirPath);
            }
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                $"{clientPrefix}최상위 폴더 구조 복제 중 오류가 발생했습니다.",
                clientNumber,
                MultiClientCreationFailureStage.CopyTopLevelDirectories,
                ex,
                orgInstallPath,
                destPath);
        }

        try
        {
            foreach (string orgFilePath in Directory.GetFiles(orgInstallPath))
            {
                string fileName = orgFilePath.Substring(orgFilePath.LastIndexOf('\\'));
                string extension = Path.GetExtension(orgFilePath);
                if (extension == ".tmp" || extension == ".bmp" || extension == ".dmp")
                    continue;

                string destFilePath = destPath + fileName;
                bool overwrite = layoutPolicy == MultiClientLayoutPolicy.V34100OrLater;
                if (layoutPolicy == MultiClientLayoutPolicy.Legacy)
                {
                    bool isConfig = fileName is @"\config.ln";
                    overwrite = !isConfig || overwriteConfig;
                }

                if (!overwrite && File.Exists(destFilePath))
                    continue;

                File.Copy(orgFilePath, destFilePath, overwrite);
            }
        }
        catch (Exception ex)
        {
            return CreateFailureResult(
                $"{clientPrefix}최상위 파일 복사 중 오류가 발생했습니다.",
                clientNumber,
                MultiClientCreationFailureStage.CopyTopLevelFiles,
                ex,
                orgInstallPath,
                destPath);
        }

        return CreateSuccessResult();
    }

    /// <summary>
    /// 다클라 생성 결과를 상세 문맥과 함께 반환합니다.
    /// </summary>
    public static CreateSymbolMultiClientResult TryCreateSymbolMultiClient(CreateSymbolMultiClientArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        InstallPathValidationResult mainPathValidation = TryValidateInstallPath(args.InstallPath);
        if (!mainPathValidation.Success)
        {
            return CreateFailureResult(
                $"올바르지 않은 메인 클라 경로: {mainPathValidation.Reason}",
                clientNumber: null,
                MultiClientCreationFailureStage.ValidateMainInstallPath,
                mainPathValidation.Exception,
                sourcePath: args.InstallPath);
        }

        if (!string.IsNullOrEmpty(args.DestPath2))
        {
            CreateSymbolMultiClientResult result = TryCreateSymbolClient(
                args.InstallPath,
                args.DestPath2,
                args.OverwriteConfig,
                args.LayoutPolicy,
                clientNumber: 2,
                clientPrefix: "2클라 생성 실패: ");
            if (!result.Success)
                return result;
        }

        if (!string.IsNullOrEmpty(args.DestPath3))
        {
            CreateSymbolMultiClientResult result = TryCreateSymbolClient(
                args.InstallPath,
                args.DestPath3,
                args.OverwriteConfig,
                args.LayoutPolicy,
                clientNumber: 3,
                clientPrefix: "3클라 생성 실패: ");
            if (!result.Success)
                return result;
        }

        return CreateSuccessResult();
    }

    /// <summary>
    /// 다클라를 생성하고, 실패 시 기존 호출부 호환을 위해 bool과 reason으로 결과를 돌려줍니다.
    /// </summary>
    public static bool CreateSymbolMultiClient(CreateSymbolMultiClientArgs args, out string reason)
    {
        CreateSymbolMultiClientResult result = TryCreateSymbolMultiClient(args);
        reason = result.Reason;
        return result.Success;
    }

    private static CreateSymbolMultiClientResult CreateSuccessResult()
        => new(true, string.Empty, null, null, null);

    private static InstallPathValidationResult CreateInstallPathFailure(
        string reason,
        InstallPathValidationFailureReason failureReason,
        Exception? exception = null)
        => new(false, reason, failureReason, exception);

    private static CreateSymbolMultiClientResult CreateFailureResult(
        string reason,
        int? clientNumber,
        MultiClientCreationFailureStage stage,
        Exception? exception = null,
        string sourcePath = "",
        string destinationPath = "")
    {
        Exception? wrappedException = exception is null
            ? null
            : new MultiClientCreationException(
                reason,
                clientNumber ?? 0,
                sourcePath,
                destinationPath,
                stage,
                exception);

        return new CreateSymbolMultiClientResult(false, reason, clientNumber, stage, wrappedException);
    }
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
