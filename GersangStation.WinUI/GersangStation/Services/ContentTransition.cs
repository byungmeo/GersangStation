using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;

namespace GersangStation.Services;

/// <summary>
/// 콘텐츠 교체를 명시적으로 애니메이션합니다. 숨김/제거에 연결하지 않아 부모 화면을 붙잡지 않습니다.
/// </summary>
internal sealed class ContentTransition
{
    private readonly FrameworkElement _target;
    private readonly TranslateTransform? _translation;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _requestVersion;
    private Storyboard? _animation;
    private TaskCompletionSource<bool>? _animationCompletion;

    public ContentTransition(FrameworkElement target, bool slide = false)
    {
        _target = target;
        if (slide)
            _target.RenderTransform = _translation = new TranslateTransform();
    }

    public async Task<bool> RunAsync(
        Func<Task<bool>> changeContent,
        Func<Task<bool>>? canLeave = null,
        Func<bool>? shouldAnimate = null)
    {
        long version = ++_requestVersion;
        await _gate.WaitAsync();
        bool wasHitTestVisible = _target.IsHitTestVisible;
        try
        {
            // 연속 요청 중 아직 시작하지 않은 이전 이동은 실행하지 않습니다.
            if (version != _requestVersion || (canLeave is not null && !await canLeave()))
                return false;
            if (version != _requestVersion)
                return false;

            bool animate = _target.IsLoaded && new UISettings().AnimationsEnabled
                && (shouldAnimate?.Invoke() ?? true);
            _target.IsHitTestVisible = false;
            if (animate)
                await AnimateAsync(opening: false);
            if (version != _requestVersion)
                return false;

            if (!await changeContent())
                return false;
            if (version != _requestVersion)
                return false;
            if (animate)
                await AnimateAsync(opening: true);
            return version == _requestVersion;
        }
        finally
        {
            _target.Opacity = 1;
            if (_translation is not null)
                _translation.Y = 0;
            _target.IsHitTestVisible = wasHitTestVisible;
            _gate.Release();
        }
    }

    public void Cancel()
    {
        ++_requestVersion;
        _animationCompletion?.TrySetResult(false);
        _animation?.Stop();
    }

    private async Task AnimateAsync(bool opening)
    {
        bool slide = _translation is not null;
        TimeSpan opacityDuration = TimeSpan.FromMilliseconds(slide
            ? (opening ? 200 : 100) : (opening ? 180 : 120));
        TimeSpan duration = slide ? TimeSpan.FromMilliseconds(opening ? 400 : 200) : opacityDuration;
        var storyboard = new Storyboard();
        void Add(DependencyObject target, string property, double from, double to, TimeSpan time)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(time),
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }

        _target.Opacity = opening ? 0 : 1;
        Add(_target, nameof(UIElement.Opacity), opening ? 0 : 1, opening ? 1 : 0, opacityDuration);
        if (_translation is not null)
        {
            _translation.Y = opening ? 24 : 0;
            Add(_translation, nameof(TranslateTransform.Y), opening ? 24 : 0, opening ? 0 : 24, duration);
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompleted(object? sender, object args) => completion.TrySetResult(true);
        storyboard.Completed += OnCompleted;
        _animation = storyboard;
        _animationCompletion = completion;
        try
        {
            storyboard.Begin();
            // 최소화/화면 해제 중 완료 이벤트가 지연되어도 다음 이동을 무한히 막지 않습니다.
            await Task.WhenAny(completion.Task, Task.Delay(duration + TimeSpan.FromMilliseconds(250)));
        }
        finally
        {
            storyboard.Completed -= OnCompleted;
            storyboard.Stop();
            _animation = null;
            _animationCompletion = null;
            _target.Opacity = opening ? 1 : 0;
        }
    }
}
