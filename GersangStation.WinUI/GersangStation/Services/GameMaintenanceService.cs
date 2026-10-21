using Core;
using Core.Models;
using GameMaintenance;
using GersangStation.Diagnostics;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GersangStation.Services;

/// <summary>작업 하나당 승격된 프로세스를 실행합니다. 계정 저장소/비밀번호는 전달하지 않습니다.</summary>
internal static class GameMaintenanceService
{
    // Serialize maintenance writes even when different pages/launch slots request them concurrently.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static Task PatchAsync(GameServer server, int version, ClientSettings settings,
        PatchRunOptions options, IProgress<PatchProgress>? progress, CancellationToken ct)
        => RunAsync(new(OperationKind.Patch, server, settings, version, options), progress, ct);

    public static Task CreateMultiClientAsync(GameServer server, ClientSettings settings,
        CreateSymbolMultiClientArgs args, CancellationToken ct = default)
        => RunAsync(new(OperationKind.CreateMultiClient, server, settings, CloneOptions: args), null, ct);

    private static async Task RunAsync(OperationRequest request, IProgress<PatchProgress>? progress, CancellationToken ct)
    {
        // Snapshot before any await: editing another page must not mutate the approved request.
        request = JsonSerializer.Deserialize<OperationRequest>(JsonSerializer.Serialize(request))!;
        nint owner = App.CurrentWindow is { } window ? WinRT.Interop.WindowNative.GetWindowHandle(window) : 0;
        try
        {
            try { await Gate.WaitAsync(ct); }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            { throw new MaintenanceCanceledException(MaintenanceCancellationReason.BeforeStart, ct, ex); }
            try { await ExecuteAsync(request, progress, owner, ct); }
            finally { Gate.Release(); }
        }
        catch (MaintenanceCanceledException) { throw; }
        catch (OperationCanceledException ex)
        {
            // 원인이 확인되지 않은 취소 예외가 공통 경계에서 조용히 무시되지 않게 합니다.
            throw new IOException("게임 작업이 예기치 않게 중단되었습니다. 완료 상태를 확인해 주세요.", ex);
        }
    }

    private static async Task ExecuteAsync(OperationRequest request, IProgress<PatchProgress>? progress, nint owner, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            throw new MaintenanceCanceledException(MaintenanceCancellationReason.BeforeStart, ct);
        string exe = Path.Combine(AppContext.BaseDirectory, "Maintenance", "GersangStation.GameMaintenance.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("게임 작업용 실행 파일을 찾지 못했습니다. 앱을 복구하거나 다시 설치해 주세요.", exe);
        string nonce = Guid.NewGuid().ToString("N");
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(identity.User!, PipeAccessRights.FullControl, AccessControlType.Allow));
        // Permit credential-based UAC by another administrator, not just the logged-in user.
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.ReadWrite, AccessControlType.Allow));
        using var pipe = NamedPipeServerStreamAcl.Create("GersangStation.Maintenance." + nonce,
            PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance,
            4096, 4096, security);
        using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task connected = pipe.WaitForConnectionAsync(connectTimeout.Token);
        try
        {
            Process process;
            try { process = await LaunchAsync(exe, nonce, owner); }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            { throw new MaintenanceCanceledException(MaintenanceCancellationReason.ElevationDeclined, ct, ex); }
            using (process)
            {
                try
                {
                    // The timeout starts AFTER the user has answered UAC, not while the prompt is open.
                    connectTimeout.CancelAfter(TimeSpan.FromSeconds(60));
                    Task exited = process.WaitForExitAsync();
                    if (await Task.WhenAny(connected, exited) == exited && !connected.IsCompletedSuccessfully)
                        throw new IOException($"게임 작업 프로세스가 연결 전에 종료되었습니다. ExitCode={process.ExitCode}");
                    try { await connected; }
                    catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
                    { throw new MaintenanceCanceledException(MaintenanceCancellationReason.BeforeStart, ct, ex); }
                    catch (OperationCanceledException ex)
                    { throw new TimeoutException("게임 작업 프로세스에 연결하는 시간이 초과되었습니다. 작업은 시작하지 않았습니다.", ex); }
                    PipePeer.Verify(pipe, process.Id, peerIsServer: false);
                    await using var connection = new PipeConnection(pipe);
                    using var lifetime = new CancellationTokenSource();
                    // If canceled while UAC was open, disconnect without ever sending a write request.
                    if (ct.IsCancellationRequested)
                        throw new MaintenanceCanceledException(MaintenanceCancellationReason.BeforeStart, ct);
                    try { await connection.SendAsync(new(Request: request), connectTimeout.Token); }
                    catch (OperationCanceledException ex)
                    { throw new IOException("게임 작업 요청을 전달하는 중 연결이 중단되어 작업 상태를 확인하지 못했습니다.", ex); }
                    Task cancellation = SendCancellationAsync(connection, ct, lifetime);
                    try
                    {
                        while (true)
                        {
                            PipeMessage message;
                            try { message = await connection.ReceiveAsync(lifetime.Token); }
                            catch (OperationCanceledException ex)
                            { throw new IOException("취소 요청 후 게임 작업의 완료 여부를 확인하지 못했습니다. 작업 상태를 확인해 주세요.", ex); }
                            catch (IOException ex)
                            { throw new IOException("게임 작업 프로세스와의 연결이 끊겨 완료 여부를 확인하지 못했습니다.", ex); }
                            if (message.Progress is { } update) progress?.Report(update);
                            if (!message.Complete) continue;
                            if (message.Diagnostics is { } diagnostics)
                                foreach (RemoteFailure diagnostic in diagnostics)
                                {
                                    var diagnosticException = new RemoteOperationException(diagnostic);
                                    diagnosticException.Data["WorkerException"] = diagnostic.Details;
                                    App.ExceptionHandler.RecordRecoverable(diagnosticException,
                                        $"GameMaintenance.{request.Kind}.PostCompletionCleanup.{request.Server}");
                                }
                            if (message.Error is { } error) throw error.Restore();
                            if (message.Canceled)
                            {
                                if (ct.IsCancellationRequested)
                                    throw new MaintenanceCanceledException(MaintenanceCancellationReason.UserCanceled, ct);
                                throw new IOException("취소를 요청하지 않았지만 게임 작업이 중단되었습니다. 작업 상태를 확인해 주세요.");
                            }
                            return;
                        }
                    }
                    finally
                    {
                        lifetime.Cancel();
                        await cancellation;
                        // Disconnect on completion/error. The worker also cancels when its app/pipe disappears.
                    }
                }
                finally
                {
                    if (pipe.IsConnected) pipe.Disconnect();
                    // Keep the serialization gate until the worker has stopped writing, even on cancellation.
                    await process.WaitForExitAsync();
                }
            }
        }
        finally
        {
            connectTimeout.Cancel();
            try { await connected; }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException) { }
        }
    }

    private static async Task SendCancellationAsync(PipeConnection connection, CancellationToken ct, CancellationTokenSource lifetime)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
        try { await Task.Delay(Timeout.Infinite, linked.Token); }
        catch (OperationCanceledException) { }
        if (!ct.IsCancellationRequested || lifetime.IsCancellationRequested) return;
        lifetime.CancelAfter(TimeSpan.FromSeconds(30));
        try { await connection.SendAsync(new(Cancel: true), lifetime.Token); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException) { lifetime.Cancel(); }
    }

    private static Task<Process> LaunchAsync(string exe, string nonce, nint owner)
    {
        var completion = new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var start = new ProcessStartInfo(exe)
                {
                    UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = Path.GetDirectoryName(exe)!, ErrorDialogParentHandle = owner
                };
                start.ArgumentList.Add(nonce);
                start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                completion.SetResult(Process.Start(start) ?? throw new IOException("게임 작업 프로세스를 시작하지 못했습니다."));
            }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true, Name = "Game maintenance elevation" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
