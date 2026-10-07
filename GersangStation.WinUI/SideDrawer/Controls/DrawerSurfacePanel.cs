using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;

namespace SideDrawer.Controls;

/// <summary>전체 배경과 방향별 패널을 배치하고, 애니메이션을 호스트 경계 안에 클리핑합니다.</summary>
public sealed partial class DrawerSurfacePanel : Panel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new DrawerSurfaceAutomationPeer(this);
    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
        nameof(Placement), typeof(DrawerPlacement), typeof(DrawerSurfacePanel), new PropertyMetadata(DrawerPlacement.Right, OnLayoutChanged));
    public DrawerPlacement Placement
    {
        get => (DrawerPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public static readonly DependencyProperty PanelLengthProperty = DependencyProperty.Register(
        nameof(PanelLength), typeof(double), typeof(DrawerSurfacePanel), new PropertyMetadata(400d, OnLayoutChanged));
    public double PanelLength
    {
        get => (double)GetValue(PanelLengthProperty);
        set => SetValue(PanelLengthProperty, value);
    }

    private static void OnLayoutChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        => ((DrawerSurfacePanel)sender).InvalidateMeasure();

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count != 2) return default;
        bool side = Placement is DrawerPlacement.Left or DrawerPlacement.Right;
        double length = double.IsFinite(PanelLength) && PanelLength > 0 ? PanelLength : 400;
        Children[0].Measure(availableSize);
        Children[1].Measure(new Size(side ? Math.Min(length, availableSize.Width) : availableSize.Width,
            side ? availableSize.Height : Math.Min(length, availableSize.Height)));
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : Children[1].DesiredSize.Width,
            double.IsFinite(availableSize.Height) ? availableSize.Height : Children[1].DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // 같은 크기로 반복 배치할 때는 Clip 객체나 속성을 다시 지정하지 않습니다.
        var clipRect = new Rect(0, 0, finalSize.Width, finalSize.Height);
        if (Clip is not RectangleGeometry clip)
            Clip = new RectangleGeometry { Rect = clipRect };
        else if (clip.Rect != clipRect)
            clip.Rect = clipRect;
        if (Children.Count != 2) return finalSize;
        Children[0].Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        Children[1].Arrange(DrawerGeometry.PanelBounds(finalSize, Placement, PanelLength));
        return finalSize;
    }
}

internal sealed class DrawerSurfaceAutomationPeer(DrawerSurfacePanel owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => nameof(Drawer);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
}
