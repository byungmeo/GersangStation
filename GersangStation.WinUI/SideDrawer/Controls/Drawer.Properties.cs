using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;

namespace SideDrawer.Controls;

public sealed partial class Drawer
{
    public static readonly DependencyProperty HostProperty = DependencyProperty.Register(
        nameof(Host), typeof(FrameworkElement), typeof(Drawer), new PropertyMetadata(null, OnScopeChanged));
    /// <summary>표시·흐림·모달 입력 제한 영역. null이면 선언 위치에서 자동 탐색합니다.</summary>
    public FrameworkElement? Host
    {
        get => (FrameworkElement?)GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public static readonly DependencyProperty ParentDrawerProperty = DependencyProperty.Register(
        nameof(ParentDrawer), typeof(Drawer), typeof(Drawer), new PropertyMetadata(null, OnScopeChanged));
    public Drawer? ParentDrawer
    {
        get => (Drawer?)GetValue(ParentDrawerProperty);
        set => SetValue(ParentDrawerProperty, value);
    }

    public FrameworkElement? ResolvedHost { get; private set; }
    public Drawer? ResolvedParentDrawer { get; private set; }
    private bool _restoringScope;
    private static void OnScopeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var drawer = (Drawer)sender;
        if (drawer._coordinator is null || drawer._restoringScope) return;
        drawer._restoringScope = true;
        try { drawer.SetValue(e.Property, e.OldValue); }
        finally { drawer._restoringScope = false; }
        throw new InvalidOperationException("Change Host/ParentDrawer only while the Drawer is closed.");
    }

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(Drawer), new PropertyMetadata(false, OnIsOpenChanged));
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private static void OnIsOpenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var drawer = (Drawer)sender;
        if (drawer._updatingIsOpen) return;
        if ((bool)e.NewValue)
        {
            if (drawer.IsLoaded) drawer.RequestOpen();
        }
        else
        {
            drawer._reopenAfterClose = false;
            drawer.RequestClose(DrawerCloseReason.Programmatic);
        }
    }

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(DrawerState), typeof(Drawer), new PropertyMetadata(DrawerState.Closed));
    public DrawerState State => (DrawerState)GetValue(StateProperty);

    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
        nameof(Placement), typeof(DrawerPlacement), typeof(Drawer), new PropertyMetadata(DrawerPlacement.Right));
    public DrawerPlacement Placement
    {
        get => (DrawerPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public static readonly DependencyProperty PanelLengthProperty = DependencyProperty.Register(
        nameof(PanelLength), typeof(double), typeof(Drawer), new PropertyMetadata(400d));
    public double PanelLength
    {
        get => (double)GetValue(PanelLengthProperty);
        set
        {
            if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            SetValue(PanelLengthProperty, value);
        }
    }

    public static readonly DependencyProperty PanelMarginProperty = DependencyProperty.Register(
        nameof(PanelMargin), typeof(Thickness), typeof(Drawer), new PropertyMetadata(new Thickness(8)));
    /// <summary>표시 패널의 바깥 여백. 선언 위치의 Margin이나 모달 Host 범위에는 영향을 주지 않습니다.</summary>
    public Thickness PanelMargin
    {
        get => (Thickness)GetValue(PanelMarginProperty);
        set => SetValue(PanelMarginProperty, value);
    }

    public static readonly DependencyProperty ModalProperty = DependencyProperty.Register(
        nameof(Modal), typeof(bool), typeof(Drawer), new PropertyMetadata(true, OnModalChanged));
    public bool Modal
    {
        get => (bool)GetValue(ModalProperty);
        set => SetValue(ModalProperty, value);
    }

    private static void OnModalChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var drawer = (Drawer)sender;
        drawer.UpdateModality();
        drawer._coordinator?.UpdateInteraction();
    }

    public static readonly DependencyProperty CloseOnOutsideClickProperty = DependencyProperty.Register(
        nameof(CloseOnOutsideClick), typeof(bool), typeof(Drawer), new PropertyMetadata(true));
    public bool CloseOnOutsideClick
    {
        get => (bool)GetValue(CloseOnOutsideClickProperty);
        set => SetValue(CloseOnOutsideClickProperty, value);
    }

    public static readonly DependencyProperty CloseOnEscapeProperty = DependencyProperty.Register(
        nameof(CloseOnEscape), typeof(bool), typeof(Drawer), new PropertyMetadata(true));
    public bool CloseOnEscape
    {
        get => (bool)GetValue(CloseOnEscapeProperty);
        set => SetValue(CloseOnEscapeProperty, value);
    }

    public static readonly DependencyProperty AnimationDurationProperty = DependencyProperty.Register(
        nameof(AnimationDuration), typeof(TimeSpan), typeof(Drawer), new PropertyMetadata(TimeSpan.FromMilliseconds(240)));
    public TimeSpan AnimationDuration
    {
        get => (TimeSpan)GetValue(AnimationDurationProperty);
        set
        {
            if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value));
            SetValue(AnimationDurationProperty, value);
        }
    }

    public static readonly DependencyProperty OverlayBrushProperty = DependencyProperty.Register(
        nameof(OverlayBrush), typeof(Brush), typeof(Drawer), new PropertyMetadata(null));
    public Brush? OverlayBrush
    {
        get => (Brush?)GetValue(OverlayBrushProperty);
        set => SetValue(OverlayBrushProperty, value);
    }

    /// <summary>사용 앱의 독자적인 명령에서도 모달 입력 범위를 조회할 수 있습니다.</summary>
    public static bool IsInputBlockedFor(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!element.DispatcherQueue.HasThreadAccess) throw new InvalidOperationException("Query Drawer input state on the UI thread.");
        return element.XamlRoot is not null && DrawerCoordinator.For(element.XamlRoot).IsInputBlocked(element);
    }
}
