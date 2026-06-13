using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace GersangStation.Services;

/// <summary>
/// 앱을 실행하는 바탕화면 바로가기를 생성합니다.
/// </summary>
public sealed class DesktopShortcutService
{
    private const string LegacyAdminShortcutFileName = "GersangStation Admin.lnk";

    private readonly string _appUserModelId;
    private readonly string _iconPath;
    private readonly string _shortcutFileName;

    public DesktopShortcutService(string appUserModelId, string iconPath, string shortcutFileName)
    {
        _appUserModelId = appUserModelId;
        _iconPath = iconPath;
        _shortcutFileName = shortcutFileName;
    }

    /// <summary>
    /// 현재 사용자 바탕화면에 앱 실행 바로가기를 만들거나 덮어씁니다.
    /// </summary>
    public DesktopShortcutCreationResult CreateShortcut()
    {
        string desktopDirectory = GetDesktopDirectory();
        if (string.IsNullOrWhiteSpace(desktopDirectory) || !Directory.Exists(desktopDirectory))
        {
            return new DesktopShortcutCreationResult(
                false,
                string.Empty,
                "바탕화면 경로를 찾지 못해 바로가기를 만들 수 없습니다.");
        }

        string shortcutPath = Path.Combine(desktopDirectory, _shortcutFileName);

        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return new DesktopShortcutCreationResult(
                    false,
                    shortcutPath,
                    "Windows 바로가기 구성 요소를 찾지 못해 바로가기를 만들 수 없습니다.");
            }

            object shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Failed to create WScript.Shell COM instance.");
            object shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    binder: null,
                    target: shell,
                    args: [shortcutPath])
                ?? throw new InvalidOperationException("Failed to create shortcut object.");

            Type shortcutType = shortcut.GetType();
            SetShortcutProperty(shortcutType, shortcut, "TargetPath", "explorer.exe");
            SetShortcutProperty(shortcutType, shortcut, "Arguments", $"shell:AppsFolder\\{_appUserModelId}");
            SetShortcutProperty(shortcutType, shortcut, "WorkingDirectory", Environment.SystemDirectory);
            SetShortcutProperty(shortcutType, shortcut, "Description", "Launch GersangStation.");

            if (File.Exists(_iconPath))
                SetShortcutProperty(shortcutType, shortcut, "IconLocation", _iconPath);

            shortcutType.InvokeMember(
                "Save",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shortcut,
                args: null);

            DeleteShortcutIfExists(LegacyAdminShortcutFileName);

            return new DesktopShortcutCreationResult(true, shortcutPath, string.Empty);
        }
        catch (Exception)
        {
            return new DesktopShortcutCreationResult(
                false,
                shortcutPath,
                "바로가기를 만들지 못했습니다. 다시 시도해도 안 되면 바탕화면 쓰기 권한과 Windows 바로가기 구성을 확인해주세요.");
        }
    }

    /// <summary>
    /// 현재 사용자 바탕화면에 앱 바로가기가 있는지 확인합니다.
    /// </summary>
    public bool ShortcutExists()
    {
        string desktopDirectory = GetDesktopDirectory();
        if (string.IsNullOrWhiteSpace(desktopDirectory))
            return false;

        return File.Exists(Path.Combine(desktopDirectory, _shortcutFileName));
    }

    /// <summary>
    /// 현재 사용자 바탕화면에서 앱 바로가기를 조용히 제거합니다.
    /// </summary>
    public void DeleteShortcutIfExists()
        => DeleteShortcutIfExists(_shortcutFileName);

    private static void DeleteShortcutIfExists(string shortcutFileName)
    {
        string desktopDirectory = GetDesktopDirectory();
        if (string.IsNullOrWhiteSpace(desktopDirectory))
            return;

        string shortcutPath = Path.Combine(desktopDirectory, shortcutFileName);
        if (!File.Exists(shortcutPath))
            return;

        try
        {
            File.Delete(shortcutPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete desktop shortcut '{shortcutPath}': {ex}");
        }
    }

    private static string GetDesktopDirectory()
        => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    private static void SetShortcutProperty(Type shortcutType, object shortcut, string propertyName, object value)
    {
        shortcutType.InvokeMember(
            propertyName,
            BindingFlags.SetProperty,
            binder: null,
            target: shortcut,
            args: [value]);
    }
}

/// <summary>
/// 바탕화면 바로가기 생성 결과입니다.
/// </summary>
public readonly record struct DesktopShortcutCreationResult(bool Success, string ShortcutPath, string Message);
