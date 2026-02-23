using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Core.Patch;

public sealed class DownloadManager : IAsyncDisposable
{
    private readonly Downloader _downloader;
    private readonly Channel<QueueItem> _channel;
    private readonly CancellationTokenSource _shutdownCts = new();

    private readonly Task[] _workers;

    // key -> job state (중복 무시 정책: 존재하면 기존 Task 반환)
    private readonly ConcurrentDictionary<string, JobState> _jobs = new(StringComparer.Ordinal);

    public int MaxConcurrency { get; }

    public DownloadManager(Downloader downloader, int maxConcurrency)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        MaxConcurrency = maxConcurrency;

        _channel = Channel.CreateUnbounded<QueueItem>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false,
            AllowSynchronousContinuations = true
        });

        _workers = new Task[MaxConcurrency];
        for (int i = 0; i < _workers.Length; i++)
            _workers[i] = Task.Run(() => WorkerLoopAsync(_shutdownCts.Token));
    }

    /// <summary>
    /// 중복(동일 url+dest)이면 기존 작업 Task 반환.
    /// </summary>
    public Task EnqueueAsync(
        Uri url,
        string destinationPath,
        DownloadOptions options,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        if (url is null) throw new ArgumentNullException(nameof(url));
        if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("destinationPath is required.", nameof(destinationPath));
        if (options is null) throw new ArgumentNullException(nameof(options));

        string key = MakeKey(url, destinationPath);

        if (_jobs.TryGetValue(key, out var existing))
            return existing.Tcs.Task;

        ct.ThrowIfCancellationRequested();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var jobCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var state = new JobState(
            Key: key,
            Url: url,
            DestinationPath: destinationPath,
            Options: options,
            Cts: jobCts,
            Tcs: tcs);

        if (!_jobs.TryAdd(key, state))
        {
            if (_jobs.TryGetValue(key, out var raced))
                return raced.Tcs.Task;

            throw new InvalidOperationException("Failed to enqueue job due to race.");
        }

        if (!_channel.Writer.TryWrite(new QueueItem(state, progress)))
        {
            _jobs.TryRemove(key, out _);
            jobCts.Dispose();
            tcs.TrySetException(new ObjectDisposedException(nameof(DownloadManager)));
        }

        return tcs.Task;
    }

    public Task EnqueueAsync(
        Uri url,
        string destinationPath,
        DownloadOptions options,
        IProgress<DownloadProgress>? progress = null)
        => EnqueueAsync(url, destinationPath, options, progress, CancellationToken.None);

    /// <summary>
    /// Cancel 의미: 작업 취소 + temp(.crdownload 또는 options.TempPath) 삭제(best-effort)
    /// </summary>
    public bool Cancel(Uri url, string destinationPath)
    {
        if (url is null) throw new ArgumentNullException(nameof(url));
        if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("destinationPath is required.", nameof(destinationPath));

        return Cancel(MakeKey(url, destinationPath));
    }

    public bool Cancel(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("key is required.", nameof(key));

        if (!_jobs.TryGetValue(key, out var state))
            return false;

        state.DeleteTempOnCancel = true;
        state.Cts.Cancel();
        return true;
    }

    public void CancelAll()
    {
        foreach (var kv in _jobs)
        {
            kv.Value.DeleteTempOnCancel = true;
            kv.Value.Cts.Cancel();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdownCts.Cancel();
        _channel.Writer.TryComplete();

        try
        {
            await Task.WhenAll(_workers).ConfigureAwait(false);
        }
        catch
        {
            // Dispose 흐름 유지
        }

        foreach (var kv in _jobs)
        {
            kv.Value.Cts.Cancel();
            kv.Value.Cts.Dispose();
        }

        _shutdownCts.Dispose();
    }

    private async Task WorkerLoopAsync(CancellationToken managerCt)
    {
        try
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(managerCt).ConfigureAwait(false))
            {
                var state = item.State;
                try
                {
                    await _downloader.DownloadAsync(
                        state.Url,
                        state.DestinationPath,
                        state.Options,
                        item.Progress,
                        state.Cts.Token).ConfigureAwait(false);

                    state.Tcs.TrySetResult();
                }
                catch (OperationCanceledException oce)
                {
                    if (state.DeleteTempOnCancel)
                        TryDeleteTemp(state.DestinationPath, state.Options);

                    state.Tcs.TrySetCanceled(oce.CancellationToken);
                }
                catch (Exception ex)
                {
                    state.Tcs.TrySetException(ex);
                }
                finally
                {
                    _jobs.TryRemove(state.Key, out _);
                    state.Cts.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private static string MakeKey(Uri url, string destinationPath)
        => string.Concat(url.AbsoluteUri, "||", destinationPath);

    private static void TryDeleteTemp(string destinationPath, DownloadOptions options)
    {
        try
        {
            string tempPath = options.TempPath ?? (destinationPath + ".crdownload");
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
        catch
        {
            // best-effort
        }
    }

    private sealed record QueueItem(JobState State, IProgress<DownloadProgress>? Progress);

    private sealed class JobState
    {
        public string Key { get; }
        public Uri Url { get; }
        public string DestinationPath { get; }
        public DownloadOptions Options { get; }
        public CancellationTokenSource Cts { get; }
        public TaskCompletionSource Tcs { get; }

        public volatile bool DeleteTempOnCancel;

        public JobState(
            string Key,
            Uri Url,
            string DestinationPath,
            DownloadOptions Options,
            CancellationTokenSource Cts,
            TaskCompletionSource Tcs)
        {
            this.Key = Key;
            this.Url = Url;
            this.DestinationPath = DestinationPath;
            this.Options = Options;
            this.Cts = Cts;
            this.Tcs = Tcs;
        }
    }
}