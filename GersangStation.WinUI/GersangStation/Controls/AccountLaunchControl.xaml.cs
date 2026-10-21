using GersangStation.Services;
using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace GersangStation.Controls;

/// <summary>계정 선택과 실행 UI만 소유합니다. 로그인/실행 처리는 요청 이벤트로 전달합니다.</summary>
public sealed partial class AccountLaunchControl : UserControl
{
    private GameLaunchNotice? _pendingLaunchWarning;
    private GameLaunchNotice? _activeLaunchNotice;
    private readonly HashSet<TeachingTip> _closingLaunchTips = [];
    private TeachingTip? _activeLaunchTip;
    private int _launchNoticeGeneration;

    public AccountLaunchControl()
    {
        InitializeComponent();
        Unloaded += (_, _) => ClearLaunchWarning();
    }

    internal void ShowLaunchWarning(string title, string message)
        => ShowLaunchNotice(new(title, message));

    internal void ShowLaunchNotice(GameLaunchNotice notice)
    {
        if (!IsLoaded) return;
        ClearLaunchWarning();
        _pendingLaunchWarning = notice;
        OpenPendingLaunchWarning();
    }

    private void OpenPendingLaunchWarning()
    {
        if (_closingLaunchTips.Count != 0 || !IsLoaded || _pendingLaunchWarning is not { } warning) return;
        _pendingLaunchWarning = null;
        _activeLaunchNotice = warning;
        TeachingTip tip = warning.Severity == GameLaunchNoticeSeverity.Critical
            ? LaunchFailureTeachingTip : LaunchWarningTeachingTip;
        StackPanel actions = warning.Severity == GameLaunchNoticeSeverity.Critical
            ? LaunchFailureActions : LaunchWarningActions;
        actions.Children.Clear();
        foreach (GameLaunchNoticeAction action in warning.Actions)
        {
            Button button = new() { Content = action.Label, Tag = action };
            button.Click += LaunchNoticeAction_Click;
            if (!string.IsNullOrWhiteSpace(action.LeadingText))
            {
                Grid row = new() { ColumnSpacing = 4, HorizontalAlignment = HorizontalAlignment.Left };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Children.Add(new TextBlock
                {
                    Text = action.LeadingText,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                });
                Grid.SetColumn(button, 1);
                row.Children.Add(button);
                actions.Children.Add(row);
            }
            else
            {
                actions.Children.Add(button);
            }
        }
        _activeLaunchTip = tip;
        tip.Title = warning.Title;
        tip.Subtitle = warning.Message;
        tip.IsOpen = true;
    }

    internal void ClearLaunchWarning()
    {
        _pendingLaunchWarning = null;
        _activeLaunchNotice = null;
        _launchNoticeGeneration++;
        foreach (TeachingTip tip in new[] { LaunchWarningTeachingTip, LaunchFailureTeachingTip })
        {
            if (!tip.IsOpen) continue;
            _closingLaunchTips.Add(tip);
            try { tip.IsOpen = false; }
            catch (Exception ex)
            {
                _closingLaunchTips.Remove(tip);
                App.ExceptionHandler.RecordRecoverable(ex, "AccountLaunchControl.CloseLaunchWarning");
            }
        }
    }

    private void LaunchWarningTeachingTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
        => _closingLaunchTips.Add(sender);

    private void LaunchWarningTeachingTip_Closed(TeachingTip sender, TeachingTipClosedEventArgs args)
    {
        _closingLaunchTips.Remove(sender);
        if (_pendingLaunchWarning is null && ReferenceEquals(_activeLaunchTip, sender))
        {
            _activeLaunchNotice = null;
            _launchNoticeGeneration++;
        }
        if (_pendingLaunchWarning is null) return;
        // Closed 콜백 안에서 즉시 열면 WinUI의 남은 Popup 정리가 새 팁까지 닫을 수 있습니다.
        try
        {
            if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                try { OpenPendingLaunchWarning(); }
                catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "AccountLaunchControl.ReopenLaunchWarning"); }
            }))
            {
                _pendingLaunchWarning = null;
                App.ExceptionHandler.RecordRecoverable(new InvalidOperationException("게임 실행 안내 재표시를 UI에 예약하지 못했습니다."),
                    "AccountLaunchControl.ReopenLaunchWarning");
            }
        }
        catch (Exception ex)
        {
            _pendingLaunchWarning = null;
            App.ExceptionHandler.RecordRecoverable(ex, "AccountLaunchControl.ReopenLaunchWarning");
        }
    }

    private async void LaunchNoticeAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GameLaunchNoticeAction action } button ||
            _activeLaunchNotice is not { } notice) return;
        int generation = _launchNoticeGeneration;
        button.IsEnabled = false;
        try
        {
            await action.Execute();
            if (generation == _launchNoticeGeneration) ClearLaunchWarning();
        }
        catch (Exception ex)
        {
            App.ExceptionHandler.RecordRecoverable(ex, "AccountLaunchControl.NoticeAction");
            if (generation == _launchNoticeGeneration && _activeLaunchTip?.IsOpen == true)
            {
                try { ShowLaunchNotice(notice with
                {
                    Message = notice.Message + "\n선택한 작업을 열지 못했습니다. 잠시 후 다시 시도해 주세요.",
                    Severity = GameLaunchNoticeSeverity.Critical
                }); }
                catch (Exception feedbackException) { App.ExceptionHandler.RecordRecoverable(feedbackException, "AccountLaunchControl.NoticeActionFeedback"); }
            }
        }
        finally
        {
            try { button.IsEnabled = true; }
            catch (Exception ex) { App.ExceptionHandler.RecordRecoverable(ex, "AccountLaunchControl.RestoreNoticeAction"); }
        }
    }

    public event EventHandler? BrowserRequested;
    public event EventHandler? ExecuteRequested;

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(AccountLaunchControl), new PropertyMetadata(string.Empty));
    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty ClientIndexProperty =
        DependencyProperty.Register(nameof(ClientIndex), typeof(int), typeof(AccountLaunchControl), new PropertyMetadata(0));
    public int ClientIndex
    {
        get => (int)GetValue(ClientIndexProperty);
        set => SetValue(ClientIndexProperty, value);
    }

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(object), typeof(AccountLaunchControl), new PropertyMetadata(null));
    public object? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty SelectedAccountIdProperty =
        DependencyProperty.Register(nameof(SelectedAccountId), typeof(string), typeof(AccountLaunchControl), new PropertyMetadata(string.Empty));
    public string? SelectedAccountId
    {
        get => (string?)GetValue(SelectedAccountIdProperty);
        set => SetValue(SelectedAccountIdProperty, value);
    }

    public static readonly DependencyProperty CanBrowseProperty =
        DependencyProperty.Register(nameof(CanBrowse), typeof(bool), typeof(AccountLaunchControl), new PropertyMetadata(false));
    public bool CanBrowse
    {
        get => (bool)GetValue(CanBrowseProperty);
        set => SetValue(CanBrowseProperty, value);
    }

    public static readonly DependencyProperty LaunchStateProperty =
        DependencyProperty.Register(nameof(LaunchState), typeof(GameLaunchButtonState), typeof(AccountLaunchControl), new PropertyMetadata(null));
    public GameLaunchButtonState? LaunchState
    {
        get => (GameLaunchButtonState?)GetValue(LaunchStateProperty);
        set => SetValue(LaunchStateProperty, value);
    }

    public bool CanBrowseAccount(bool canBrowse, string? accountId)
        => canBrowse && !string.IsNullOrWhiteSpace(accountId);

    public string AutomationIdFor(int clientIndex, string controlName)
        => $"Client{clientIndex + 1}{controlName}";

    private void BrowserButton_Click(object sender, RoutedEventArgs e)
    {
        if (CanBrowseAccount(CanBrowse, SelectedAccountId))
            BrowserRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        // 처리 중에는 같은 요청 이벤트가 기존 실행 취소 경로로 연결됩니다.
        if (LaunchState?.IsEnabled == true)
            ExecuteRequested?.Invoke(this, EventArgs.Empty);
    }
}
