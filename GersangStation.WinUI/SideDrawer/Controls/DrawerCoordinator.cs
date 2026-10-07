using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.Foundation;
using Windows.System;

namespace SideDrawer.Controls;

/// <summary>사용 측의 래퍼 없이, XamlRoot별 팝업 스택·Host 수명·입력·포커스를 관리합니다.</summary>
internal sealed class DrawerCoordinator
{
    private sealed record Entry(Drawer Drawer, FrameworkElement Host, WeakReference<UIElement>? ReturnFocus);
    private static readonly ConditionalWeakTable<XamlRoot, DrawerCoordinator> Coordinators = new();
    private readonly XamlRoot _xamlRoot;
    private readonly List<Entry> _entries = [];
    private UIElement? _root;
    private PointerRoutedEventArgs? _lastPointerEvent;

    private DrawerCoordinator(XamlRoot root) => _xamlRoot = root;
    internal static DrawerCoordinator For(XamlRoot root) => Coordinators.GetValue(root, static key => new(key));

    private static DependencyObject? ParentOf(DependencyObject element)
        => VisualTreeHelper.GetParent(element) ?? (element as FrameworkElement)?.Parent;

    internal static bool IsDescendant(DependencyObject? element, DependencyObject ancestor)
    {
        for (DependencyObject? current = element; current is not null; current = ParentOf(current))
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }

    internal static FrameworkElement? FindContainer(Drawer drawer)
    {
        for (DependencyObject? current = ParentOf(drawer); current is not null; current = ParentOf(current))
            if (current is not Drawer && current is Panel or Border or ContentControl)
                return (FrameworkElement)current;
        return null;
    }

    internal Drawer? ResolveParent(Drawer drawer)
    {
        Drawer? parent = drawer.ParentDrawer;
        if (parent is null)
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (_entries[i].Drawer.Surface is UIElement surface && IsDescendant(drawer, surface))
                { parent = _entries[i].Drawer; break; }
        if (parent is not null && (!_entries.Any(entry => entry.Drawer == parent) || !CanClose(parent)))
            throw new InvalidOperationException("ParentDrawer must be an active Drawer without another open child in the same XamlRoot.");
        if (parent?.State == DrawerState.Closing)
            throw new InvalidOperationException("Wait for the parent to close before opening another Drawer.");
        return parent;
    }

    internal void Mount(Drawer drawer)
    {
        FrameworkElement host = drawer.ResolvedHost!;
        if (host is Drawer || !host.IsLoaded || host.XamlRoot != _xamlRoot || drawer.XamlRoot != _xamlRoot
            || host.DispatcherQueue != drawer.DispatcherQueue)
            throw new InvalidOperationException("Host must be a loaded container in the Drawer's UI thread and XamlRoot.");
        if (drawer.ResolvedParentDrawer is null && IsInputBlocked(drawer))
            throw new InvalidOperationException("A modal Drawer already blocks this declaration. Open it as a nested Drawer using ParentDrawer.");
        var focus = FocusManager.GetFocusedElement(_xamlRoot) as UIElement;
        _entries.Add(new Entry(drawer, host, focus is null ? null : new WeakReference<UIElement>(focus)));
        host.Unloaded += OnHostUnloaded;
        if (_entries.Count == 1) Attach();
    }

    private void Attach()
    {
        _root = _xamlRoot.Content as UIElement
            ?? throw new InvalidOperationException("XamlRoot must have visual content.");
        _root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        _root.KeyDown += OnKeyDown;
        _root.PreviewKeyDown += OnPreviewKeyDown;
        if (_root is FrameworkElement element) element.LayoutUpdated += OnLayoutUpdated;
        FocusManager.GettingFocus += OnGettingFocus;
    }

    private void Detach()
    {
        if (_root is null) return;
        _root.RemoveHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed));
        _root.KeyDown -= OnKeyDown;
        _root.PreviewKeyDown -= OnPreviewKeyDown;
        if (_root is FrameworkElement element) element.LayoutUpdated -= OnLayoutUpdated;
        FocusManager.GettingFocus -= OnGettingFocus;
        _root = null;
        _lastPointerEvent = null;
    }

    internal bool CanClose(Drawer drawer)
        => !_entries.Any(entry => IsChildOf(entry.Drawer, drawer));

    private static bool IsChildOf(Drawer child, Drawer parent)
    {
        for (Drawer? current = child.ResolvedParentDrawer; current is not null; current = current.ResolvedParentDrawer)
            if (ReferenceEquals(current, parent)) return true;
        return false;
    }

    internal void Unmount(Drawer drawer, bool restoreFocus)
    {
        int index = _entries.FindIndex(entry => entry.Drawer == drawer);
        if (index < 0) return;
        Entry entry = _entries[index];
        _entries.RemoveAt(index);
        entry.Host.Unloaded -= OnHostUnloaded;
        UpdateInteraction();
        if (_entries.Count == 0) Detach();
        if (!restoreFocus) return;
        if (entry.ReturnFocus?.TryGetTarget(out UIElement? target) == true
            && target.XamlRoot == _xamlRoot && !IsInputBlocked(target) && target.Focus(FocusState.Programmatic)) return;
        if (_entries.Count > 0) FocusDrawer(_entries[^1].Drawer);
    }

    internal void Unload(Drawer drawer)
    {
        // 폼의 Loaded/Unloaded, Host 삭제, 창 종료는 닫기 취소로 되돌릴 수 없습니다.
        Entry[] affected = _entries.Where(entry => entry.Drawer == drawer || IsChildOf(entry.Drawer, drawer)).Reverse().ToArray();
        foreach (Entry entry in affected)
        {
            _entries.Remove(entry);
            entry.Host.Unloaded -= OnHostUnloaded;
        }
        UpdateInteraction();
        if (_entries.Count == 0) Detach();
        var failures = new List<(Drawer Drawer, Exception Error)>();
        foreach (Entry entry in affected)
        {
            try { entry.Drawer.OnHostUnloaded(); }
            catch (Exception exception) { failures.Add((entry.Drawer, exception)); }
        }
        foreach (var failure in failures) failure.Drawer.ReportFailure(failure.Error);
    }

    private void OnHostUnloaded(object sender, RoutedEventArgs e)
    {
        foreach (Entry entry in _entries.Where(entry => ReferenceEquals(entry.Host, sender)).ToArray())
            Unload(entry.Drawer);
    }

    private void OnLayoutUpdated(object? sender, object e)
    {
        foreach (Entry entry in _entries.ToArray())
        {
            if (!entry.Host.IsLoaded || entry.Host.XamlRoot != _xamlRoot) { Unload(entry.Drawer); continue; }
            try { entry.Drawer.UpdatePopupBounds(); }
            catch (Exception exception) { Unload(entry.Drawer); entry.Drawer.ReportFailure(exception); }
        }
    }

    internal void UpdateInteraction()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            Drawer drawer = _entries[i].Drawer;
            // 작은 Host를 모달로 열어도 Host 밖의 포커스는 막지 않습니다.
            drawer.SetFocusTrap(drawer.Modal && ReferenceEquals(drawer.ResolvedHost, _xamlRoot.Content)
                && i == _entries.Count - 1);
        }
        if (FocusManager.GetFocusedElement(_xamlRoot) is UIElement focused)
        {
            int barrier = FindBlockingEntry(focused);
            if (barrier < 0) return;
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (i == barrier || IsChildOf(_entries[i].Drawer, _entries[barrier].Drawer))
                { FocusDrawer(_entries[i].Drawer); break; }
        }
    }

    internal void FocusDrawer(Drawer drawer)
    {
        if (drawer.Panel is not Border panel || drawer.State == DrawerState.Closing) return;
        if (panel.Child is not null && FocusManager.FindFirstFocusableElement(panel.Child) is UIElement target
            && !IsInputBlocked(target) && target.Focus(FocusState.Programmatic)) return;
        if (!IsInputBlocked(panel)) panel.Focus(FocusState.Programmatic);
    }

    internal bool IsInputBlocked(DependencyObject element) => FindBlockingEntry(element) >= 0;

    private int FindBlockingEntry(DependencyObject element)
    {
        int owner = FindSurfaceOwner(element);
        for (int i = _entries.Count - 1; i > owner; i--)
        {
            Entry entry = _entries[i];
            if (!entry.Drawer.Modal) continue;
            if (IsDescendant(element, entry.Host)) return i;
            // Popup으로 표시되는 부모 Drawer도 같은 Host의 모달 자식에 의해 보호됩니다.
            if (owner >= 0 && (IsDescendant(_entries[owner].Host, entry.Host)
                || element is FrameworkElement target && PointInHost(entry.Host, CenterInRoot(target)))) return i;
        }
        return -1;
    }

    private int FindSurfaceOwner(DependencyObject element)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
            if (_entries[i].Drawer.Surface is UIElement surface && IsDescendant(element, surface)) return i;
        return -1;
    }

    private Point CenterInRoot(FrameworkElement element)
        => element.TransformToVisual(_xamlRoot.Content).TransformPoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2));

    private bool PointInHost(FrameworkElement host, Point point)
        => DrawerGeometry.Contains(host.TransformToVisual(_xamlRoot.Content).TransformBounds(
            new Rect(0, 0, host.ActualWidth, host.ActualHeight)), point);

    private void OnGettingFocus(object? sender, GettingFocusEventArgs e)
    {
        if (e.NewFocusedElement is not UIElement target || target.XamlRoot != _xamlRoot) return;
        int barrier = FindBlockingEntry(target);
        if (barrier < 0) return;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Drawer drawer = _entries[i].Drawer;
            if (i != barrier && !IsChildOf(drawer, _entries[barrier].Drawer)) continue;
            if (drawer.Panel is not Border panel || drawer.State == DrawerState.Closing) continue;
            DependencyObject next = panel.Child is null ? panel : FocusManager.FindFirstFocusableElement(panel.Child) ?? panel;
            if (!IsInputBlocked(next) && e.TrySetNewFocusedElement(next)) { e.Handled = true; return; }
        }
        e.TryCancel();
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInputBlocked(source)) e.Handled = true;
    }

    internal void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled || e.Key != VirtualKey.Escape || _entries.Count == 0) return;
        if (e.OriginalSource is not DependencyObject source) return;
        int owner = FindSurfaceOwner(source);
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Entry entry = _entries[i];
            bool inScope = i == owner || IsDescendant(source, entry.Host)
                || owner >= 0 && i > owner && (IsDescendant(_entries[owner].Host, entry.Host)
                    || source is FrameworkElement target && PointInHost(entry.Host, CenterInRoot(target)));
            if (!inScope) continue;
            if (!entry.Drawer.CloseOnEscape) return;
            e.Handled = true;
            entry.Drawer.RequestClose(DrawerCloseReason.Escape);
            return;
        }
    }

    internal void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_root is null || ReferenceEquals(_lastPointerEvent, e)) return;
        _lastPointerEvent = e;
        Point point = e.GetCurrentPoint(_root).Position;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            Drawer drawer = _entries[i].Drawer;
            if (!PointInHost(_entries[i].Host, point) || drawer.Panel is not Border panel) continue;
            Point panelPoint = e.GetCurrentPoint(panel).Position;
            if (DrawerGeometry.Contains(new Rect(0, 0, panel.ActualWidth, panel.ActualHeight), panelPoint)) return;
            if (drawer.Modal) e.Handled = true;
            if (drawer.CloseOnOutsideClick) drawer.RequestClose(DrawerCloseReason.OutsidePointer);
            return;
        }
    }
}
