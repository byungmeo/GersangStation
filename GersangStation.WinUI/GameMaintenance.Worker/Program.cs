using Core;
using Core.Models;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading.Channels;

namespace GameMaintenance.Worker;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try { return RunAsync(args).GetAwaiter().GetResult(); }
        catch { return 2; } // No UI or writes before authenticating the calling app.
    }

    private static async Task<int> RunAsync(string[] args)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return 3;
        if (args.Length != 2 || !Guid.TryParseExact(args[0], "N", out _) || !int.TryParse(args[1], out int parentPid)) return 2;
        using Process parent = Process.GetProcessById(parentPid);
        string expectedParent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "GersangStation.exe"));
        if (!string.Equals(parent.MainModule?.FileName, expectedParent, StringComparison.OrdinalIgnoreCase)) return 3;

        using var pipe = new NamedPipeClientStream(".", "GersangStation.Maintenance." + args[0], PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
        using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await pipe.ConnectAsync(connectTimeout.Token);
        PipePeer.Verify(pipe, parentPid, peerIsServer: true);
        await using var connection = new PipeConnection(pipe);
        PipeMessage first = await connection.ReceiveAsync(connectTimeout.Token);
        OperationRequest request = first.Request ?? throw new InvalidDataException("Missing maintenance request.");

        using var operation = new CancellationTokenSource();
        using var requestedCancellation = new CancellationTokenSource();
        using var lifetime = new CancellationTokenSource();
        Task controls = ReadControlsAsync(connection, operation, requestedCancellation, lifetime.Token);
        Task parentExit = WatchParentAsync(parent, operation, lifetime.Token);
        var messages = Channel.CreateBounded<PipeMessage>(new BoundedChannelOptions(128) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        Task sender = SendMessagesAsync(connection, messages.Reader, operation, lifetime);
        PipeMessage result;
        List<RemoteFailure> diagnostics = [];
        try
        {
            await Task.Run(async () =>
            {
                Validate(request);
                operation.Token.ThrowIfCancellationRequested();
                if (request.Kind == OperationKind.Patch)
                    await new PatchService().RunAsync(request.Server, request.CurrentVersion, request.Settings.InstallPath,
                        request.PatchOptions!, request.Settings,
                        new InlineProgress<PatchProgress>(p => messages.Writer.TryWrite(new(Progress: p))), operation.Token,
                        ex => diagnostics.Add(RemoteFailure.Capture(ex)));
                else
                {
                    var cloneResult = new MultiClientService(operation.Token).TryCreateSymbolMultiClient(request.CloneOptions!);
                    if (!cloneResult.Success) throw cloneResult.Exception ?? new IOException(cloneResult.Reason);
                }
            });
            result = new(Complete: true, Diagnostics: diagnostics.ToArray());
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested && requestedCancellation.IsCancellationRequested)
        { result = new(Complete: true, Canceled: true); }
        catch (Exception ex) { result = new(Complete: true, Error: RemoteFailure.Capture(ex)); }
        try
        {
            await messages.Writer.WriteAsync(result, lifetime.Token);
            messages.Writer.TryComplete();
            await sender;
        }
        finally
        {
            lifetime.Cancel();
            try { await Task.WhenAll(controls, parentExit); } catch (OperationCanceledException) { }
        }
        return result.Error is null ? 0 : 1;
    }

    private static void Validate(OperationRequest request)
    {
        if (!Enum.IsDefined(request.Kind) || !Enum.IsDefined(request.Server)) throw new InvalidDataException("Invalid operation/server.");
        var validation = GameClientHelper.TryValidateInstallPath(request.Server, request.Settings.InstallPath);
        if (!validation.Success) throw new InvalidDataException(validation.Reason, validation.Exception);
        if (request.Kind == OperationKind.Patch)
        {
            if (request.CurrentVersion <= 0 || request.CurrentVersion == int.MaxValue || request.PatchOptions is null
                || !Enum.IsDefined(request.PatchOptions.ArchiveReuseMode)) throw new InvalidDataException("Invalid patch options.");
            return;
        }
        CreateSymbolMultiClientArgs clone = request.CloneOptions ?? throw new InvalidDataException("Missing clone options.");
        if (!SamePath(clone.InstallPath, request.Settings.InstallPath)
            || (!string.IsNullOrEmpty(clone.DestPath2) && !SamePath(clone.DestPath2, request.Settings.Client2Path))
            || (!string.IsNullOrEmpty(clone.DestPath3) && !SamePath(clone.DestPath3, request.Settings.Client3Path)))
            throw new InvalidDataException("Clone destinations must be station-managed sibling folders.");
        int version = PatchManager.GetCurrentClientVersion(clone.InstallPath) ?? throw new InvalidDataException("Missing game version.");
        clone.LayoutPolicy = version >= GameClientHelper.MultiClientLayoutBoundaryVersion
            ? GameClientHelper.MultiClientLayoutPolicy.V34100OrLater : GameClientHelper.MultiClientLayoutPolicy.Legacy;
    }

    private static bool SamePath(string first, string second) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)), StringComparison.OrdinalIgnoreCase);

    private static async Task ReadControlsAsync(PipeConnection connection, CancellationTokenSource operation,
        CancellationTokenSource requestedCancellation, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
                if ((await connection.ReceiveAsync(ct)).Cancel)
                {
                    requestedCancellation.Cancel();
                    operation.Cancel();
                }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch { operation.Cancel(); } // Lost app connection means stop, not continue unattended.
    }

    private static async Task WatchParentAsync(Process parent, CancellationTokenSource operation, CancellationToken ct)
    {
        try { await parent.WaitForExitAsync(ct); operation.Cancel(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch { operation.Cancel(); }
    }

    private static async Task SendMessagesAsync(PipeConnection connection, ChannelReader<PipeMessage> messages,
        CancellationTokenSource operation, CancellationTokenSource lifetime)
    {
        try { await foreach (var message in messages.ReadAllAsync(lifetime.Token)) await connection.SendAsync(message, lifetime.Token); }
        catch { operation.Cancel(); lifetime.Cancel(); throw; }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    { public void Report(T value) => report(value); }
}
