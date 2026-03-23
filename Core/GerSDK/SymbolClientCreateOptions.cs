using System;
using System.Collections.Generic;
using System.IO;

namespace GerSDK;

/// <summary>
/// <see cref="ClientCreator.CreateSymbolClient(string, string, SymbolClientCreateOptions?)"/>의 동작을 제어하는 옵션입니다.
/// 각 컬렉션은 자유롭게 <c>Add</c>, <c>Remove</c>, <c>Clear</c> 할 수 있으며, 생성자에서는 기본값이 채워집니다.
/// </summary>
public sealed class SymbolClientCreateOptions
{
    /// <summary>
    /// 원본 게임 경로에 반드시 존재해야 하는 폴더 이름 목록입니다.
    /// 기본값은 <c>"DLL"</c>입니다.
    /// 여기에 값을 추가하면 해당 폴더도 원본 경로에 반드시 존재해야 하며, 심볼릭 링크 형태여서는 안 됩니다.
    /// 필요 없는 기본 규칙으로 바꾸려면 <c>Clear()</c> 후 원하는 이름만 다시 추가하면 됩니다.
    /// </summary>
    public HashSet<string> RequiredSourceDirectoryNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 원본 게임 경로에 반드시 존재해야 하는 파일 이름 목록입니다.
    /// 기본값은 <c>"Run.exe"</c>입니다.
    /// 여기에 값을 추가하면 해당 파일도 원본 경로에 반드시 존재해야 합니다.
    /// 필요 없는 기본 규칙으로 바꾸려면 <c>Clear()</c> 후 원하는 이름만 다시 추가하면 됩니다.
    /// </summary>
    public HashSet<string> RequiredSourceFileNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 대상 게임 경로에서 이미 존재할 수 있는 심볼릭 링크 폴더 이름 목록입니다.
    /// 기본값은 <c>"DLL"</c>입니다.
    /// 여기에 값을 추가하면 대상 경로에 같은 이름의 폴더가 이미 있을 때,
    /// 해당 폴더는 반드시 원본 경로의 같은 이름 폴더를 가리키는 심볼릭 링크여야 합니다.
    /// 다른 링크나 일반 폴더가 있으면 생성이 중단됩니다.
    /// 이 검사를 끄려면 <c>Clear()</c>로 비울 수 있습니다.
    /// </summary>
    public HashSet<string> OptionalDestinationSymbolDirectoryNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 원본 게임 경로의 최상위 파일 복사에서 제외할 확장자 목록입니다.
    /// 기본값은 <c>".bmp"</c>, <c>".dmp"</c>, <c>".tmp"</c>입니다.
    /// 여기에 값을 추가하면 같은 확장자의 최상위 파일은 복사되지 않습니다.
    /// 확장자는 반드시 <c>".ext"</c> 형식으로 넣어야 하며, 이 규칙은 <see cref="Validate"/>에서 검사됩니다.
    /// 기본 제외 규칙을 비우고 싶다면 <c>Clear()</c>를 사용할 수 있습니다.
    /// </summary>
    public HashSet<string> ExcludedFileExtensions { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 폴더별 사용자 지정 복사 규칙 목록입니다.
    /// 기본값으로는 <c>"Assets"</c> 폴더에 대한 특수 처리 규칙이 들어 있습니다.
    /// 키는 폴더 이름이고, 값은 해당 폴더를 어떻게 처리할지 결정하는 핸들러입니다.
    /// 여기에 값을 추가하면 일반 복사나 심볼릭 링크 대신 지정한 사용자 로직이 실행됩니다.
    /// 기본 규칙을 모두 비우려면 <c>Clear()</c>를 사용할 수 있습니다.
    /// </summary>
    public Dictionary<string, SymbolClientCustomDirectoryHandler> CustomDirectoryHandlers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 복사와 링크 생성 대상에서 완전히 제외할 폴더 이름 목록입니다.
    /// 기본값은 <c>"TempFiles"</c>, <c>"PatchTemp"</c>입니다.
    /// 여기에 값을 추가하면 해당 폴더는 무시되고, 복사도 링크 생성도 하지 않습니다.
    /// 기본 무시 목록을 비우려면 <c>Clear()</c>를 사용할 수 있습니다.
    /// </summary>
    public HashSet<string> IgnoredDirectoryNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 원본을 가리키는 심볼릭 링크로 생성할 폴더 이름 목록입니다.
    /// 기본값은 <c>"DLL"</c>, <c>"Online"</c>입니다.
    /// 여기에 값을 추가하면 해당 폴더는 깊은 복사 대신 원본 경로를 가리키는 심볼릭 링크로 생성됩니다.
    /// 기본 심볼릭 링크 규칙을 비우려면 <c>Clear()</c>를 사용할 수 있습니다.
    /// </summary>
    public HashSet<string> SymbolicLinkDirectoryNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 기본 규칙이 채워진 옵션을 초기화합니다.
    /// </summary>
    public SymbolClientCreateOptions()
    {
        RequiredSourceDirectoryNames.Add("DLL");
        RequiredSourceFileNames.Add("Run.exe");
        OptionalDestinationSymbolDirectoryNames.Add("DLL");
        ExcludedFileExtensions.Add(".bmp");
        ExcludedFileExtensions.Add(".dmp");
        ExcludedFileExtensions.Add(".tmp");
        CustomDirectoryHandlers.Add("Assets", SymbolClientCreateDefaultHandlers.CopyAssetsDirectory);
        IgnoredDirectoryNames.Add("TempFiles");
        IgnoredDirectoryNames.Add("PatchTemp");
        SymbolicLinkDirectoryNames.Add("DLL");
        SymbolicLinkDirectoryNames.Add("Online");
    }

    /// <summary>
    /// 현재 옵션 값들이 유효한지 검사합니다.
    /// 폴더 이름과 파일 이름은 비어 있으면 안 되고, 제외 확장자는 반드시 <c>".ext"</c> 형식이어야 합니다.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// 컬렉션 안에 비어 있는 이름이 있거나, 확장자 형식이 올바르지 않거나, 사용자 지정 핸들러가 <see langword="null"/>인 경우 발생합니다.
    /// </exception>
    public void Validate()
    {
        ValidateNames(RequiredSourceDirectoryNames, nameof(RequiredSourceDirectoryNames), "폴더 이름");
        ValidateNames(RequiredSourceFileNames, nameof(RequiredSourceFileNames), "파일 이름");
        ValidateNames(OptionalDestinationSymbolDirectoryNames, nameof(OptionalDestinationSymbolDirectoryNames), "폴더 이름");
        ValidateNames(IgnoredDirectoryNames, nameof(IgnoredDirectoryNames), "폴더 이름");
        ValidateNames(SymbolicLinkDirectoryNames, nameof(SymbolicLinkDirectoryNames), "폴더 이름");

        foreach (string extension in ExcludedFileExtensions)
        {
            if (string.IsNullOrWhiteSpace(extension))
                throw new ArgumentException("제외 확장자 목록에는 비어 있는 값을 넣을 수 없습니다.", nameof(ExcludedFileExtensions));
            if (!extension.StartsWith(".", StringComparison.Ordinal))
                throw new ArgumentException("제외 확장자는 반드시 '.ext' 형식이어야 합니다.", nameof(ExcludedFileExtensions));
            if (extension.Length < 2)
                throw new ArgumentException("제외 확장자는 '.'만 단독으로 사용할 수 없습니다.", nameof(ExcludedFileExtensions));
            if (extension.Contains(Path.DirectorySeparatorChar) || extension.Contains(Path.AltDirectorySeparatorChar))
                throw new ArgumentException("제외 확장자에는 경로 구분자를 포함할 수 없습니다.", nameof(ExcludedFileExtensions));
        }

        foreach (KeyValuePair<string, SymbolClientCustomDirectoryHandler> pair in CustomDirectoryHandlers)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                throw new ArgumentException("사용자 지정 폴더 규칙에는 비어 있는 폴더 이름을 넣을 수 없습니다.", nameof(CustomDirectoryHandlers));
            if (pair.Value is null)
                throw new ArgumentException("사용자 지정 폴더 규칙에는 null 핸들러를 넣을 수 없습니다.", nameof(CustomDirectoryHandlers));
        }
    }

    private static void ValidateNames(IEnumerable<string> names, string paramName, string valueDescription)
    {
        foreach (string name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"{paramName}에는 비어 있는 {valueDescription}을 넣을 수 없습니다.", paramName);
        }
    }
}

internal static class SymbolClientCreateDefaultHandlers
{
    public static void CopyAssetsDirectory(DirectoryInfo sourceDirectoryInfo, string destinationDirectoryPath)
    {
        try
        {
            Directory.CreateDirectory(destinationDirectoryPath);
            FileSystemHelper.CopyFilesInDirectory(sourceDirectoryInfo, destinationDirectoryPath);

            foreach (DirectoryInfo subDir in sourceDirectoryInfo.GetDirectories())
            {
                string destinationSubDirectoryPath = Path.Combine(destinationDirectoryPath, subDir.Name);
                if (subDir.Name.Equals("Config", StringComparison.OrdinalIgnoreCase))
                {
                    FileSystemHelper.DeepCopyDirectory(subDir, destinationSubDirectoryPath);
                }
                else
                {
                    FileSystemHelper.CreateOrReuseDirectorySymbolicLink(destinationSubDirectoryPath, subDir.FullName);
                }
            }
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"An exception occurred while custom copy {sourceDirectoryInfo.FullName} -> {destinationDirectoryPath}", ex);
        }
    }
}
