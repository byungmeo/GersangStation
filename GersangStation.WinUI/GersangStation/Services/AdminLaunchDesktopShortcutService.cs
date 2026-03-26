using System;
using System.IO;
using System.Reflection;

namespace GersangStation.Services;

/// <summary>
/// 관리자 실행 작업을 호출하는 바탕화면 바로가기를 생성합니다.
/// </summary>
public sealed class AdminLaunchDesktopShortcutService
{
    private readonly string _taskName;

    public AdminLaunchDesktopShortcutService(string taskName)
    {
        _taskName = taskName;
    }

    /// <summary>
    /// 현재 사용자 바탕화면에 관리자 실행 바로가기를 만들거나 덮어씁니다.
    /// </summary>
    public DesktopShortcutCreationResult CreateShortcut()
    {
        string desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktopDirectory) || !Directory.Exists(desktopDirectory))
        {
            return new DesktopShortcutCreationResult(
                false,
                string.Empty,
                "바탕화면 경로를 찾지 못해 관리자 실행 바로가기를 만들 수 없습니다.");
        }

        string shortcutPath = Path.Combine(desktopDirectory, "GersangStation Admin.lnk");

        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return new DesktopShortcutCreationResult(
                    false,
                    shortcutPath,
                    "Windows 바로가기 구성 요소를 찾지 못해 관리자 실행 바로가기를 만들 수 없습니다.");
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
            SetShortcutProperty(shortcutType, shortcut, "TargetPath", Path.Combine(Environment.SystemDirectory, "schtasks.exe"));
            SetShortcutProperty(shortcutType, shortcut, "Arguments", $"/Run /TN \"{_taskName}\"");
            SetShortcutProperty(shortcutType, shortcut, "WorkingDirectory", Environment.SystemDirectory);
            SetShortcutProperty(shortcutType, shortcut, "Description", "Launch GersangStation through the elevated startup task.");

            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "GersangStationShortcut.ico");
            if (File.Exists(iconPath))
                SetShortcutProperty(shortcutType, shortcut, "IconLocation", iconPath);

            shortcutType.InvokeMember(
                "Save",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shortcut,
                args: null);

            return new DesktopShortcutCreationResult(true, shortcutPath, string.Empty);
        }
        catch (Exception)
        {
            return new DesktopShortcutCreationResult(
                false,
                shortcutPath,
                "관리자 실행 바로가기를 만들지 못했습니다. 다시 시도해도 안 되면 바탕화면 쓰기 권한과 Windows 바로가기 구성을 확인해주세요.");
        }
    }

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
