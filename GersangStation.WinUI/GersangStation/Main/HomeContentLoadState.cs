using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Main;

/// <summary>홈 콘텐츠의 대기/조회/빈 결과/실패를 구분하고 오래된 요청의 화면 갱신을 차단합니다.</summary>
public sealed class HomeContentLoadState : INotifyPropertyChanged
{
    private enum LoadStatus { Pending, Loading, Loaded, Empty, Failed }

    private LoadStatus _status = LoadStatus.Pending;
    private LoadStatus _settledStatus = LoadStatus.Pending;
    private CancellationTokenSource? _request;
    private DateTimeOffset? _loadedAt;

    public bool IsLoading => _status is LoadStatus.Pending or LoadStatus.Loading;
    public bool HasFailed => _status == LoadStatus.Failed;
    public bool IsEmpty => _status == LoadStatus.Empty;
    public bool HasSucceeded => _status is LoadStatus.Loaded or LoadStatus.Empty;
    public bool IsOverlayVisible => IsLoading || HasFailed;
    public event PropertyChangedEventHandler? PropertyChanged;

    internal async Task LoadAsync<T>(Func<CancellationToken, Task<IReadOnlyList<T>>> fetch,
        Action<IReadOnlyList<T>> apply, Action<Exception> recordFailure, bool forceRefresh = false,
        TimeSpan? timeout = null)
    {
        if (_request is not null)
            return;
        if (!forceRefresh && HasSucceeded && _loadedAt is { } loadedAt
            && DateTimeOffset.UtcNow - loadedAt < TimeSpan.FromMinutes(10))
            return;

        using var request = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(60));
        _request = request;
        SetStatus(LoadStatus.Loading);
        try
        {
            IReadOnlyList<T> items = await fetch(request.Token).WaitAsync(request.Token);
            if (!ReferenceEquals(_request, request)) return;
            request.Token.ThrowIfCancellationRequested();
            apply(items);
            _loadedAt = DateTimeOffset.UtcNow;
            _settledStatus = items.Count == 0 ? LoadStatus.Empty : LoadStatus.Loaded;
            SetStatus(_settledStatus);
        }
        catch (Exception ex)
        {
            // 탐색/화면 해제로 취소한 요청은 실패가 아니며, 다음 요청의 상태도 덮지 않습니다.
            if (!ReferenceEquals(_request, request)) return;
            _settledStatus = LoadStatus.Failed;
            SetStatus(LoadStatus.Failed);
            recordFailure(ex is OperationCanceledException && request.IsCancellationRequested
                ? new TimeoutException("홈 콘텐츠를 불러오는 시간이 초과되었습니다.", ex) : ex);
        }
        finally
        {
            if (ReferenceEquals(_request, request)) _request = null;
        }
    }

    internal void Cancel()
    {
        CancellationTokenSource? request = _request;
        _request = null;
        request?.Cancel();
        SetStatus(_settledStatus);
    }

    private void SetStatus(LoadStatus status)
    {
        _status = status;
        foreach (string property in new[] { nameof(IsLoading), nameof(HasFailed), nameof(IsEmpty),
            nameof(HasSucceeded), nameof(IsOverlayVisible) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
