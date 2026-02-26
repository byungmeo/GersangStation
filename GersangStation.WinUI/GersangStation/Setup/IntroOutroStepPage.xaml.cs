using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;

namespace GersangStation.Setup;

public sealed partial class IntroOutroStepPage : Page, ISetupStepPage, IAutoAdvanceSetupStepPage
{
    private readonly TaskCompletionSource<bool> _completedTcs = new();
    private bool _started;

    public bool CanGoNext => false;

    public bool CanSkip => false;

    public event EventHandler? StateChanged;

    public IntroOutroStepPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string text && !string.IsNullOrWhiteSpace(text))
            MessageText.Text = text;
    }

    public bool OnNext() => false;

    public void OnSkip()
    {
    }

    public Task WaitForCompletionAsync() => _completedTcs.Task;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started)
            return;

        _started = true;

        await RunFadeAsync(from: 0, to: 1, durationMs: 500);
        await Task.Delay(1000);
        await RunFadeAsync(from: 1, to: 0, durationMs: 500);

        _completedTcs.TrySetResult(true);
    }

    private Task RunFadeAsync(double from, double to, int durationMs)
    {
        var tcs = new TaskCompletionSource<bool>();

        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(animation, MessageText);
        Storyboard.SetTargetProperty(animation, nameof(Opacity));

        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) => tcs.TrySetResult(true);
        storyboard.Begin();

        return tcs.Task;
    }
}
