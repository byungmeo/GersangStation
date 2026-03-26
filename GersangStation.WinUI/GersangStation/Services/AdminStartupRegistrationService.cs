using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel;

namespace GersangStation.Services;

/// <summary>
/// 작업 스케줄러를 사용해 로그인 시 관리자 권한으로 앱을 시작하는 등록을 관리합니다.
/// </summary>
public sealed class AdminStartupRegistrationService
{
    private const string WScriptExecutableName = "wscript.exe";
    private const string PowerShellExecutableName = "powershell.exe";
    private const string DesktopShortcutFileName = "GersangStation Admin.lnk";

    private static readonly XNamespace TaskSchemaNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    private readonly string _taskName = $"GersangStation.AdminStartup.{Package.Current.Id.Name}";
    /// <summary>
    /// 현재 패키지에 대응하는 관리자 자동 실행 작업 이름입니다.
    /// </summary>
    public string TaskName => _taskName;

    /// <summary>
    /// 관리자 실행 바로가기에서 사용할 아이콘 경로입니다.
    /// </summary>
    public string DesktopShortcutIconPath => GetSupportIconPath();

    private readonly string _packageName = Package.Current.Id.Name;
    private readonly string _launcherExecutableName = Path.GetFileName(Environment.ProcessPath) ?? "GersangStation.exe";

    /// <summary>
    /// 현재 사용자에 대한 관리자 권한 자동 실행 작업이 올바르게 등록되어 있는지 확인합니다.
    /// </summary>
    public async Task<AdminStartupRegistrationState> GetStateAsync()
    {
        CommandResult queryResult = await RunCommandAsync(
            "schtasks.exe",
            $"/Query /TN {QuoteArgument(_taskName)} /XML",
            elevate: false).ConfigureAwait(false);

        if (queryResult.ExitCode != 0 || string.IsNullOrWhiteSpace(queryResult.StandardOutput))
            return new AdminStartupRegistrationState(false, string.Empty);

        try
        {
            string expectedVbsPath = GetTaskLauncherScriptPath();
            XDocument document = XDocument.Parse(queryResult.StandardOutput);

            string? command = document.Root?
                .Element(TaskSchemaNamespace + "Actions")?
                .Element(TaskSchemaNamespace + "Exec")?
                .Element(TaskSchemaNamespace + "Command")?
                .Value;

            string? arguments = document.Root?
                .Element(TaskSchemaNamespace + "Actions")?
                .Element(TaskSchemaNamespace + "Exec")?
                .Element(TaskSchemaNamespace + "Arguments")?
                .Value;

            string? runLevel = document.Root?
                .Element(TaskSchemaNamespace + "Principals")?
                .Element(TaskSchemaNamespace + "Principal")?
                .Element(TaskSchemaNamespace + "RunLevel")?
                .Value;

            string? logonType = document.Root?
                .Element(TaskSchemaNamespace + "Principals")?
                .Element(TaskSchemaNamespace + "Principal")?
                .Element(TaskSchemaNamespace + "LogonType")?
                .Value;

            bool hasExpectedCommand = string.Equals(
                Path.GetFileName(command ?? string.Empty),
                WScriptExecutableName,
                StringComparison.OrdinalIgnoreCase);
            bool hasExpectedArguments =
                !string.IsNullOrWhiteSpace(arguments) &&
                arguments.Contains(expectedVbsPath, StringComparison.OrdinalIgnoreCase);
            bool hasExpectedSecurity =
                string.Equals(runLevel, "HighestAvailable", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(logonType, "InteractiveToken", StringComparison.OrdinalIgnoreCase);

            return hasExpectedCommand && hasExpectedArguments && hasExpectedSecurity
                ? new AdminStartupRegistrationState(true, string.Empty)
                : new AdminStartupRegistrationState(false, "관리자 권한 자동 실행 작업이 현재 앱 설정과 일치하지 않아 다시 등록이 필요합니다.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to parse admin startup task XML: {ex}");
            return new AdminStartupRegistrationState(false, "관리자 권한 자동 실행 작업 상태를 확인하지 못했습니다.");
        }
    }

    /// <summary>
    /// 현재 사용자 로그인 시 관리자 권한으로 앱을 시작하는 작업을 등록하거나 갱신합니다.
    /// </summary>
    public async Task<StartupRegistrationOperationResult> EnableAsync()
    {
        try
        {
            await EnsureLauncherSupportFilesAsync().ConfigureAwait(false);
            string xmlPath = await WriteTaskDefinitionAsync().ConfigureAwait(false);

            try
            {
                CommandResult createResult = await RunCommandAsync(
                    "schtasks.exe",
                    $"/Create /TN {QuoteArgument(_taskName)} /XML {QuoteArgument(xmlPath)} /F",
                    elevate: true).ConfigureAwait(false);

                if (createResult.ExitCode != 0)
                {
                    return new StartupRegistrationOperationResult(
                        false,
                        "관리자 권한 자동 실행을 등록하지 못했습니다. 다시 시도해도 안 되면 작업 스케줄러에서 직접 확인해주세요.");
                }

                AdminStartupRegistrationState state = await GetStateAsync().ConfigureAwait(false);
                return state.IsRegistered
                    ? new StartupRegistrationOperationResult(true, string.Empty)
                    : new StartupRegistrationOperationResult(
                        false,
                        string.IsNullOrWhiteSpace(state.Message)
                            ? "관리자 권한 자동 실행이 정상적으로 등록되지 않았습니다."
                            : state.Message);
            }
            finally
            {
                TryDeleteFile(xmlPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to enable admin startup registration: {ex}");
            return new StartupRegistrationOperationResult(
                false,
                "관리자 권한 자동 실행을 등록하지 못했습니다. 다시 시도해도 안 되면 작업 스케줄러에서 직접 확인해주세요.");
        }
    }

    /// <summary>
    /// 관리자 권한 자동 실행 작업을 제거합니다.
    /// </summary>
    public async Task<StartupRegistrationOperationResult> DisableAsync()
    {
        AdminStartupRegistrationState state = await GetStateAsync().ConfigureAwait(false);
        if (!state.IsRegistered && string.IsNullOrWhiteSpace(state.Message))
            return new StartupRegistrationOperationResult(true, string.Empty);

        try
        {
            CommandResult deleteResult = await RunCommandAsync(
                "schtasks.exe",
                $"/Delete /TN {QuoteArgument(_taskName)} /F",
                elevate: true).ConfigureAwait(false);

            return deleteResult.ExitCode == 0
                ? new StartupRegistrationOperationResult(true, string.Empty)
                : new StartupRegistrationOperationResult(
                    false,
                    "관리자 권한 자동 실행을 해제하지 못했습니다. 다시 시도해도 안 되면 작업 스케줄러에서 직접 삭제해주세요.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to disable admin startup registration: {ex}");
            return new StartupRegistrationOperationResult(
                false,
                "관리자 권한 자동 실행을 해제하지 못했습니다. 다시 시도해도 안 되면 작업 스케줄러에서 직접 삭제해주세요.");
        }
    }

    /// <summary>
    /// 관리자 실행에 필요한 외부 지원 파일을 최신 상태로 생성합니다.
    /// </summary>
    public async Task EnsureLauncherSupportFilesAsync()
    {
        string scriptPath = GetLauncherScriptPath();
        string supportDirectory = GetSupportDirectoryPath();
        Directory.CreateDirectory(supportDirectory);
        await File.WriteAllTextAsync(scriptPath, BuildLauncherScriptContent(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(GetTaskLauncherScriptPath(), BuildWindowlessLauncherScriptContent(scriptPath), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            .ConfigureAwait(false);

        string sourceIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "GersangStationShortcut.ico");
        if (File.Exists(sourceIconPath))
            File.Copy(sourceIconPath, GetSupportIconPath(), overwrite: true);
    }

    private async Task<string> WriteTaskDefinitionAsync()
    {
        string currentUserSid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("Current user SID is unavailable.");
        string tempFilePath = Path.Combine(Path.GetTempPath(), $"{_taskName}.xml");

        XDocument document = new(
            new XDeclaration("1.0", "UTF-16", null),
            new XElement(
                TaskSchemaNamespace + "Task",
                new XAttribute("version", "1.4"),
                new XElement(
                    TaskSchemaNamespace + "RegistrationInfo",
                    new XElement(TaskSchemaNamespace + "Author", "GersangStation"),
                    new XElement(TaskSchemaNamespace + "Description", "Launch GersangStation at user logon with highest privileges.")),
                new XElement(
                    TaskSchemaNamespace + "Triggers",
                    new XElement(
                        TaskSchemaNamespace + "LogonTrigger",
                        new XElement(TaskSchemaNamespace + "Enabled", "true"),
                        new XElement(TaskSchemaNamespace + "UserId", currentUserSid))),
                new XElement(
                    TaskSchemaNamespace + "Principals",
                    new XElement(
                        TaskSchemaNamespace + "Principal",
                        new XAttribute("id", "Author"),
                        new XElement(TaskSchemaNamespace + "UserId", currentUserSid),
                        new XElement(TaskSchemaNamespace + "LogonType", "InteractiveToken"),
                        new XElement(TaskSchemaNamespace + "RunLevel", "HighestAvailable"))),
                new XElement(
                    TaskSchemaNamespace + "Settings",
                    new XElement(TaskSchemaNamespace + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(TaskSchemaNamespace + "DisallowStartIfOnBatteries", "false"),
                    new XElement(TaskSchemaNamespace + "StopIfGoingOnBatteries", "false"),
                    new XElement(TaskSchemaNamespace + "AllowHardTerminate", "true"),
                    new XElement(TaskSchemaNamespace + "StartWhenAvailable", "true"),
                    new XElement(TaskSchemaNamespace + "RunOnlyIfNetworkAvailable", "false"),
                    new XElement(
                        TaskSchemaNamespace + "IdleSettings",
                        new XElement(TaskSchemaNamespace + "StopOnIdleEnd", "false"),
                        new XElement(TaskSchemaNamespace + "RestartOnIdle", "false")),
                    new XElement(TaskSchemaNamespace + "AllowStartOnDemand", "true"),
                    new XElement(TaskSchemaNamespace + "Enabled", "true"),
                    new XElement(TaskSchemaNamespace + "Hidden", "false"),
                    new XElement(TaskSchemaNamespace + "RunOnlyIfIdle", "false"),
                    new XElement(TaskSchemaNamespace + "DisallowStartOnRemoteAppSession", "false"),
                    new XElement(TaskSchemaNamespace + "UseUnifiedSchedulingEngine", "true"),
                    new XElement(TaskSchemaNamespace + "WakeToRun", "false"),
                    new XElement(TaskSchemaNamespace + "ExecutionTimeLimit", "PT0S"),
                    new XElement(TaskSchemaNamespace + "Priority", "7")),
                new XElement(
                    TaskSchemaNamespace + "Actions",
                    new XAttribute("Context", "Author"),
                    new XElement(
                        TaskSchemaNamespace + "Exec",
                        new XElement(TaskSchemaNamespace + "Command", WScriptExecutableName),
                        new XElement(
                            TaskSchemaNamespace + "Arguments",
                            QuoteArgument(GetTaskLauncherScriptPath()))))));

        await File.WriteAllTextAsync(tempFilePath, document.ToString(), Encoding.Unicode).ConfigureAwait(false);
        return tempFilePath;
    }

    private string BuildLauncherScriptContent()
    {
        string escapedPackageName = _packageName.Replace("'", "''", StringComparison.Ordinal);
        string escapedExecutableName = _launcherExecutableName.Replace("'", "''", StringComparison.Ordinal);
        string escapedTaskName = _taskName.Replace("'", "''", StringComparison.Ordinal);
        string escapedShortcutPath = GetDesktopShortcutPath().Replace("'", "''", StringComparison.Ordinal);

        return
            "$ErrorActionPreference = 'Stop'" + Environment.NewLine +
            $"$taskName = '{escapedTaskName}'" + Environment.NewLine +
            $"$package = Get-AppxPackage -Name '{escapedPackageName}' | Sort-Object Version -Descending | Select-Object -First 1" + Environment.NewLine +
            $"$shortcutPath = '{escapedShortcutPath}'" + Environment.NewLine +
            "function Remove-AdminArtifacts {" + Environment.NewLine +
            "    schtasks.exe /Delete /TN $taskName /F *> $null" + Environment.NewLine +
            "    if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue }" + Environment.NewLine +
            "}" + Environment.NewLine +
            "if ($null -eq $package) {" + Environment.NewLine +
            "    Remove-AdminArtifacts" + Environment.NewLine +
            "    exit 2" + Environment.NewLine +
            "}" + Environment.NewLine +
            "$installLocation = $package.InstallLocation" + Environment.NewLine +
            "if ([string]::IsNullOrWhiteSpace($installLocation)) {" + Environment.NewLine +
            "    Remove-AdminArtifacts" + Environment.NewLine +
            "    exit 3" + Environment.NewLine +
            "}" + Environment.NewLine +
            $"$exePath = Join-Path $installLocation '{escapedExecutableName}'" + Environment.NewLine +
            "if (-not (Test-Path -LiteralPath $exePath)) {" + Environment.NewLine +
            "    Remove-AdminArtifacts" + Environment.NewLine +
            "    exit 4" + Environment.NewLine +
            "}" + Environment.NewLine +
            "Start-Process -FilePath $exePath -WorkingDirectory $installLocation | Out-Null";
    }

    private string BuildWindowlessLauncherScriptContent(string powerShellScriptPath)
    {
        string escapedPowerShellPath = powerShellScriptPath.Replace("\"", "\"\"", StringComparison.Ordinal);
        string powerShellCommand =
            $"{PowerShellExecutableName} -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"\"{escapedPowerShellPath}\"\"";

        return
            "Set shell = CreateObject(\"WScript.Shell\")" + Environment.NewLine +
            $"command = \"{powerShellCommand}\"" + Environment.NewLine +
            "shell.Run command, 0, False";
    }

    private string GetLauncherScriptPath()
        => Path.Combine(GetSupportDirectoryPath(), $"{_taskName}.ps1");

    private string GetTaskLauncherScriptPath()
        => Path.Combine(GetSupportDirectoryPath(), $"{_taskName}.task.vbs");

    private string GetSupportIconPath()
        => Path.Combine(GetSupportDirectoryPath(), "GersangStationShortcut.ico");

    private string GetSupportDirectoryPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GersangStation", "AdminStartup");

    private string GetDesktopShortcutPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), DesktopShortcutFileName);

    private static async Task<CommandResult> RunCommandAsync(string fileName, string arguments, bool elevate)
    {
        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = !elevate,
                UseShellExecute = elevate,
            };

            if (elevate)
            {
                startInfo.Verb = "runas";
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
            else
            {
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");
            await process.WaitForExitAsync().ConfigureAwait(false);

            string standardOutput = elevate ? string.Empty : await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            string standardError = elevate ? string.Empty : await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

            return new CommandResult(process.ExitCode, standardOutput, standardError);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new CommandResult(1223, string.Empty, "Elevation request was canceled.");
        }
    }

    private static string QuoteArgument(string value)
        => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to delete temporary file '{path}': {ex}");
        }
    }

    private readonly record struct CommandResult(int ExitCode, string StandardOutput, string StandardError);
}

/// <summary>
/// 관리자 권한 자동 실행 작업의 현재 등록 상태입니다.
/// </summary>
public readonly record struct AdminStartupRegistrationState(bool IsRegistered, string Message);

/// <summary>
/// 시작 프로그램 구성 변경 결과입니다.
/// </summary>
public readonly record struct StartupRegistrationOperationResult(bool Success, string Message);
