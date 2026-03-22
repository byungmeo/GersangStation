namespace Core;

/// <summary>
/// 홈페이지 쿠키의 로그인 ID와 앱에 저장된 계정 ID를 같은 규칙으로 비교합니다.
/// </summary>
public static class LoginIdComparer
{
    /// <summary>
    /// 두 로그인 ID가 대소문자, 공백, URL 인코딩 차이를 제외하면 같은지 확인합니다.
    /// </summary>
    public static bool EqualsForComparison(string? left, string? right)
    {
        return string.Equals(
            NormalizeForComparison(left),
            NormalizeForComparison(right),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 로그인 ID를 URL 디코딩 및 공백 정리 후 비교 가능한 형태로 변환합니다.
    /// </summary>
    public static string NormalizeForComparison(string? value)
    {
        string candidate = value?.Trim() ?? string.Empty;
        if (candidate.Length == 0)
            return string.Empty;

        try
        {
            return Uri.UnescapeDataString(candidate).Trim();
        }
        catch (UriFormatException)
        {
            return candidate;
        }
    }
}
