using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace SideDrawer.Controls;

/// <summary>XAML에 직접 선언하는, 레이아웃 공간을 차지하지 않는 방향별 팝업 패널.</summary>
public sealed partial class Drawer : ContentControl
{
    private Popup? _popup;
    private DrawerSurfacePanel? _surface;
    private Border? _panel;
    private Border? _overlay;
    private ContentPresenter? _contentPresenter;
    private ContentControl? _contentRoot;
    private TranslateTransform? _translation;
    private Storyboard? _animation;
    private CancellationTokenSource? _lifetime;
    private DrawerClosingEventArgs? _closingArgs;
    private DrawerCoordinator? _coordinator;
    private Task? _openingTask;
    private Task<bool>? _closingTask;
    private long _session;
    private bool _updatingIsOpen;
    private bool _reopenAfterClose;

    public Drawer()
    {
        DefaultStyleKey = typeof(Drawer);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += (_, _) => UpdatePopupTheme();
    }

    public event EventHandler? Opened;
    public event EventHandler<DrawerClosingEventArgs>? Closing;
    public event EventHandler<DrawerClosedEventArgs>? Closed;
    public event EventHandler<DrawerInteractionFailedEventArgs>? InteractionFailed;
    public Exception? LastInteractionException { get; private set; }

    // 선언 위치는 유지하고 템플릿의 표시 부분만 Popup으로 옮깁니다(TeachingTip과 같은 패턴).
    protected override Size MeasureOverride(Size availableSize) => default;
    protected override Size ArrangeOverride(Size finalSize) => default;

    protected override void OnApplyTemplate()
    {
        if (_coordinator is not null)
            throw new InvalidOperationException("Change the Drawer template only while it is closed.");
        if (_popup is not null) _popup.Child = null;
        if (_contentPresenter is not null) _contentPresenter.ClearValue(ContentPresenter.ContentProperty);
        base.OnApplyTemplate();
        var container = GetTemplateChild("PART_Container") as Border
            ?? throw new InvalidOperationException("Drawer template requires PART_Container (Border).");
        _surface = GetTemplateChild("PART_Surface") as DrawerSurfacePanel
            ?? throw new InvalidOperationException("Drawer template requires PART_Surface (DrawerSurfacePanel).");
        _panel = GetTemplateChild("PART_Panel") as Border
            ?? throw new InvalidOperationException("Drawer template requires PART_Panel (Border).");
        _overlay = GetTemplateChild("PART_Overlay") as Border
            ?? throw new InvalidOperationException("Drawer template requires PART_Overlay (Border).");
        _contentPresenter = GetTemplateChild("PART_Content") as ContentPresenter;
        _contentRoot = GetTemplateChild("PART_ContentRoot") as ContentControl;
        container.Child = null;
        _translation = new TranslateTransform();
        _panel.RenderTransform = _translation;
        _popup = new Popup { Child = _surface, IsLightDismissEnabled = false, ShouldConstrainToRootBounds = true };
        _surface.DataContext = DataContext;
        _surface.PointerPressed += OnSurfacePointerPressed;
        _surface.KeyDown += OnSurfaceKeyDown;
        DataContextChanged -= OnDataContextChanged;
        DataContextChanged += OnDataContextChanged;
        UpdatePopupTheme();
        UpdateModality();
    }

    /// <summary>Host 생략 시 가장 가까운 컨테이너. 중첩에서는 부모 Drawer의 Host를 상속합니다.</summary>
    public Task OpenAsync()
    {
        VerifyAccess();
        if (!IsLoaded || XamlRoot is null)
            throw new InvalidOperationException("Load the Drawer declaration before calling OpenAsync; IsOpen=True can be set before Loaded.");
        if (_coordinator is not null)
        {
            if (State is DrawerState.ClosePending or DrawerState.Closing)
                throw new InvalidOperationException("Await the close request before reopening this Drawer.");
            return _openingTask ?? Task.CompletedTask;
        }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _openingTask = completion.Task; // Opened/Loaded 재진입보다 먼저 공개합니다.
        _ = CompleteOpenAsync(completion);
        return completion.Task;
    }

    private async Task CompleteOpenAsync(TaskCompletionSource completion)
    {
        long session = ++_session;
        _lifetime = new CancellationTokenSource();
        try
        {
            ApplyTemplate();
            var coordinator = DrawerCoordinator.For(XamlRoot);
            ResolvedParentDrawer = coordinator.ResolveParent(this);
            ResolvedHost = Host ?? ResolvedParentDrawer?.ResolvedHost ?? DrawerCoordinator.FindContainer(this);
            if (ResolvedHost is null)
                throw new InvalidOperationException("No parent container was found. Set Host to a loaded FrameworkElement.");
            _coordinator = coordinator;
            coordinator.Mount(this);
            SetState(DrawerState.Opening);
            SetIsOpen(true);
            AutomationProperties.SetName(_surface!, AutomationProperties.GetName(this));
            UpdatePopupBounds();
            _popup!.XamlRoot = XamlRoot;
            // 표시 전에 시작 위치를 준비해 첫 프레임에 열린 패널이 먼저 나타나지 않도록 합니다.
            Point closedOffset = GetClosedOffset();
            _translation!.X = closedOffset.X;
            _translation.Y = closedOffset.Y;
            _overlay!.Opacity = 0;
            _surface!.Measure(new Size(_surface.Width, _surface.Height));
            _surface.Arrange(new Rect(0, 0, _surface.Width, _surface.Height));
            _popup.IsOpen = true;
            coordinator.FocusDrawer(this);
            await AnimateAsync(true, _lifetime.Token);
            if (session != _session || _coordinator != coordinator) { completion.TrySetResult(); return; }
            SetState(DrawerState.Open);
            Opened?.Invoke(this, EventArgs.Empty);
            completion.TrySetResult();
        }
        catch (OperationCanceledException) when (session != _session)
        {
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            if (session == _session && State != DrawerState.Open)
            {
                _coordinator?.Unmount(this, restoreFocus: true);
                ResetSession();
            }
            completion.TrySetException(exception);
        }
        finally
        {
            if (ReferenceEquals(_openingTask, completion.Task)) _openingTask = null;
        }
    }

    /// <returns>닫혔거나 이미 닫혀 있으면 true, Handled 또는 열린 자식 때문에 유지되면 false.</returns>
    public Task<bool> CloseAsync(DrawerCloseReason reason = DrawerCloseReason.Programmatic)
    {
        VerifyAccess();
        if (reason == DrawerCloseReason.HostUnloaded)
            throw new ArgumentException("HostUnloaded is reserved for lifetime cleanup.", nameof(reason));
        if (_closingTask is not null) return _closingTask;
        if (_coordinator is null) { SetIsOpen(false); return Task.FromResult(true); }
        if (!_coordinator.CanClose(this)) { SetIsOpen(true); return Task.FromResult(false); }
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _closingTask = completion.Task;
        _ = CompleteCloseAsync(reason, completion, _session);
        return completion.Task;
    }

    private async Task CompleteCloseAsync(DrawerCloseReason reason, TaskCompletionSource<bool> completion, long session)
    {
        DrawerClosingEventArgs? args = null;
        try
        {
            if (State == DrawerState.Opening && _openingTask is not null) await _openingTask;
            DrawerCoordinator? coordinator = _coordinator;
            if (session != _session || coordinator is null || !coordinator.CanClose(this))
            {
                if (session == _session && coordinator is not null) SetIsOpen(true);
                completion.TrySetResult(false);
                return;
            }
            SetState(DrawerState.ClosePending);
            _closingArgs = args = new DrawerClosingEventArgs(reason);
            try { Closing?.Invoke(this, args); }
            catch { args.Abort(); throw; }
            finally { args.Seal(); }
            bool accepted = await args.WaitAsync();
            if (session != _session || _coordinator != coordinator) { completion.TrySetResult(false); return; }
            if (!accepted || !coordinator.CanClose(this))
            {
                SetState(DrawerState.Open);
                SetIsOpen(true);
                completion.TrySetResult(false);
                return;
            }
            SetState(DrawerState.Closing);
            await AnimateAsync(false, _lifetime!.Token);
            if (session != _session || _coordinator != coordinator) { completion.TrySetResult(false); return; }
            _popup!.IsOpen = false;
            coordinator.Unmount(this, restoreFocus: Modal || reason != DrawerCloseReason.OutsidePointer);
            ResetSession();
            Closed?.Invoke(this, new DrawerClosedEventArgs(reason));
            completion.TrySetResult(true);
        }
        catch (OperationCanceledException) when (session != _session || _coordinator is null)
        {
            completion.TrySetResult(false);
        }
        catch (Exception exception)
        {
            if (session == _session && _coordinator is not null) { SetState(DrawerState.Open); SetIsOpen(true); }
            completion.TrySetException(exception);
        }
        finally
        {
            if (ReferenceEquals(_closingArgs, args)) _closingArgs = null;
            if (ReferenceEquals(_closingTask, completion.Task)) _closingTask = null;
        }
    }

    internal void OnHostUnloaded()
    {
        _reopenAfterClose = false;
        ResetSession();
        Closed?.Invoke(this, new DrawerClosedEventArgs(DrawerCloseReason.HostUnloaded));
    }

    private void ResetSession()
    {
        _session++;
        _lifetime?.Cancel();
        _animation?.Stop();
        _lifetime?.Dispose();
        _lifetime = null;
        _closingArgs?.Abort();
        _closingArgs = null;
        _openingTask = null;
        _closingTask = null;
        _coordinator = null;
        if (_popup is not null) _popup.IsOpen = false;
        ResolvedHost = null;
        ResolvedParentDrawer = null;
        SetState(DrawerState.Closed);
        SetIsOpen(false);
    }

    private void SetState(DrawerState state)
    {
        SetValue(StateProperty, state);
        _coordinator?.UpdateInteraction();
    }

    private void SetIsOpen(bool value)
    {
        _updatingIsOpen = true;
        try { SetValue(IsOpenProperty, value); }
        finally { _updatingIsOpen = false; }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsOpen) RequestOpen();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _coordinator?.Unload(this);
    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs e)
    {
        if (_surface is not null) _surface.DataContext = DataContext;
    }

    private async void RequestOpen()
    {
        try
        {
            if (_closingTask is Task<bool> closing)
            {
                _reopenAfterClose = true;
                if (State == DrawerState.ClosePending && _closingArgs is not null) _closingArgs.Handled = true;
                await closing;
                if (!_reopenAfterClose) return;
                _reopenAfterClose = false;
            }
            await OpenAsync();
        }
        catch (Exception exception) { ReportFailure(exception); }
    }

    internal async void RequestClose(DrawerCloseReason reason)
    {
        try { await CloseAsync(reason); }
        catch (Exception exception) { ReportFailure(exception); }
    }

    internal void ReportFailure(Exception exception)
    {
        LastInteractionException = exception;
        InteractionFailed?.Invoke(this, new DrawerInteractionFailedEventArgs(this, exception));
    }

    private void OnSurfacePointerPressed(object sender, PointerRoutedEventArgs e) => _coordinator?.OnPointerPressed(sender, e);
    private void OnSurfaceKeyDown(object sender, KeyRoutedEventArgs e) => _coordinator?.OnKeyDown(sender, e);

    private void UpdatePopupTheme()
    {
        if (_surface is not null) _surface.RequestedTheme = ActualTheme;
    }

    private void UpdateModality()
    {
        if (_overlay is not null) _overlay.Visibility = Modal ? Visibility.Visible : Visibility.Collapsed;
    }

    internal void UpdatePopupBounds()
    {
        if (_popup is null || _surface is null || ResolvedHost is null || XamlRoot?.Content is not UIElement root) return;
        Rect bounds = DrawerGeometry.ClipToRoot(ResolvedHost.TransformToVisual(root).TransformBounds(
            new Rect(0, 0, ResolvedHost.ActualWidth, ResolvedHost.ActualHeight)), XamlRoot.Size);
        // Popup의 offset setter는 같은 값도 재배치를 요청합니다.
        // LayoutUpdated에서 매번 지정하면 레이아웃이 안정화되지 않으므로 변경 시에만 반영합니다.
        if (_popup.HorizontalOffset != bounds.X) _popup.HorizontalOffset = bounds.X;
        if (_popup.VerticalOffset != bounds.Y) _popup.VerticalOffset = bounds.Y;
        if (_surface.Width != bounds.Width) _surface.Width = bounds.Width;
        if (_surface.Height != bounds.Height) _surface.Height = bounds.Height;
        if (_surface.FlowDirection != FlowDirection) _surface.FlowDirection = FlowDirection;
    }

    internal void SetFocusTrap(bool enabled)
    {
        if (_panel is not null) _panel.TabFocusNavigation = enabled ? KeyboardNavigationMode.Cycle : KeyboardNavigationMode.Local;
        // 닫기 승인 후에는 폼만 막고, 오버레이는 애니메이션 종료까지 입력을 계속 막습니다.
        if (_panel is not null) _panel.IsHitTestVisible = State != DrawerState.Closing;
        if (_contentRoot is not null) _contentRoot.IsEnabled = State != DrawerState.Closing;
    }

    internal Border? Panel => _panel;
    internal DrawerSurfacePanel? Surface => _surface;
    internal void VerifyAccess()
    {
        if (!DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Drawer APIs require the owning UI thread.");
    }

    private Point GetClosedOffset()
    {
        // 첫 표시의 ActualWidth/ActualHeight는 아직 0일 수 있습니다.
        // 배치와 같은 계산을 사용하므로 첫 열기 및 닫힌 동안의 Host 크기 변경에도 대응합니다.
        Rect bounds = DrawerGeometry.PanelBounds(new Size(_surface!.Width, _surface.Height), Placement, PanelLength);
        return Placement switch
        {
            DrawerPlacement.Left => new Point(-bounds.Width, 0),
            DrawerPlacement.Right => new Point(bounds.Width, 0),
            DrawerPlacement.Top => new Point(0, -bounds.Height),
            DrawerPlacement.Bottom => new Point(0, bounds.Height),
            _ => default
        };
    }

    private async Task AnimateAsync(bool opening, CancellationToken token)
    {
        if (_panel is null || _overlay is null || _translation is null) return;
        token.ThrowIfCancellationRequested();
        Point closedOffset = GetClosedOffset();
        double x = closedOffset.X;
        double y = closedOffset.Y;
        _translation.X = opening ? x : 0;
        _translation.Y = opening ? y : 0;
        _overlay.Opacity = opening ? 0 : 1;
        if (AnimationDuration <= TimeSpan.Zero || !new UISettings().AnimationsEnabled)
        {
            _translation.X = opening ? 0 : x;
            _translation.Y = opening ? 0 : y;
            _overlay.Opacity = opening ? 1 : 0;
            return;
        }
        var storyboard = new Storyboard();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        storyboard.Completed += (_, _) => done.TrySetResult();
        void Add(DependencyObject target, string property, double from, double to)
        {
            var animation = new DoubleAnimation
            {
                From = from, To = to, Duration = new Duration(AnimationDuration),
                EasingFunction = new CubicEase { EasingMode = opening ? EasingMode.EaseOut : EasingMode.EaseIn }
            };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }
        Add(_translation, "X", opening ? x : 0, opening ? 0 : x);
        Add(_translation, "Y", opening ? y : 0, opening ? 0 : y);
        if (Modal) Add(_overlay, "Opacity", opening ? 0 : 1, opening ? 1 : 0);
        _animation = storyboard;
        try { storyboard.Begin(); await done.Task.WaitAsync(token); }
        finally { storyboard.Stop(); if (ReferenceEquals(_animation, storyboard)) _animation = null; }
        _translation.X = opening ? 0 : x;
        _translation.Y = opening ? 0 : y;
        _overlay.Opacity = opening ? 1 : 0;
    }
}
