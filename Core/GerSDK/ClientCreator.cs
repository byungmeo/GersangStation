using System;
using System.ComponentModel;
using System.IO;
using static GerSDK.GamePathChecker;

namespace GerSDK;

/// <summary>
/// 원본 게임 폴더를 기준으로 다중 실행용 심볼릭 링크 클라이언트를 생성하는 기능을 제공합니다.
/// </summary>
public static class ClientCreator
{
    /// <summary>
    /// 원본 게임 폴더를 기준으로 다중 실행용 심볼릭 링크 클라이언트를 생성합니다.
    /// 기본 규칙을 사용하며, 세부 목록은 <see cref="SymbolClientCreateOptions"/>의 기본값을 따릅니다.
    /// 기본값은 원본 필수 폴더 <c>"DLL"</c>, 원본 필수 파일 <c>"Run.exe"</c>,
    /// 대상 기존 심볼릭 링크 허용 폴더 <c>"DLL"</c>,
    /// 제외 확장자 <c>".bmp"</c>, <c>".dmp"</c>, <c>".tmp"</c>,
    /// 사용자 지정 규칙 폴더 <c>"Assets"</c>,
    /// 무시 폴더 <c>"TempFiles"</c>, <c>"PatchTemp"</c>,
    /// 심볼릭 링크 생성 폴더 <c>"DLL"</c>, <c>"Online"</c>입니다.
    /// </summary>
    /// <param name="sourceGamePath">기준이 되는 원본 게임 폴더 경로입니다.</param>
    /// <param name="destGamePath">생성할 다중 실행용 대상 게임 폴더 경로입니다.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sourceGamePath"/> 또는 <paramref name="destGamePath"/>가 <see langword="null"/>인 경우 발생합니다.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// 경로가 비어 있거나 잘못되었거나, 원본 게임 구조가 요구 조건을 만족하지 않거나,
    /// 대상 경로가 원본과 같거나 중첩되었거나, 복사 및 링크 생성 과정에서 예외가 발생한 경우 발생합니다.
    /// 일부 경우 실제 I/O 실패 원인은 InnerException을 통해 확인할 수 있습니다.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="destGamePath"/>가 위치한 파일 시스템이 심볼릭 링크를 지원하지 않는 경우 발생합니다.
    /// </exception>
    public static void CreateSymbolClient(string sourceGamePath, string destGamePath)
    {
        CreateSymbolClient(sourceGamePath, destGamePath, null);
    }

    /// <summary>
    /// 원본 게임 폴더를 기준으로 다중 실행용 심볼릭 링크 클라이언트를 생성합니다.
    /// <paramref name="options"/>가 <see langword="null"/>이면 <see cref="SymbolClientCreateOptions"/>의 기본값을 사용합니다.
    /// </summary>
    /// <param name="sourceGamePath">기준이 되는 원본 게임 폴더 경로입니다.</param>
    /// <param name="destGamePath">생성할 다중 실행용 대상 게임 폴더 경로입니다.</param>
    /// <param name="options">필수 폴더, 필수 파일, 제외 확장자, 사용자 지정 규칙 등을 제어하는 옵션입니다.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="sourceGamePath"/> 또는 <paramref name="destGamePath"/>가 <see langword="null"/>인 경우 발생합니다.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// 경로가 비어 있거나 잘못되었거나, 원본 게임 구조가 요구 조건을 만족하지 않거나,
    /// 옵션 값이 잘못되었거나, 대상 경로가 원본과 같거나 중첩되었거나, 복사 및 링크 생성 과정에서 예외가 발생한 경우 발생합니다.
    /// 일부 경우 실제 I/O 실패 원인은 <see cref="Exception.InnerException"/>에서 확인할 수 있습니다.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// <paramref name="destGamePath"/>가 위치한 파일 시스템이 심볼릭 링크를 지원하지 않는 경우 발생합니다.
    /// </exception>
    public static void CreateSymbolClient(string sourceGamePath, string destGamePath, SymbolClientCreateOptions? options)
    {
        options ??= new SymbolClientCreateOptions();
        options.Validate();

        // 게임 설치 시 기본적으로 포함되는 폴더 및 파일 (v34200 기준)
        // 폴더: Online, DLL, Assets, XIGNCODE
        // 파일: AutopatchUpdater.exe, Gersang.exe, gersang.gcs, Korean.gts, Run.exe, run.ico, system.gcs, system.gts, zlib.dll, GersangKR.ini(서버별로 다름)
        // 하지만, 이는 매번 바뀔 수 있기 때문에 기본적으로 강한 제약은 걸지 않고 DLL 폴더만 확인한다.

        // CreateSymbolClient에서 다루는 GamePath 정책
        // 1. GamePath는 DLL 폴더가 심볼릭 링크인지 여부에 따라 SourceGamePath와 SymbolGamePath로 구분한다.
        // 2. SourceGamePath는 언제나 존재하며, 유효해야 한다. (DLL 폴더가 일반적인 형태로 존재해야 하며, Run.exe 파일이 존재해야 한다.)
        // 3. SymbolGamePath는 존재하지 않을 수 있으며, 이미 존재할 경우 SymbolGamePath로 구분될 수 있는 조건을 충족해야 한다.
        // 4. 단, SymbolGamePath에 DLL 폴더가 꼭 존재해야 할 이유는 없다. 어차피 생성 할 것이기 때문이다.

        //
        // SourceGamePath 검증
        //
        if (sourceGamePath is null)
            throw new ArgumentNullException(nameof(sourceGamePath));

        if (string.IsNullOrWhiteSpace(sourceGamePath))
            throw new ArgumentException("SourceGamePath is empty or consists only of white-space characters", nameof(sourceGamePath));

        try
        {
            sourceGamePath = FileSystemHelper.NormalizePath(sourceGamePath);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Failed get SourceGame full path", nameof(sourceGamePath), ex);
        }

        if (!Directory.Exists(sourceGamePath))
            throw new ArgumentException("SourceGamePath must be exists.", nameof(sourceGamePath));

        foreach (string sourceRequiredDirectoryName in options.RequiredSourceDirectoryNames)
        {
            string sourceRequiredDirectoryPath = Path.Combine(sourceGamePath, sourceRequiredDirectoryName);

            DirectoryInfo sourceRequiredDirectoryInfo;
            try
            {
                sourceRequiredDirectoryInfo = new DirectoryInfo(sourceRequiredDirectoryPath);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"An exception occurred while creating directoryInfo with sourceGamePath\\{sourceRequiredDirectoryName}", nameof(sourceGamePath), ex);
            }

            if (!sourceRequiredDirectoryInfo.Exists)
                throw new ArgumentException($"SourceGamePath must contained {sourceRequiredDirectoryName} directory.", nameof(sourceGamePath));
            if (sourceRequiredDirectoryInfo.LinkTarget is not null)
                throw new ArgumentException($"SourceGamePath contained {sourceRequiredDirectoryName} must not be symlink.", nameof(sourceGamePath));
        }

        foreach (string sourceRequiredFileName in options.RequiredSourceFileNames)
        {
            string sourceRequiredFilePath = Path.Combine(sourceGamePath, sourceRequiredFileName);

            FileInfo requiredFileInfo;
            try
            {
                requiredFileInfo = new FileInfo(sourceRequiredFilePath);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"An exception occurred while creating fileInfo with sourceGamePath\\{sourceRequiredFileName}", nameof(sourceGamePath), ex);
            }

            if (!requiredFileInfo.Exists)
                throw new ArgumentException($"SourceGamePath must contained {sourceRequiredFileName} file.", nameof(sourceGamePath));
        }

        //
        // DestGamePath 검증
        //
        if (destGamePath is null)
            throw new ArgumentNullException(nameof(destGamePath));

        if (string.IsNullOrWhiteSpace(destGamePath))
            throw new ArgumentException("DestGamePath is empty or consists only of white-space characters", nameof(destGamePath));

        try
        {
            destGamePath = FileSystemHelper.NormalizePath(destGamePath);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Failed get DestGame full path", nameof(destGamePath), ex);
        }

        // 두 경로는 부모/자식 관계가 될 수 없음 (무한 재귀 복사 방지)
        if (FileSystemHelper.IsSameOrNestedPath(sourceGamePath, destGamePath) ||
            FileSystemHelper.IsSameOrNestedPath(destGamePath, sourceGamePath))
        {
            throw new ArgumentException(
                "SourceGamePath and DestGamePath must not be the same path or nested paths.",
                nameof(destGamePath));
        }

        if (File.Exists(destGamePath))
        {
            throw new ArgumentException("DestGamePath must not be file path", nameof(destGamePath));
        }

        try
        {
            if (!IsSupportedSymlink(destGamePath))
            {
                throw new NotSupportedException("DestGamePath not supported symlink");
            }
        }
        catch (Win32Exception ex)
        {
            throw new ArgumentException("An exception occurred while checking if DestGamePath supports Symlink.", nameof(destGamePath), ex);
        }

        if (Directory.Exists(destGamePath))
        {
            foreach (string optionalDestinationSymbolDirectoryName in options.OptionalDestinationSymbolDirectoryNames)
            {
                string optionalDestinationSymbolDirectoryPath = Path.Combine(destGamePath, optionalDestinationSymbolDirectoryName);
                string sourceSymbolDirectoryPath = Path.Combine(sourceGamePath, optionalDestinationSymbolDirectoryName);

                if (File.Exists(optionalDestinationSymbolDirectoryPath))
                    throw new ArgumentException($"DestGamePath contained {optionalDestinationSymbolDirectoryName} must be symbolic link directory or not exists.", nameof(destGamePath));

                if (!Directory.Exists(optionalDestinationSymbolDirectoryPath))
                    continue;

                try
                {
                    if (!FileSystemHelper.IsDirectorySymbolicLinkPointingTo(optionalDestinationSymbolDirectoryPath, sourceSymbolDirectoryPath))
                    {
                        throw new ArgumentException($"DestGamePath contained {optionalDestinationSymbolDirectoryName} must be symbolic link pointing to sourceGamePath\\{optionalDestinationSymbolDirectoryName} or not exists.", nameof(destGamePath));
                    }
                }
                catch (ArgumentException ex) when (ex.ParamName != nameof(destGamePath))
                {
                    throw new ArgumentException($"An exception occurred while validating destGamePath\\{optionalDestinationSymbolDirectoryName}", nameof(destGamePath), ex);
                }
            }
        }

        //
        // 다클라 생성
        //
        try
        {
            Directory.CreateDirectory(destGamePath);
        }
        catch (Exception ex)
        {
            // 예방 못 한 예외:
            //   T:UnauthorizedAccessException:
            //     사용자에게 디렉터리를 만들 권한이 없는 경우
            throw new ArgumentException($"An exception occurred while creating directory with destGamePath", nameof(destGamePath), ex);
        }

        DirectoryInfo sourceDirInfo;
        try
        {
            // 게임 폴더 최상위에 있는 파일들은 일부 확장자를 제외하고 모두 복사한다
            sourceDirInfo = new(sourceGamePath);
            FileSystemHelper.CopyFilesInDirectory(sourceDirInfo, destGamePath, options.ExcludedFileExtensions);
        }
        catch (Exception ex)
        {
            // 예방 못 한 예외:
            //   T:UnauthorizedAccessException:
            //     대상 경로가 디렉터리이거나 파일 쓰기 권한 부족 또는 기존 파일이 읽기 전용일 때 발생
            //   T:System.Security.SecurityException:
            //     호출자에게 필요한 권한이 없는 경우
            throw new ArgumentException($"An exception occurred while copy files in top level game path", nameof(destGamePath), ex);
        }

        foreach (DirectoryInfo dir in sourceDirInfo.GetDirectories())
        {
            if (options.IgnoredDirectoryNames.Contains(dir.Name))
            {
                continue;
            }

            string destPath = Path.Combine(destGamePath, dir.Name);
            if (options.CustomDirectoryHandlers.TryGetValue(dir.Name, out SymbolClientCustomDirectoryHandler? value))
            {
                value(dir, destPath);
            }
            else if (options.SymbolicLinkDirectoryNames.Contains(dir.Name))
            {
                try
                {
                    FileSystemHelper.CreateOrReuseDirectorySymbolicLink(destPath, dir.FullName);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"An exception occurred while create symbol {dir.FullName} -> {destPath}", ex);
                }
            }
            else
            {
                // 별도로 표시하지 않았으면 무조건 깊은 복사
                FileSystemHelper.DeepCopyDirectory(dir, destPath);
            }
        }
    }
}
