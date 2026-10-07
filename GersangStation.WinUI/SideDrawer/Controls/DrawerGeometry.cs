using System;
using Windows.Foundation;

namespace SideDrawer.Controls;

internal static class DrawerGeometry
{
    internal static Rect PanelBounds(Size surfaceSize, DrawerPlacement placement, double panelLength)
    {
        double length = double.IsFinite(panelLength) && panelLength > 0 ? panelLength : 400;
        bool side = placement is DrawerPlacement.Left or DrawerPlacement.Right;
        double width = side ? Math.Min(length, surfaceSize.Width) : surfaceSize.Width;
        double height = side ? surfaceSize.Height : Math.Min(length, surfaceSize.Height);
        double x = placement == DrawerPlacement.Right ? surfaceSize.Width - width : 0;
        double y = placement == DrawerPlacement.Bottom ? surfaceSize.Height - height : 0;
        return new Rect(x, y, width, height);
    }

    internal static Rect ClipToRoot(Rect bounds, Size rootSize)
    {
        double left = Math.Clamp(bounds.X, 0, rootSize.Width);
        double top = Math.Clamp(bounds.Y, 0, rootSize.Height);
        double right = Math.Clamp(bounds.X + bounds.Width, left, rootSize.Width);
        double bottom = Math.Clamp(bounds.Y + bounds.Height, top, rootSize.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    internal static bool Contains(Rect bounds, Point point)
        => point.X >= bounds.X && point.Y >= bounds.Y
            && point.X < bounds.X + bounds.Width && point.Y < bounds.Y + bounds.Height;
}
