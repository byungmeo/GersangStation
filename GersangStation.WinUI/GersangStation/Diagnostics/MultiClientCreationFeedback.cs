using Core;
using Core.Models;
using GersangStation.Main;
using GersangStation.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;

namespace GersangStation.Diagnostics;

/// <summary>생성 진입점과 무관한 동일 문구·아이콘·해결 동작을 기존 TeachingTip에 표시합니다.</summary>
internal sealed class MultiClientCreationFeedback(TeachingTip tip)
{
    private Action? _action;

    internal void Show(GameServer server, MultiClientCreationResult result)
    {
        _action = null;
        tip.ActionButtonContent = null;
        tip.CloseButtonContent = "닫기";
        tip.IconSource = result.Success || result.Canceled ? null : new FontIconSource
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = "\uEA39"
        };
        tip.Title = result.Canceled ? "다클라 생성 취소" : result.Success ? "다클라 생성 완료" : "다클라 생성 실패";
        string message = result.Message;

        if (!result.Success && !result.Canceled)
        {
            var conflict = FindException<GameClientHelper.MultiClientSymbolicDirectoryConflictException>(result.Exception);
            if (conflict is not null)
            {
                message = $"다클라 경로에 기존 일반 폴더가 있어 생성할 수 없습니다.\n충돌 경로: {conflict.DestinationPath}\n폴더 내용을 확인하고 충돌을 해결한 후 다시 시도해 주세요.";
                string? parent = TryGetParent(conflict.DestinationPath);
                if (parent is not null)
                {
                    tip.ActionButtonContent = "상위 폴더 열기";
                    _action = () =>
                    {
                        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException(parent);
                        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                        start.ArgumentList.Add(parent);
                        Process.Start(start);
                    };
                }
            }
            else if (PathPermissionFailure.IsPermissionFailure(result.Exception))
            {
                message = "다클라 생성에 필요한 파일 접근 권한이 부족합니다. 해결 방법을 확인한 후 다시 시도해 주세요.";
                tip.ActionButtonContent = "해결 방법 확인하기";
                _action = () =>
                {
                    if (App.CurrentWindow is MainWindow window)
                        window.NavigateToWebViewPageByLinkKey(AppLinkKeys.HelpPermissionMultiClient);
                };
            }
        }
        tip.Subtitle = $"{GameServerHelper.GetServerDisplayName(server)}\n{message}";
        tip.IsOpen = true;
    }

    internal void InvokeAction()
    {
        if (_action is null) return;
        tip.IsOpen = false;
        try { _action(); }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, "MultiClientCreation.ResultAction");
            tip.Subtitle = "해결 방법을 열지 못했습니다. 잠시 후 다시 시도해 주세요.";
            tip.IsOpen = true;
        }
    }

    private static T? FindException<T>(Exception? exception) where T : Exception
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is T matched) return matched;
        return null;
    }

    private static string? TryGetParent(string path)
    {
        try { return Directory.GetParent(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))?.FullName; }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException) { return null; }
    }
}
