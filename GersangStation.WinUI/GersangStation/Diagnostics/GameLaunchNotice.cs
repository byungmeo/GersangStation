using System;
using System.Threading.Tasks;

namespace GersangStation.Diagnostics;

internal enum GameLaunchNoticeSeverity { Caution, Critical }

internal sealed record GameLaunchNoticeAction(string Label, Func<Task> Execute)
{
    public string? LeadingText { get; init; }
}

internal sealed record GameLaunchNotice(string Title, string Message,
    GameLaunchNoticeSeverity Severity = GameLaunchNoticeSeverity.Caution,
    params GameLaunchNoticeAction[] Actions);
