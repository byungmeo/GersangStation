using System;

namespace SideDrawer.Controls;

public enum DrawerPlacement { Left, Right, Top, Bottom }
public enum DrawerState { Closed, Opening, Open, ClosePending, Closing }
public enum DrawerCloseReason { Programmatic, OutsidePointer, Escape, HostUnloaded }

public sealed class DrawerClosedEventArgs(DrawerCloseReason reason) : EventArgs
{
    public DrawerCloseReason Reason { get; } = reason;
}

public sealed class DrawerInteractionFailedEventArgs(Drawer drawer, Exception exception) : EventArgs
{
    public Drawer Drawer { get; } = drawer;
    public Exception Exception { get; } = exception;
}
