using Core;
using Core.Download;
using Core.Extract;
using Core.Models;
using System.Diagnostics;
using static Core.PatchManager;

namespace GameMaintenance.Worker;

internal sealed class PatchService
{
    private readonly Downloader _downloader = new(HttpClientProvider.Http);
    private readonly IExtractor _extractor = new ZipFileExtractor();

    public async Task RunAsync(
    GameServer targetServer,
    int currentClientVersion,
    string originClientPath,
    PatchRunOptions options,
    ClientSettings clientSettings,
    IProgress<PatchProgress>? progress,
    CancellationToken ct)
    {
        Debug.WriteLine(
            $"RunAsync\n\t" +
            $"targetServer: {targetServer}\n\t" +
            $"currentVersion: {currentClientVersion}\n\t" +
            $"clientPath: {originClientPath}");

        // Use the same validity check as the installation page, before patch writes
        // can alter the shared source directories. Missing/invalid clones are not created.
        CreateSymbolMultiClientArgs multiClientPatchArgs = BuildExistingMultiClientPatchArgs(
            targetServer, originClientPath, clientSettings);

        int latestClientVersion;
        try
        {
            latestClientVersion = await GetLatestServerVersionAsync(targetServer, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PatchOperationException(
                $"Failed to resolve the latest patch version for server '{targetServer}'.",
                targetServer,
                PatchFailureStage.ResolveLatestVersion,
                ex);
        }

        multiClientPatchArgs.LayoutPolicy = latestClientVersion >= GameClientHelper.MultiClientLayoutBoundaryVersion
            ? GameClientHelper.MultiClientLayoutPolicy.V34100OrLater
            : GameClientHelper.MultiClientLayoutPolicy.Legacy;

        string tempRoot = GetPatchTempRoot(originClientPath, currentClientVersion, latestClientVersion);

        if (options.ArchiveReuseMode == PatchArchiveReuseMode.RestartFromScratch)
        {
            Debug.WriteLine($"[PatchManager] RestartFromScratch. Delete temp root: {tempRoot}");

            DirectoryDeleteResult deleteResult = await TryDeleteDirectoryIfExistsAsync(tempRoot, ct).ConfigureAwait(false);
            if (!deleteResult.Success)
            {
                throw new PatchOperationException(
                    $"Failed to delete existing patch temp directory '{tempRoot}'.",
                    targetServer,
                    PatchFailureStage.ResetTempDirectory,
                    deleteResult.Exception ?? new IOException($"Failed to delete existing patch temp directory: {tempRoot}"),
                    targetPath: tempRoot);
            }
        }

        try
        {
            Directory.CreateDirectory(tempRoot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PatchOperationException(
                $"Failed to prepare patch temp directory '{tempRoot}'.",
                targetServer,
                PatchFailureStage.PrepareTempDirectory,
                ex,
                targetPath: tempRoot);
        }

        List<PatchItem> items;
        try
        {
            items = await BuildPatchItemsAsync(
                targetServer,
                currentClientVersion,
                latestClientVersion,
                originClientPath,
                tempRoot,
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PatchOperationException(
                $"Failed to build patch item list for server '{targetServer}'.",
                targetServer,
                PatchFailureStage.BuildPatchItems,
                ex,
                targetPath: tempRoot);
        }

        Debug.WriteLine("패치 파일 목록(압축 푸는 순서대로 정렬):");
        foreach (PatchItem item in items)
        {
            Debug.WriteLine(
                $"\t[{item.FileName}]\n\t" +
                $"{item.DownloadUrl}\n\t" +
                $"{item.DownloadDestPath}\n\t" +
                $"{item.ExtractDestPath}\n");
        }

        PatchProgressState state = new()
        {
            // TotalCount = 다운로드해야할파일 갯수 + 압축해제해야할파일 갯수로 정의
            // 다운로드 하는 파일이 모두 압축파일이라고 가정하기 때문에 * 2
            TotalCount = items.Count * 2
        };

        void ReportProgressSnapshot()
        {
            if (progress is null)
                return;

            progress.Report(state.Snapshot());
        }

        Progress<DownloadProgress>? downloadProgress = null;
        if (progress is not null)
        {
            downloadProgress = new Progress<DownloadProgress>(p =>
            {
                double? percent = null;
                if (p.TotalBytes is long totalBytes && totalBytes > 0)
                    percent = (double)p.BytesReceived / totalBytes * 100.0;

                state.Update(s =>
                {
                    s.DownloadingPercent = percent;
                });

                ReportProgressSnapshot();
            });
        }

        Progress<ExtractionProgress>? extractionProgress = null;
        if (progress is not null)
        {
            extractionProgress = new Progress<ExtractionProgress>(p =>
            {
                state.Update(s =>
                {
                    s.ExtractingPercent = p.Percentage;
                });

                ReportProgressSnapshot();
            });
        }

        DownloadOptions downloadOptions = CreateDownloadOptions(options);

        bool patchSucceeded = false;

        try
        {
            try
            {
                await ApplyPatchAsync(
                    targetServer,
                    items,
                    downloadOptions: downloadOptions,
                    onBeforeDownload: item =>
                    {
                        state.Update(s =>
                        {
                            s.DownloadingFileName = item.FileName;
                            s.DownloadingPercent = 0;
                        });

                        ReportProgressSnapshot();
                    },
                    onAfterDownload: item =>
                    {
                        state.Update(s =>
                        {
                            s.DownloadedCount++;
                            s.DownloadingFileName = item.FileName;
                            s.DownloadingPercent = 100;
                        });

                        ReportProgressSnapshot();
                    },
                    onBeforeExtract: item =>
                    {
                        state.Update(s =>
                        {
                            s.ExtractingFileName = item.FileName;
                            s.ExtractingPercent = 0;
                        });

                        ReportProgressSnapshot();
                    },
                    onAfterExtract: item =>
                    {
                        state.Update(s =>
                        {
                            s.ExtractedCount++;
                            s.ExtractingFileName = item.FileName;
                            s.ExtractingPercent = 100;
                        });

                        ReportProgressSnapshot();
                    },
                    downloadProgress: downloadProgress,
                    extractionProgress: extractionProgress,
                    ct: ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and PatchOperationException)
            {
                throw new PatchOperationException(
                    $"Failed to download or extract patch items for server '{targetServer}'.",
                    targetServer,
                    PatchFailureStage.DownloadPatchItem,
                    ex,
                    targetPath: tempRoot);
            }

            try
            {
                ApplyMultiClientPatchIfNeeded(multiClientPatchArgs, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new PatchOperationException(
                    $"Failed to apply multi-client patch updates for server '{targetServer}'.",
                    targetServer,
                    PatchFailureStage.ApplyMultiClientPatch,
                    ex,
                    version: latestClientVersion,
                    targetPath: originClientPath);
            }

            patchSucceeded = true;
        }
        finally
        {
            // 성공적으로 전체 패치가 끝났을 때만 temp 루트를 정리한다.
            // 실패/중단 시에는 archive/temp/meta를 그대로 남겨 다음 실행에서
            // 이어받기 / 새로 받기 선택이 가능해야 한다.
            if (patchSucceeded && options.DeleteTempFilesAfterPatch)
            {
                DirectoryDeleteResult deleteResult = await TryDeleteDirectoryIfExistsAsync(tempRoot, ct).ConfigureAwait(false);
                if (!deleteResult.Success)
                {
                    Debug.WriteLine($"[PatchManager] Temp root cleanup failed after successful patch: {tempRoot}, Error: {deleteResult.Exception}");
                }
            }
        }
    }

    public async Task ApplyPatchAsync(
        GameServer targetServer,
        IReadOnlyList<PatchItem> items,
        DownloadOptions downloadOptions,
        Action<PatchItem>? onBeforeDownload = null,
        Action<PatchItem>? onAfterDownload = null,
        Action<PatchItem>? onBeforeExtract = null,
        Action<PatchItem>? onAfterExtract = null,
        IProgress<DownloadProgress>? downloadProgress = null,
        IProgress<ExtractionProgress>? extractionProgress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(downloadOptions);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ExtractorWorker worker = new(_extractor, 3, cts.Token);
        // A failed extractor must also unblock a download waiting to enqueue its next archive.
        Task stopOnExtractFailure = worker.Completion.ContinueWith(_ => cts.Cancel(),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        bool completedQueue = false;

        try
        {
            foreach (PatchItem item in items)
            {
                onBeforeDownload?.Invoke(item);

                try
                {
                    await _downloader.DownloadFileAsync(
                        item.DownloadUrl,
                        item.DownloadDestPath,
                        downloadOptions,
                        downloadProgress,
                        cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new PatchOperationException(
                        $"Failed to download patch item '{item.FileName}'.",
                        targetServer,
                        PatchFailureStage.DownloadPatchItem,
                        ex,
                        targetPath: item.DownloadDestPath,
                        fileName: item.FileName);
                }

                onAfterDownload?.Invoke(item);

                try
                {
                    await worker.EnqueueAsync(
                        new ExtractJob(
                            item.DownloadDestPath,
                            item.ExtractDestPath,
                            extractionProgress,
                            OnBeforeExtract: () => onBeforeExtract?.Invoke(item),
                            OnAfterExtract: () => onAfterExtract?.Invoke(item))).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new PatchOperationException(
                        $"Failed to queue extraction for patch item '{item.FileName}'.",
                        targetServer,
                        PatchFailureStage.ExtractPatchItem,
                        ex,
                        targetPath: item.ExtractDestPath,
                        fileName: item.FileName);
                }
            }

            worker.Complete();
            completedQueue = true;
            try
            {
                await worker.Completion.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new PatchOperationException(
                    "Failed while extracting downloaded patch items.",
                    targetServer,
                    PatchFailureStage.ExtractPatchItem,
                    ex);
            }
        }
        catch
        {
            cts.Cancel();
            if (!completedQueue) worker.Complete();
            // Do not acknowledge cancellation while extraction is still touching game files.
            try { await worker.Completion.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                throw new PatchOperationException("Failed while extracting downloaded patch items.",
                    targetServer, PatchFailureStage.ExtractPatchItem, ex);
            }
            throw;
        }
        finally
        {
            // Observe the continuation before disposing its cancellation source.
            try { await stopOnExtractFailure.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
    }

    private async Task<List<PatchItem>> BuildPatchItemsAsync(
    GameServer targetServer,
    int currentClientVersion,
    int latestClientVersion,
    string originClientPath,
    string tempRoot,
    CancellationToken ct)
    {
        HashSet<string> added = [];
        List<PatchItem> items = [];

        // 구버전부터 해도 되는 이유:
        // 동일한 파일이 여러 버전 구간에 등장하더라도 downloadUrl은 항상 최신 archive 경로를 가리킨다.
        // 압축 해제는 구버전부터 순서대로 수행되어야 하므로 이 순서가 자연스럽다.
        for (int version = currentClientVersion + 1; version <= latestClientVersion; ++version)
        {
            ct.ThrowIfCancellationRequested();

            VersionInfo? versionInfo = await DownloadVersionInfo(targetServer, version, ct);
            if (versionInfo is null)
                continue;

            foreach (VersionInfoRow row in versionInfo.Rows)
            {
                string relativeDirForUrl = NormalizeRelativeDirForUrl(row.RelativeDir);
                string relativeDirForPath = NormalizeRelativeDirForPath(row.RelativeDir);

                string relativeArchiveUrlPath = string.IsNullOrEmpty(relativeDirForUrl)
                    ? row.ZipFileName
                    : $"{relativeDirForUrl}/{row.ZipFileName}";

                string downloadUrl = GameServerHelper.GetPatchFileUrl(targetServer, relativeArchiveUrlPath);

                string downloadDestPath = string.IsNullOrEmpty(relativeDirForPath)
                    ? Path.Combine(tempRoot, row.ZipFileName)
                    : Path.Combine(tempRoot, relativeDirForPath, row.ZipFileName);

                string extractDestPath = string.IsNullOrEmpty(relativeDirForPath)
                    ? originClientPath
                    : Path.Combine(originClientPath, relativeDirForPath);

                EnsureContainedPath(tempRoot, downloadDestPath);
                EnsureContainedPath(originClientPath, extractDestPath);
                PatchItem item = new(
                    row.FileNameAfterUnZip,
                    new Uri(downloadUrl),
                    downloadDestPath,
                    extractDestPath);

                bool shouldAdd = added.Add(downloadUrl);
                if (shouldAdd)
                    items.Add(item);
            }
        }

        return items;
    }

    internal static CreateSymbolMultiClientArgs BuildExistingMultiClientPatchArgs(
        GameServer targetServer, string originClientPath, ClientSettings clientSettings)
    {
        return new CreateSymbolMultiClientArgs
        {
            InstallPath = originClientPath,
            DestPath2 = GameClientHelper.TryValidateCloneInstallPath(targetServer, clientSettings.Client2Path).Success
                ? clientSettings.Client2Path : string.Empty,
            DestPath3 = GameClientHelper.TryValidateCloneInstallPath(targetServer, clientSettings.Client3Path).Success
                ? clientSettings.Client3Path : string.Empty
        };
    }

    private static void ApplyMultiClientPatchIfNeeded(CreateSymbolMultiClientArgs args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.DestPath2) && string.IsNullOrWhiteSpace(args.DestPath3))
            return;

        CreateSymbolMultiClientResult result = new MultiClientService(ct).TryCreateSymbolMultiClient(args);
        if (!result.Success)
        {
            throw result.Exception is null
                ? new IOException($"다클라 패치 적용 실패: {result.Reason}")
                : new IOException($"다클라 패치 적용 실패: {result.Reason}", result.Exception);
        }
    }

    private static DownloadOptions CreateDownloadOptions(PatchRunOptions options)
    {
        DownloadExistingArtifactMode existingArtifactMode =
            options.ArchiveReuseMode == PatchArchiveReuseMode.RestartFromScratch
                ? DownloadExistingArtifactMode.RestartFromScratch
                : DownloadExistingArtifactMode.ResumeIfPossible;

        return new DownloadOptions(
            Overwrite: true,
            MaxRetries: 8,
            BufferSize: 1024 * 1024,
            ExistingArtifactMode: existingArtifactMode);
    }

    private static async Task<DirectoryDeleteResult> TryDeleteDirectoryIfExistsAsync(
    string path,
    CancellationToken ct,
    int maxAttempts = 5)
    {
        if (!Directory.Exists(path))
            return new DirectoryDeleteResult(true, path, null, null);

        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; ++attempt)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);

                if (!Directory.Exists(path))
                    return new DirectoryDeleteResult(true, path, null, null);

                lastException = new IOException($"Directory still exists after delete attempt: {path}");
            }
            catch (IOException ex)
            {
                lastException = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastException = ex;
            }

            Debug.WriteLine(
                $"[PatchManager] Delete temp root failed. " +
                $"attempt={attempt}/{maxAttempts}, " +
                $"path={path}, " +
                $"error={lastException?.Message}");

            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct).ConfigureAwait(false);
        }

        Debug.WriteLine(
            $"[PatchManager] Delete temp root gave up. " +
            $"path={path}, " +
            $"error={lastException}");

        return new DirectoryDeleteResult(
            false,
            path,
            DirectoryDeleteFailureStage.DeleteDirectory,
            lastException ?? new IOException($"Failed to delete directory: {path}"));
    }

    private static void EnsureContainedPath(string root, string target)
    {
        string basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(target);
        if (!path.Equals(Path.TrimEndingDirectorySeparator(basePath), StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Patch path escapes the selected game folder: {target}");
    }
}

