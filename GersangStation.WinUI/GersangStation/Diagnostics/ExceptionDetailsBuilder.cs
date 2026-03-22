using System.Collections;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Windows.ApplicationModel;
using Windows.Storage;

namespace GersangStation.Diagnostics;

/// <summary>
/// 사용자 전달과 디버깅에 필요한 예외 상세 문자열을 생성합니다.
/// </summary>
internal static class ExceptionDetailsBuilder
{
    /// <summary>
    /// 예외, 런타임, 앱 정보를 하나의 보고 문자열로 만듭니다.
    /// </summary>
    public static string Build(Exception exception, string context, bool isFatal)
    {
        ArgumentNullException.ThrowIfNull(exception);

        StringBuilder builder = new();
        builder.AppendLine("=== GersangStation Exception Report ===");
        builder.AppendLine($"Occurred At: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        builder.AppendLine($"Context: {context}");
        builder.AppendLine($"Fatal: {isFatal}");
        builder.AppendLine($"Process Id: {Environment.ProcessId}");
        builder.AppendLine($"Thread Id: {Environment.CurrentManagedThreadId}");
        builder.AppendLine($"OS Version: {Environment.OSVersion}");
        builder.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        builder.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
        builder.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        builder.AppendLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
        builder.AppendLine($"Culture: {System.Globalization.CultureInfo.CurrentCulture.Name}");
        builder.AppendLine($"UICulture: {System.Globalization.CultureInfo.CurrentUICulture.Name}");
        builder.AppendLine($"Command Line: {Environment.CommandLine}");
        builder.AppendLine($"Base Directory: {AppContext.BaseDirectory}");
        builder.AppendLine($"Current Directory: {Environment.CurrentDirectory}");
        builder.AppendLine($"LocalFolder: {TryGetLocalFolderPath()}");
        builder.AppendLine($"Main Window Ready: {App.CurrentWindow is not null}");
        builder.AppendLine($"App Version: {TryGetAppVersion()}");
        builder.AppendLine($"Assembly Version: {Assembly.GetExecutingAssembly().GetName().Version}");
        builder.AppendLine();

        AppendException(builder, exception, level: 0);
        return builder.ToString();
    }

    private static void AppendException(StringBuilder builder, Exception exception, int level)
    {
        string sectionTitle = level == 0 ? "Exception" : $"Inner Exception {level}";
        builder.AppendLine($"[{sectionTitle}]");
        builder.AppendLine($"Type: {exception.GetType().FullName}");
        builder.AppendLine($"Message: {exception.Message}");
        builder.AppendLine($"HResult: 0x{exception.HResult:X8}");
        builder.AppendLine($"Source: {exception.Source ?? "(null)"}");
        builder.AppendLine($"TargetSite: {exception.TargetSite?.ToString() ?? "(null)"}");

        if (exception.Data.Count > 0)
        {
            builder.AppendLine("Data:");
            foreach (DictionaryEntry item in exception.Data)
                builder.AppendLine($"  {item.Key ?? "(null)"}: {item.Value ?? "(null)"}");
        }

        builder.AppendLine("StackTrace:");
        builder.AppendLine(string.IsNullOrWhiteSpace(exception.StackTrace) ? "(null)" : exception.StackTrace);
        builder.AppendLine();

        if (exception.InnerException is not null)
            AppendException(builder, exception.InnerException, level + 1);
    }

    private static string TryGetAppVersion()
    {
        try
        {
            PackageVersion version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch
        {
            return "(unavailable)";
        }
    }

    private static string TryGetLocalFolderPath()
    {
        try
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
            return "(unavailable)";
        }
    }
}
