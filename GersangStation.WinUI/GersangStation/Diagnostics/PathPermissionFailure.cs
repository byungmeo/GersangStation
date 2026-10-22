using System;

namespace GersangStation.Diagnostics;

/// <summary>실제 작업의 권한 부족을 판별합니다. 사전 쓰기 테스트나 대화상자는 표시하지 않습니다.</summary>
internal static class PathPermissionFailure
{
    private const int AccessDeniedHResult = unchecked((int)0x80070005);
    private const int PrivilegeNotHeldHResult = unchecked((int)0x80070522);

    public static bool IsPermissionFailure(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is UnauthorizedAccessException ||
                current.HResult is AccessDeniedHResult or PrivilegeNotHeldHResult)
                return true;
        }
        return false;
    }
}
