using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace GersangStation.Services;

/// <summary>
/// 패키지 앱의 바탕화면 바로가기 생성과 존재 여부 확인을 담당합니다.
/// </summary>
public sealed class DesktopShortcutService
{
    private const string ApplicationId = "App";
    private const string ShortcutFileName = "거상스테이션.lnk";
    private const string ShortcutExtension = ".lnk";
    private const string ShortcutIconRelativePath = "Assets\\Icons\\GersangStationShortcut.ico";

    /// <summary>
    /// 현재 사용자 바탕화면에 앱 바로가기가 이미 있는지 확인합니다.
    /// </summary>
    public bool DesktopShortcutExists()
        => File.Exists(GetDesktopShortcutPath());

    /// <summary>
    /// 현재 패키지 앱을 여는 바탕화면 바로가기를 생성합니다.
    /// </summary>
    public DesktopShortcutOperationResult CreateDesktopShortcut()
    {
        string shortcutPath = GetDesktopShortcutPath();

        try
        {
            if (File.Exists(shortcutPath))
                return DesktopShortcutOperationResult.FromExistingShortcut(shortcutPath);

            string desktopDirectoryPath = Path.GetDirectoryName(shortcutPath)
                ?? throw new DirectoryNotFoundException("바탕화면 경로를 확인할 수 없습니다.");
            Directory.CreateDirectory(desktopDirectoryPath);

            string appUserModelId = GetAppUserModelId();
            string explorerPath = GetExplorerPath();
            string? processPath = Environment.ProcessPath;
            string iconLocation = GetShortcutIconLocation(processPath, explorerPath);
            string workingDirectory = string.IsNullOrWhiteSpace(processPath)
                ? desktopDirectoryPath
                : Path.GetDirectoryName(processPath) ?? desktopDirectoryPath;

            CreateShortcutFile(
                shortcutPath,
                targetPath: explorerPath,
                arguments: $"shell:AppsFolder\\{appUserModelId}",
                workingDirectory,
                iconLocation,
                description: $"{Package.Current.DisplayName} 실행");

            return DesktopShortcutOperationResult.Created(shortcutPath);
        }
        catch (Exception ex)
        {
            return DesktopShortcutOperationResult.Fail(shortcutPath, ex);
        }
    }

    /// <summary>
    /// 앱 기본 표시 이름 기준의 바탕화면 바로가기 경로를 반환합니다.
    /// </summary>
    public string GetDesktopShortcutPath()
        => Path.Combine(GetDesktopDirectoryPath(), ShortcutFileName);

    private static string GetDesktopDirectoryPath()
    {
        string desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktopDirectory))
            throw new DirectoryNotFoundException("바탕화면 경로를 찾을 수 없습니다.");

        return desktopDirectory;
    }

    private static string GetAppUserModelId()
    {
        string packageFamilyName = Package.Current.Id.FamilyName;
        if (string.IsNullOrWhiteSpace(packageFamilyName))
            throw new InvalidOperationException("패키지 패밀리 이름을 확인할 수 없습니다.");

        return $"{packageFamilyName}!{ApplicationId}";
    }

    private static string GetExplorerPath()
    {
        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windowsDirectory))
        {
            string explorerPath = Path.Combine(windowsDirectory, "explorer.exe");
            if (File.Exists(explorerPath))
                return explorerPath;
        }

        return "explorer.exe";
    }

    private static string GetShortcutIconLocation(string? processPath, string fallbackIconLocation)
    {
        string installedLocationPath = Package.Current.InstalledLocation.Path;
        if (!string.IsNullOrWhiteSpace(installedLocationPath))
        {
            string packagedIconPath = Path.Combine(installedLocationPath, ShortcutIconRelativePath);
            if (File.Exists(packagedIconPath))
                return packagedIconPath;
        }

        return string.IsNullOrWhiteSpace(processPath)
            ? fallbackIconLocation
            : $"{processPath},0";
    }

    private static void CreateShortcutFile(
        string shortcutPath,
        string targetPath,
        string arguments,
        string workingDirectory,
        string iconLocation,
        string description)
    {
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
            throw new PlatformNotSupportedException("WScript.Shell COM 개체를 사용할 수 없습니다.");

        object shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("WScript.Shell COM 개체를 만들 수 없습니다.");
        object? shortcut = null;

        try
        {
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: [shortcutPath]);

            if (shortcut is null)
                throw new InvalidOperationException("바로가기 COM 개체를 만들 수 없습니다.");

            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, [targetPath]);
            shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, [arguments]);
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, [workingDirectory]);
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, [iconLocation]);
            shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, [description]);
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, args: null);
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
            Marshal.FinalReleaseComObject(instance);
    }
}

/// <summary>
/// 바탕화면 바로가기 작업 결과를 UI 계층에 전달합니다.
/// </summary>
public readonly record struct DesktopShortcutOperationResult(bool Success, bool AlreadyExists, string ShortcutPath, Exception? Exception)
{
    public static DesktopShortcutOperationResult Created(string shortcutPath)
        => new(true, false, shortcutPath, null);

    public static DesktopShortcutOperationResult FromExistingShortcut(string shortcutPath)
        => new(true, true, shortcutPath, null);

    public static DesktopShortcutOperationResult Fail(string shortcutPath, Exception exception)
        => new(false, false, shortcutPath, exception);
}
