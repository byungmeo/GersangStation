using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GersangStation.Controls;

public sealed partial class HorizontalScrollContainer : UserControl
{
    public HorizontalScrollContainer()
    {
        InitializeComponent();
    }

    public object Source
    {
        get => (object)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(object), typeof(HorizontalScrollContainer), new PropertyMetadata(null));

    private void Scroller_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
    {
        ScrollBackBtn.Visibility = e.FinalView.HorizontalOffset < 1 ? Visibility.Collapsed : Visibility.Visible;
        ScrollForwardBtn.Visibility = e.FinalView.HorizontalOffset > scroller.ScrollableWidth - 1
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ScrollBackBtn_Click(object sender, RoutedEventArgs e)
    {
        scroller.ChangeView(scroller.HorizontalOffset - scroller.ViewportWidth, null, null);
        ScrollForwardBtn.Focus(FocusState.Programmatic);
    }

    private void ScrollForwardBtn_Click(object sender, RoutedEventArgs e)
    {
        scroller.ChangeView(scroller.HorizontalOffset + scroller.ViewportWidth, null, null);
        ScrollBackBtn.Focus(FocusState.Programmatic);
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateScrollButtonsVisibility();

    private void UpdateScrollButtonsVisibility()
        => ScrollForwardBtn.Visibility = scroller.ScrollableWidth > 0 ? Visibility.Visible : Visibility.Collapsed;
}
