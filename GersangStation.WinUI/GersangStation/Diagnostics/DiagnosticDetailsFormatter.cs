using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GersangStation.Diagnostics;

/// <summary>원본 진단 기록은 보존하고 Drawer의 환경 정보만 간소화합니다.</summary>
internal static class DiagnosticDetailsFormatter
{
    private static readonly HashSet<string> HiddenHeaderFields = new(StringComparer.Ordinal)
    {
        "Process Id", "Thread Id", "64-bit OS", "64-bit Process",
        "Culture", "UICulture", "Command Line", "Base Directory", "Current Directory",
        "LocalFolder", "Main Window Ready", "Assembly Version",
        "Previous Session", "Previous Process Id"
    };

    internal static string FormatForDisplay(string details, string? logPath)
    {
        using StringReader reader = new(details);
        string? title = reader.ReadLine();
        // 알 수 없는 형식이나 포맷팅 실패 시의 대체 기록은 임의로 해석하지 않습니다.
        if (title is not ("=== GersangStation Exception Report ==="
            or "=== GersangStation Fatal Exception Report ==="
            or "=== GersangStation Unexpected Termination Report ==="))
            return details;

        StringBuilder display = new();
        bool inHeader = true;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (inHeader)
            {
                if (string.IsNullOrWhiteSpace(line))
                    inHeader = false;
                else
                {
                    int separator = line.IndexOf(':');
                    if (separator > 0 && HiddenHeaderFields.Contains(line[..separator]))
                        continue;
                }
            }
            display.AppendLine(line);
        }

        string result = display.ToString().TrimEnd();
        // 경로는 뷰 아래에 별도로 표시합니다. 예외 본문 안의 동일한 문구는 건드리지 않습니다.
        if (logPath is not null)
        {
            string footer = $"{Environment.NewLine}Crash Log: {logPath}";
            if (result.EndsWith(footer, StringComparison.Ordinal))
                result = result[..^footer.Length].TrimEnd();
        }
        return result;
    }
}
