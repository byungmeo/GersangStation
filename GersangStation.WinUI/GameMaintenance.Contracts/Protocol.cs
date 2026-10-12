using Core;
using Core.Models;

namespace GameMaintenance;

public enum OperationKind { Patch, CreateMultiClient }

// Only game-maintenance data crosses the elevation boundary. Never send account credentials.
public sealed record OperationRequest(
    OperationKind Kind, GameServer Server, ClientSettings Settings,
    int CurrentVersion = 0, PatchRunOptions? PatchOptions = null,
    CreateSymbolMultiClientArgs? CloneOptions = null);

public sealed record PipeMessage(
    OperationRequest? Request = null, PatchProgress? Progress = null,
    RemoteFailure? Error = null, bool Complete = false, bool Canceled = false,
    bool Cancel = false);

public sealed record RemoteFailure(
    string Type, string Message, string Details, int HResult,
    Dictionary<string, string> Context, RemoteFailure? Inner)
{
    public static RemoteFailure Capture(Exception exception, int depth = 0)
    {
        Dictionary<string, string> context = [];
        foreach (var property in exception.GetType().GetProperties())
        {
            if (property.Name is "SourcePath" or "DestinationPath" or "ClientNumber" or "Stage"
                or "Server" or "Version" or "TargetPath" or "FileName")
                context[property.Name] = property.GetValue(exception)?.ToString() ?? string.Empty;
        }
        return new(exception.GetType().FullName ?? exception.GetType().Name, exception.Message,
            exception.ToString(), exception.HResult, context,
            depth < 16 && exception.InnerException is { } inner ? Capture(inner, depth + 1) : null);
    }

    public Exception Restore()
    {
        Exception inner = Inner?.Restore() ?? new RemoteOperationException(this);
        string Value(string key) => Context.GetValueOrDefault(key) ?? string.Empty;
        Exception result = Type switch
        {
            "Core.GameClientHelper+MultiClientSymbolicDirectoryConflictException" =>
                new GameClientHelper.MultiClientSymbolicDirectoryConflictException(Value("SourcePath"), Value("DestinationPath")),
            "Core.GameClientHelper+MultiClientCreationException" =>
                new GameClientHelper.MultiClientCreationException(Message, int.Parse(Value("ClientNumber")),
                    Value("SourcePath"), Value("DestinationPath"),
                    Enum.Parse<GameClientHelper.MultiClientCreationFailureStage>(Value("Stage")), inner),
            "Core.PatchManager+PatchOperationException" =>
                new PatchManager.PatchOperationException(Message, Enum.Parse<GameServer>(Value("Server")),
                    Enum.Parse<PatchManager.PatchFailureStage>(Value("Stage")), inner,
                    int.TryParse(Value("Version"), out int version) ? version : null,
                    Value("TargetPath"), Value("FileName")),
            _ => new RemoteOperationException(this, Inner?.Restore())
        };
        result.Data["WorkerException"] = Details;
        return result;
    }
}

public sealed class RemoteOperationException : IOException
{
    public string WorkerDetails { get; }
    public RemoteOperationException(RemoteFailure failure, Exception? inner = null)
        : base(failure.Message, inner)
    {
        WorkerDetails = failure.Details;
        HResult = failure.HResult;
    }
    public override string ToString() => base.ToString() + Environment.NewLine + WorkerDetails;
}
