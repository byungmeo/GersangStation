using System.Buffers.Binary;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace GameMaintenance;

public sealed class PipeConnection(Stream stream) : IAsyncDisposable
{
    private const int MaxMessageBytes = 4 * 1024 * 1024;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public async Task SendAsync(PipeMessage message, CancellationToken ct = default)
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(message);
        if (data.Length > MaxMessageBytes) throw new InvalidDataException("Maintenance message is too large.");
        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, data.Length);
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(header, ct).ConfigureAwait(false);
            await stream.WriteAsync(data, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally { _writeGate.Release(); }
    }

    public async Task<PipeMessage> ReceiveAsync(CancellationToken ct = default)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, ct).ConfigureAwait(false);
        int count = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (count <= 0 || count > MaxMessageBytes) throw new InvalidDataException("Invalid maintenance message length.");
        byte[] data = new byte[count];
        await stream.ReadExactlyAsync(data, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<PipeMessage>(data)
            ?? throw new InvalidDataException("Empty maintenance message.");
    }

    public ValueTask DisposeAsync() => stream.DisposeAsync();
}

public static class PipePeer
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint pid);

    public static void Verify(PipeStream pipe, int expectedPid, bool peerIsServer)
    {
        uint pid;
        bool ok = peerIsServer ? GetNamedPipeServerProcessId(pipe.SafePipeHandle, out pid)
            : GetNamedPipeClientProcessId(pipe.SafePipeHandle, out pid);
        if (!ok || pid != expectedPid)
            throw new UnauthorizedAccessException("The maintenance pipe peer does not match the launched process.");
    }
}
