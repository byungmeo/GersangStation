namespace GersangStation.Shared.Input;

/// <summary>
/// Describes a native hook warning or a background failure without choosing host UI or logging policy.
/// A hook installation warning keeps polling active; an unexpected callback failure stops monitoring.
/// </summary>
public sealed record MouseConfinementDiagnostic(string Operation, Exception Exception, bool MonitoringStopped);
