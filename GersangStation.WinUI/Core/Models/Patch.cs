namespace Core.Models;

/// <summary>
/// 패치 readme에서 추출한 개별 버전 항목을 나타냅니다.
/// </summary>
public sealed class PatchReadmeInfoItem(DateTime date, int version, List<string> details)
{
    public DateTime Date { get; } = date;
    public int Version { get; } = version;
    public List<string> Details { get; } = details;
    // A locally read version may have no corresponding readme date.
    public string Display => Date == default ? $"v{Version}" : $"v{Version}   {Date:yyyy-MM-dd}";
}
