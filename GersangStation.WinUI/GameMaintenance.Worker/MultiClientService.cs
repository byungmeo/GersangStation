using Core;
using static Core.GameClientHelper;

namespace GameMaintenance.Worker;

internal sealed class MultiClientService(CancellationToken ct)
{
    private readonly CancellationToken _ct = ct;
    private static readonly HashSet<string> OnlineConfigFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CombineInfo.txt.txt", "PetSetting.dat", "AKinteractive.cfg"
    };
    private static readonly HashSet<string> TopLevelDirectoriesToSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        "Online",
        "XIGNCODE",
        "PatchTemp",
        "TempFiles",
        "GersangDown",
        "ScreenShots"
    };

    /// <summary>
    /// 특정 폴더를 목적 경로에 복사합니다.
    /// </summary>
    private void HardCopyDirectory(string sourceDirPath, string destDirPath, bool copySubDirs)
        => HardCopyDirectory(sourceDirPath, destDirPath, copySubDirs, overwriteExistingFiles: true);

    /// <summary>
    /// 특정 폴더를 목적 경로에 복사하되 파일 덮어쓰기 정책을 선택적으로 적용합니다.
    /// </summary>
    private void HardCopyDirectory(string sourceDirPath, string destDirPath, bool copySubDirs, bool overwriteExistingFiles)
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
            _ct.ThrowIfCancellationRequested();
            string tempPath = Path.Combine(destDirPath, file.Name);
            if (!overwriteExistingFiles && File.Exists(tempPath))
                continue;

            CopyFileWithSymbolAwareOverwrite(file.FullName, tempPath, overwriteExistingFiles);
        }

        if (!copySubDirs)
            return;

        foreach (DirectoryInfo subdir in dirs)
        {
            _ct.ThrowIfCancellationRequested();
            string tempPath = Path.Combine(destDirPath, subdir.Name);
            HardCopyDirectory(subdir.FullName, tempPath, copySubDirs, overwriteExistingFiles);
        }
    }

    /// <summary>
    /// 실 디렉터리가 필요할 때 기존 심볼릭 링크를 제거하고 실제 디렉터리를 보장합니다.
    /// </summary>
    private void EnsureRealDirectory(string dirPath)
    {
        if (Directory.Exists(dirPath))
        {
            SymbolDirectoryProbeResult symbolDirectoryResult = TryIsSymbolDirectory(dirPath);
            if (!symbolDirectoryResult.ProbeSuccess)
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
    /// 기존 심볼릭 링크/정션만 제거한 뒤 디렉터리 정션으로 연결합니다.
    /// </summary>
    private void RecreateJunctionDirectory(string sourceDirPath, string destDirPath)
    {
        DirectoryJunction.ValidatePaths(destDirPath, sourceDirPath);
        if (Directory.Exists(destDirPath))
        {
            SymbolDirectoryProbeResult symbolDirectoryResult = TryIsSymbolDirectory(destDirPath);
            if (!symbolDirectoryResult.ProbeSuccess)
            {
                throw new IOException(
                    $"Failed to inspect whether '{destDirPath}' is a symbolic directory.",
                    symbolDirectoryResult.Exception);
            }

            if (!symbolDirectoryResult.IsSymbolDirectory)
                throw new MultiClientSymbolicDirectoryConflictException(sourceDirPath, destDirPath);

            Directory.Delete(destDirPath);
        }
        else if (File.Exists(destDirPath))
        {
            File.Delete(destDirPath);
        }

        DirectoryJunction.Create(destDirPath, sourceDirPath);
    }

    /// <summary>
    /// 목적지 파일이 심볼릭 링크인 경우 먼저 제거한 뒤 복사합니다.
    /// </summary>
    private void CopyFileWithSymbolAwareOverwrite(string sourceFilePath, string destFilePath, bool overwrite)
    {
        if (!overwrite && File.Exists(destFilePath))
            return;

        if (overwrite && File.Exists(destFilePath))
        {
            SymbolFileProbeResult symbolFileResult = TryIsSymbolFile(destFilePath);
            if (!symbolFileResult.Success)
            {
                throw new IOException(
                    $"Failed to inspect whether '{destFilePath}' is a symbolic file.",
                    symbolFileResult.Exception);
            }

            if (symbolFileResult.IsSymbolFile)
                File.Delete(destFilePath);
        }

        _ct.ThrowIfCancellationRequested();
        File.Copy(sourceFilePath, destFilePath, overwrite);
    }

    /// <summary>
    /// Assets\Config의 기존 파일은 보존하고, 나머지 Assets 파일은 갱신합니다.
    /// </summary>
    private void CopyAssetsDirectoryPreservingConfig(string sourceAssetsPath, string destAssetsPath)
    {
        EnsureRealDirectory(destAssetsPath);

        foreach (string sourceFilePath in Directory.GetFiles(sourceAssetsPath))
        {
            _ct.ThrowIfCancellationRequested();
            string fileName = Path.GetFileName(sourceFilePath);
            string destFilePath = Path.Combine(destAssetsPath, fileName);
            CopyFileWithSymbolAwareOverwrite(sourceFilePath, destFilePath, overwrite: true);
        }

        foreach (string sourceDirPath in Directory.GetDirectories(sourceAssetsPath))
        {
            _ct.ThrowIfCancellationRequested();
            string dirName = new DirectoryInfo(sourceDirPath).Name;
            string destDirPath = Path.Combine(destAssetsPath, dirName);

            if (dirName.Equals("Config", StringComparison.OrdinalIgnoreCase))
            {
                HardCopyDirectory(sourceDirPath, destDirPath, copySubDirs: true, overwriteExistingFiles: false);
                continue;
            }

            RecreateJunctionDirectory(sourceDirPath, destDirPath);
        }
    }

    /*
     * Junction 방식 다클라 생성 정책
     * 1. 기존 Online\Map의 정션/심볼릭 링크는 재연결하고 일반 폴더 충돌은 거절
     * 2. 최상위 폴더 내 파일들은 모두 HardCopy(.tmp, .bmp, .dmp 확장자 파일은 아예 무시)
     * 3. XIGONCODE 폴더는 안의 내용물 포함 모두 HardCopy
     * 4. Online 직접 파일은 설정파일을 제외하고 덮어쓰며, 하위 폴더는 정션
     * 5. Assets\Config와 config.ln은 기존 파일을 보존하고, 없는 설정파일만 복사
    */

    private CreateSymbolMultiClientResult TryCreateSymbolClient(
        string orgInstallPath,
        string destPath,
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

        try
        {
            DirectoryJunction.ValidatePaths(destPath, orgInstallPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return CreateFailureResult(
                $"{clientPrefix}로컬 NTFS 경로와 정션 생성 대상을 확인해 주세요.",
                clientNumber,
                MultiClientCreationFailureStage.ValidateDestinationPath,
                ex,
                sourcePath: orgInstallPath,
                destinationPath: destPath);
        }

        string destOnlineMapPath = Path.Combine(destPath, "Online", "Map");
        if (Directory.Exists(destOnlineMapPath))
        {
            SymbolDirectoryProbeResult destMapProbeResult = TryIsSymbolDirectory(destOnlineMapPath);
            if (!destMapProbeResult.ProbeSuccess)
            {
                return CreateFailureResult(
                    $"{clientPrefix}기존 다클라 경로의 디렉터리 링크 상태를 확인하지 못했습니다.",
                    clientNumber,
                    MultiClientCreationFailureStage.ValidateDestinationPath,
                    destMapProbeResult.Exception,
                    sourcePath: orgInstallPath,
                    destinationPath: destPath);
            }

            if (!destMapProbeResult.IsSymbolDirectory)
            {
                string sourceOnlineMapPath = Path.Combine(orgInstallPath, "Online", "Map");
                return CreateFailureResult(
                    $"{clientPrefix}기존 다클라 경로에 정션으로 연결해야 하는 폴더가 일반 폴더로 존재합니다.",
                    clientNumber,
                    MultiClientCreationFailureStage.ValidateDestinationPath,
                    new MultiClientSymbolicDirectoryConflictException(sourceOnlineMapPath, destOnlineMapPath),
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
        catch (Exception ex) when (ex is not OperationCanceledException)
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
                bool overwrite = !OnlineConfigFileNames.Contains(Path.GetFileName(orgFilePath));

                if (!overwrite && File.Exists(destFilePath))
                    continue;

                CopyFileWithSymbolAwareOverwrite(orgFilePath, destFilePath, overwrite);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
                RecreateJunctionDirectory(eachDirPath, destDirPath);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CreateFailureResult(
                $"{clientPrefix}Online 하위 폴더 정션 생성 중 오류가 발생했습니다.",
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
        catch (Exception ex) when (ex is not OperationCanceledException)
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
                string dirName = new DirectoryInfo(eachDirPath).Name;
                string destDirPath = $"{destPath}\\{dirName}";

                if (TopLevelDirectoriesToSkip.Contains(dirName))
                {
                    // 심볼릭으로 생성하면 안 되는 폴더인데 심볼릭으로 존재하는 경우
                    SymbolDirectoryProbeResult result = TryIsSymbolDirectory(destDirPath);
                    if (result.ProbeSuccess && result.IsSymbolDirectory)
                    {
                        Directory.Delete(destDirPath);
                    }
                    continue;
                }

                if (dirName.Equals("Assets", StringComparison.OrdinalIgnoreCase))
                {
                    CopyAssetsDirectoryPreservingConfig(eachDirPath, destDirPath);
                    continue;
                }

                RecreateJunctionDirectory(eachDirPath, destDirPath);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
                bool overwrite = !Path.GetFileName(orgFilePath).Equals("config.ln", StringComparison.OrdinalIgnoreCase);

                if (!overwrite && File.Exists(destFilePath))
                    continue;

                CopyFileWithSymbolAwareOverwrite(orgFilePath, destFilePath, overwrite);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
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
    public CreateSymbolMultiClientResult TryCreateSymbolMultiClient(CreateSymbolMultiClientArgs args)
    {
        _ct.ThrowIfCancellationRequested();
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
    public bool CreateSymbolMultiClient(CreateSymbolMultiClientArgs args, out string reason)
    {
        CreateSymbolMultiClientResult result = TryCreateSymbolMultiClient(args);
        reason = result.Reason;
        return result.Success;
    }

    private CreateSymbolMultiClientResult CreateSuccessResult()
        => new(true, string.Empty, null, null, null);

    private CreateSymbolMultiClientResult CreateFailureResult(
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

