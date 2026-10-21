using System;
using System.Threading;

namespace GersangStation.Diagnostics;

/// <summary>
/// 정상 취소와 구별되는 브라우저 고장입니다. 같은 고장으로 중단된 작업들은
/// 이 인스턴스를 공유하지만, 이후 사용자의 새 요청은 새 안내 인스턴스를 사용합니다.
/// </summary>
internal sealed class BrowserUnavailableException(Exception? cause)
    : InvalidOperationException("브라우저를 사용할 수 없어 요청을 완료하지 못했습니다. 앱을 다시 실행한 후 시도해 주세요.", cause)
{
    private int _reported;
    internal bool TryClaimReport() => Interlocked.CompareExchange(ref _reported, 1, 0) == 0;
}
