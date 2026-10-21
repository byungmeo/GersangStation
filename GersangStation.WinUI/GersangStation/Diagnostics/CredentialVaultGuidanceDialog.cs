using System;
using System.Runtime.InteropServices;

namespace GersangStation.Diagnostics;

/// <summary>
/// Windows 자격 증명 관리자 서비스 접근 실패 여부를 판별합니다.
/// </summary>
public static class CredentialVaultGuidanceDialog
{
    private const int HrServiceCannotAcceptControl = unchecked((int)0x80070425);

    /// <summary>
    /// 예외 체인에 Credential Manager 서비스 제어 불가 HRESULT가 있는지 확인합니다.
    /// </summary>
    public static bool IsServiceUnavailable(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is COMException comException && comException.ErrorCode == HrServiceCannotAcceptControl)
                return true;

            if (current.HResult == HrServiceCannotAcceptControl)
                return true;
        }

        return false;
    }

}
