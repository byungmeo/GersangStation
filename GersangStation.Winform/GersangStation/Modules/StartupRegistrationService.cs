using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;

namespace GersangStation.Modules;

internal static class StartupRegistrationService {
    private const string SourcePrefix = "GersangStation.WinForms.Startup/";
    private static readonly XNamespace TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public static string Register(string executablePath) {
        executablePath = Path.GetFullPath(executablePath);
        if(!File.Exists(executablePath)) {
            throw new FileNotFoundException("자동 실행할 프로그램을 찾을 수 없습니다.", executablePath);
        }

        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        string userSid = identity.User?.Value ?? throw new InvalidOperationException("현재 Windows 사용자를 확인할 수 없습니다.");
        string taskName = "GersangStation.WinForms.Startup." + userSid;
        string source = SourcePrefix + Guid.NewGuid().ToString("N");
        string xml = BuildTaskXml(executablePath, userSid, taskName, source);
        object? scheduler = null;
        object? folder = null;
        object? registeredTask = null;
        try {
            Type schedulerType = Type.GetTypeFromProgID("Schedule.Service")
                ?? throw new InvalidOperationException("Windows 작업 스케줄러를 사용할 수 없습니다.");
            scheduler = Activator.CreateInstance(schedulerType)!;
            ((dynamic)scheduler).Connect();
            folder = ((dynamic)scheduler).GetFolder(@"\");
            EnsureOwnedTask(folder, taskName);
            // TASK_CREATE_OR_UPDATE / TASK_LOGON_INTERACTIVE_TOKEN: 암호를 저장하지 않습니다.
            registeredTask = ((dynamic)folder).RegisterTask(taskName, xml, 6, userSid, null, 3, null);
            return taskName;
        } finally {
            ReleaseComObject(registeredTask);
            ReleaseComObject(folder);
            ReleaseComObject(scheduler);
        }
    }

    private static void EnsureOwnedTask(object folder, string taskName) {
        object? existingTask = null;
        try {
            existingTask = ((dynamic)folder).GetTask(taskName);
            XDocument existingXml = XDocument.Parse((string)((dynamic)existingTask).Xml);
            string? source = (string?)existingXml.Root?.Element(TaskNamespace + "RegistrationInfo")?.Element(TaskNamespace + "Source");
            if(source?.StartsWith(SourcePrefix, StringComparison.Ordinal) != true) {
                throw new InvalidOperationException("같은 이름의 다른 예약 작업이 있어 자동 실행을 등록하지 못했습니다.");
            }
        } catch(Exception ex) when(ex.HResult == unchecked((int)0x80070002)) {
            // 아직 등록하지 않은 경우입니다.
        } finally {
            ReleaseComObject(existingTask);
        }
    }

    internal static string BuildTaskXml(string executablePath, string userSid, string taskName, string source) {
        string powershellPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        string script = BuildLauncherScript(executablePath, taskName, source);
        string arguments = "-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand "
            + Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        XNamespace ns = TaskNamespace;
        return new XDocument(new XElement(ns + "Task", new XAttribute("version", "1.2"),
            new XElement(ns + "RegistrationInfo",
                new XElement(ns + "Author", userSid),
                new XElement(ns + "Description", "Windows 로그인 시 GersangStation Mini를 실행합니다. 실행 파일이 삭제되면 이 작업을 자동으로 정리합니다."),
                new XElement(ns + "Source", source)),
            new XElement(ns + "Triggers", new XElement(ns + "LogonTrigger",
                new XElement(ns + "Enabled", true), new XElement(ns + "UserId", userSid))),
            new XElement(ns + "Principals", new XElement(ns + "Principal", new XAttribute("id", "CurrentUser"),
                new XElement(ns + "UserId", userSid),
                new XElement(ns + "LogonType", "InteractiveToken"),
                new XElement(ns + "RunLevel", "HighestAvailable"))),
            new XElement(ns + "Settings",
                new XElement(ns + "MultipleInstancesPolicy", "IgnoreNew"),
                new XElement(ns + "DisallowStartIfOnBatteries", false),
                new XElement(ns + "StopIfGoingOnBatteries", false),
                new XElement(ns + "AllowStartOnDemand", true),
                new XElement(ns + "Enabled", true),
                new XElement(ns + "ExecutionTimeLimit", "PT0S")),
            // 삭제된 앱 폴더를 작업 디렉터리로 지정하면 정리 스크립트조차 시작하지 못합니다.
            new XElement(ns + "Actions", new XAttribute("Context", "CurrentUser"),
                new XElement(ns + "Exec", new XElement(ns + "Command", powershellPath), new XElement(ns + "Arguments", arguments)))))
            .ToString();
    }

    internal static string BuildLauncherScript(string executablePath, string taskName, string source) {
        return $$"""
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            $exePath = '{{EscapePowerShellLiteral(executablePath)}}'
            $taskName = '{{EscapePowerShellLiteral(taskName)}}'
            $source = '{{EscapePowerShellLiteral(source)}}'
            try {
                $attributes = [System.IO.File]::GetAttributes($exePath)
                $exists = ($attributes -band [System.IO.FileAttributes]::Directory) -eq 0
            } catch {
                $failure = $_.Exception.GetBaseException()
                if ($failure -isnot [System.IO.FileNotFoundException] -and $failure -isnot [System.IO.DirectoryNotFoundException]) { exit 0 }
                $exists = $false
            }
            if ($exists) {
                try {
                    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
                    $startInfo.FileName = $exePath
                    $startInfo.WorkingDirectory = [System.IO.Path]::GetDirectoryName($exePath)
                    $startInfo.UseShellExecute = $false
                    [System.Diagnostics.Process]::Start($startInfo) | Out-Null
                } catch { }
                exit 0
            }
            try {
                $scheduler = New-Object -ComObject Schedule.Service
                $scheduler.Connect()
                $folder = $scheduler.GetFolder('\')
                $task = $folder.GetTask($taskName)
                [xml]$definition = $task.Xml
                # 이전 실행이 새 경로로 다시 등록한 작업까지 지우지 않도록 확인합니다.
                if ($definition.Task.RegistrationInfo.Source -ceq $source) {
                    $folder.DeleteTask($taskName, 0)
                }
            } catch { }
            exit 0
            """;
    }

    private static string EscapePowerShellLiteral(string value) => value.Replace("'", "''");

    private static void ReleaseComObject(object? value) {
        if(value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }
}
