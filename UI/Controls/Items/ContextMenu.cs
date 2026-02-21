using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class ContextMenu : ItemsControl
{
    internal readonly record struct ContextMenuHitTestStats(
        int NodesVisited,
        int OpenBranchesVisited,
        int RootItemsVisited,
        int RowChecks,
        int BoundsChecks,
        int MaxDepth,
        int UninitializedBoundsFallbackCount,
        double InternalElapsedMs);

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(ContextMenu), new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty StaysOpenProperty =
        DependencyProperty.Register(nameof(StaysOpen), typeof(bool), typeof(ContextMenu), new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty PlacementModeProperty =
        DependencyProperty.Register(nameof(PlacementMode), typeof(PopupPlacementMode), typeof(ContextMenu), new FrameworkPropertyMetadata(PopupPlacementMode.Absolute, FrameworkPropertyMetadataOptions.AffectsArrange));

    // Alias for WPF-style naming.
    public static readonly DependencyProperty PlacementProperty = PlacementModeProperty;

    public static readonly DependencyProperty PlacementTargetProperty =
        DependencyProperty.Register(nameof(PlacementTarget), typeof(UIElement), typeof(ContextMenu), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(nameof(HorizontalOffset), typeof(float), typeof(ContextMenu), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(nameof(VerticalOffset), typeof(float), typeof(ContextMenu), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty LeftProperty =
        DependencyProperty.Register(nameof(Left), typeof(float), typeof(ContextMenu), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty TopProperty =
        DependencyProperty.Register(nameof(Top), typeof(float), typeof(ContextMenu), new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Color), typeof(ContextMenu), new FrameworkPropertyMetadata(new Color(22, 30, 44), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(nameof(BorderBrush), typeof(Color), typeof(ContextMenu), new FrameworkPropertyMetadata(new Color(62, 87, 117), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ContextMenu),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(ContextMenu),
            new FrameworkPropertyMetadata(
                new Thickness(2f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ContextMenuProperty =
        DependencyProperty.RegisterAttached("ContextMenu", typeof(ContextMenu), typeof(ContextMenu), new FrameworkPropertyMetadata(null));

    private Panel? _host;
    private UIElement? _focusBeforeOpen;
    private UIElement? _pendingFocusRestore;

    public ContextMenu()
    {
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Width = float.NaN;
        Height = float.NaN;
    }

    public event EventHandler? Opened;

    public event EventHandler? Closed;

    public bool IsOpen
    {
        get => GetValue<bool>(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool StaysOpen
    {
        get => GetValue<bool>(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }

    public PopupPlacementMode PlacementMode
    {
        get => GetValue<PopupPlacementMode>(PlacementModeProperty);
        set => SetValue(PlacementModeProperty, value);
    }

    public PopupPlacementMode Placement
    {
        get => GetValue<PopupPlacementMode>(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public UIElement? PlacementTarget
    {
        get => GetValue<UIElement>(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    public float HorizontalOffset
    {
        get => GetValue<float>(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public float VerticalOffset
    {
        get => GetValue<float>(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    public float Left
    {
        get => GetValue<float>(LeftProperty);
        set => SetValue(LeftProperty, value);
    }

    public float Top
    {
        get => GetValue<float>(TopProperty);
        set => SetValue(TopProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public float BorderThickness
    {
        get => GetValue<float>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    internal bool IsMenuMode => IsOpen;

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is MenuItem || item is Separator;
    }

    protected override UIElement CreateContainerForItemOverride(object item)
    {
        if (item is Label label)
        {
            return new MenuItem
            {
                Header = label.Text
            };
        }

        return new MenuItem
        {
            Header = item?.ToString() ?? string.Empty
        };
    }

    protected override void PrepareContainerForItemOverride(UIElement element, object item, int index)
    {
        base.PrepareContainerForItemOverride(element, item, index);

        if (element is MenuItem menuItem)
        {
            menuItem.SetOwnerContextMenu(this);
        }
    }

    protected override void ClearContainerForItemOverride(UIElement element, object item)
    {
        if (element is MenuItem menuItem)
        {
            menuItem.SetOwnerContextMenu(null);
        }

        base.ClearContainerForItemOverride(element, item);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var padding = Padding;
        var border = BorderThickness * 2f;
        var innerWidth = MathF.Max(0f, availableSize.X - border - padding.Horizontal);
        var innerHeight = MathF.Max(0f, availableSize.Y - border - padding.Vertical);

        var maxItemWidth = 0f;
        var totalItemHeight = 0f;

        foreach (var element in ItemContainers)
        {
            if (element is not FrameworkElement child)
            {
                continue;
            }

            child.Measure(new Vector2(innerWidth, innerHeight));
            maxItemWidth = MathF.Max(maxItemWidth, child.DesiredSize.X);
            totalItemHeight += child.DesiredSize.Y;
        }

        return new Vector2(
            maxItemWidth + border + padding.Horizontal,
            totalItemHeight + border + padding.Vertical);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var border = BorderThickness;
        var padding = Padding;
        var x = LayoutSlot.X + border + padding.Left;
        var y = LayoutSlot.Y + border + padding.Top;
        var width = MathF.Max(0f, finalSize.X - (border * 2f) - padding.Horizontal);

        foreach (var element in ItemContainers)
        {
            if (element is not FrameworkElement child)
            {
                continue;
            }

            var height = child.DesiredSize.Y;
            child.Arrange(new LayoutRect(x, y, width, height));
            y += height;
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, Background, Opacity);
        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, LayoutSlot, BorderThickness, BorderBrush, Opacity);
        }
    }

    public void Open(Panel host)
    {
        _host = host;
        _focusBeforeOpen = FocusManager.GetFocusedElement();

        if (VisualParent == null)
        {
            host.AddChild(this);
        }

        var contentSize = MeasureContentSizeFromItems();
        var width = ResolveOpenSize(Width, contentSize.X, minSize: 120f, MathF.Max(1f, host.LayoutSlot.Width));
        var height = ResolveOpenSize(Height, contentSize.Y, minSize: 24f, MathF.Max(1f, host.LayoutSlot.Height));

        Width = width;
        Height = height;

        ApplyPlacement(width, height);
        PrimeLayoutForImmediateInteraction(width, height);

        if (!IsOpen)
        {
            IsOpen = true;
            Opened?.Invoke(this, EventArgs.Empty);
        }

        HighlightFirstRootItem();
    }

    private void PrimeLayoutForImmediateInteraction(float width, float height)
    {
        if (_host == null)
        {
            return;
        }

        var hostSize = new Vector2(
            MathF.Max(0f, _host.LayoutSlot.Width),
            MathF.Max(0f, _host.LayoutSlot.Height));
        Measure(hostSize);
        Arrange(new LayoutRect(Left, Top, width, height));
    }

    public void OpenAt(Panel host, float left, float top, UIElement? placementTarget = null)
    {
        PlacementMode = PopupPlacementMode.Absolute;
        PlacementTarget = placementTarget;
        Left = left;
        Top = top;
        Open(host);
    }

    internal void OpenAtPointer(Panel host, Vector2 pointerPosition, UIElement? placementTarget)
    {
        PlacementMode = PopupPlacementMode.Absolute;
        PlacementTarget = placementTarget;
        Left = pointerPosition.X;
        Top = pointerPosition.Y;
        Open(host);
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;

        foreach (var item in GetRootItems())
        {
            item.CloseSubmenuRecursive(clearHighlight: true);
        }

        if (_host != null && VisualParent != null)
        {
            _host.RemoveChild(this);
        }

        _host = null;
        _pendingFocusRestore = _focusBeforeOpen;
        _focusBeforeOpen = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public static void SetContextMenu(UIElement element, ContextMenu? value)
    {
        element.SetValue(ContextMenuProperty, value);
    }

    public static ContextMenu? GetContextMenu(UIElement element)
    {
        return element.GetValue<ContextMenu>(ContextMenuProperty);
    }

    internal bool ContainsElement(UIElement element)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    internal bool TryConsumePendingFocusRestore(out UIElement? element)
    {
        element = _pendingFocusRestore;
        _pendingFocusRestore = null;
        return element != null;
    }

    internal bool HandlePointerDownFromInput(MenuItem source)
    {
        return IsOpen;
    }

    internal bool HandlePointerUpFromInput(MenuItem source)
    {
        if (!IsOpen)
        {
            return false;
        }

        if (source.HasChildItems)
        {
            return true;
        }

        var invoked = source.InvokeLeaf();
        if (invoked && !StaysOpen)
        {
            Close();
        }

        return invoked;
    }

    internal void HandlePointerMoveFromInput(MenuItem source)
    {
        if (!IsOpen)
        {
            return;
        }

        if (source.GetParentMenuItem() == null)
        {
            if (ReferenceEquals(GetHighlightedRootItem(), source))
            {
                if (source.HasChildItems && !source.IsSubmenuOpen)
                {
                    source.OpenSubmenu(focusFirstItem: false);
                }

                return;
            }

            HighlightRoot(source);
            if (source.HasChildItems)
            {
                source.OpenSubmenu(focusFirstItem: false);
            }

            return;
        }

        if (source.GetParentMenuItem() is { } parent)
        {
            if (ReferenceEquals(parent.GetHighlightedChild(), source) &&
                (!source.HasChildItems || source.IsSubmenuOpen))
            {
                return;
            }

            parent.OpenChildFromHover(source);
        }
    }

    internal bool TryHandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        if (!IsOpen)
        {
            return false;
        }

        _ = modifiers;

        var root = GetHighlightedRootItem() ?? GetFirstRootItem();
        if (root == null)
        {
            return false;
        }

        root.IsHighlighted = true;
        var current = GetDeepestHighlightedItem(root);

        switch (key)
        {
            case Keys.Escape:
                Close();
                return true;
            case Keys.Down:
                if (ReferenceEquals(current, root) && !root.HasChildItems)
                {
                    return MoveRootSibling(root, 1);
                }

                if (ReferenceEquals(current, root))
                {
                    root.OpenSubmenu(focusFirstItem: true);
                    return true;
                }

                return current.MoveSibling(1);
            case Keys.Up:
                if (ReferenceEquals(current, root) && !root.HasChildItems)
                {
                    return MoveRootSibling(root, -1);
                }

                if (ReferenceEquals(current, root))
                {
                    root.OpenSubmenu(focusFirstItem: false);
                    if (root.GetLastChildMenuItem() is { } last)
                    {
                        last.IsHighlighted = true;
                        return true;
                    }

                    return false;
                }

                return current.MoveSibling(-1);
            case Keys.Right:
                if (current.HasChildItems)
                {
                    current.OpenSubmenu(focusFirstItem: true);
                    return true;
                }

                return false;
            case Keys.Left:
                if (current.GetParentMenuItem() is { } parent)
                {
                    current.CloseSubmenuRecursive(clearHighlight: true);
                    parent.IsSubmenuOpen = false;
                    parent.IsHighlighted = true;
                    return true;
                }

                Close();
                return true;
            case Keys.Enter:
            case Keys.Space:
                if (current.HasChildItems)
                {
                    current.OpenSubmenu(focusFirstItem: true);
                    return true;
                }

                if (current.InvokeLeaf())
                {
                    if (!StaysOpen)
                    {
                        Close();
                    }

                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    private static float ResolveOpenSize(float requested, float content, float minSize, float maxSize)
    {
        if (!float.IsFinite(requested) || requested <= 0f)
        {
            return MathF.Min(maxSize, MathF.Max(minSize, content));
        }

        return MathF.Min(maxSize, MathF.Max(1f, requested));
    }

    private void ApplyPlacement(float width, float height)
    {
        if (_host == null)
        {
            return;
        }

        var (x, y) = ResolvePlacement(width, height);

        var hostRect = _host.LayoutSlot;
        var maxX = hostRect.X + MathF.Max(0f, hostRect.Width - width);
        var maxY = hostRect.Y + MathF.Max(0f, hostRect.Height - height);
        x = Math.Clamp(x, hostRect.X, maxX);
        y = Math.Clamp(y, hostRect.Y, maxY);

        Left = x;
        Top = y;

        if (_host is Canvas)
        {
            Canvas.SetLeft(this, x);
            Canvas.SetTop(this, y);
            return;
        }

        Margin = new Thickness(x, y, 0f, 0f);
    }

    private (float X, float Y) ResolvePlacement(float width, float height)
    {
        var x = Left;
        var y = Top;

        switch (PlacementMode)
        {
            case PopupPlacementMode.Bottom:
                if (TryGetPlacementTargetRect(out var bottomRect))
                {
                    x = bottomRect.X;
                    y = bottomRect.Y + bottomRect.Height;
                }

                break;
            case PopupPlacementMode.Right:
                if (TryGetPlacementTargetRect(out var rightRect))
                {
                    x = rightRect.X + rightRect.Width;
                    y = rightRect.Y;
                }

                break;
            case PopupPlacementMode.Center:
                if (_host != null)
                {
                    x = _host.LayoutSlot.X + ((_host.LayoutSlot.Width - width) / 2f);
                    y = _host.LayoutSlot.Y + ((_host.LayoutSlot.Height - height) / 2f);
                }

                break;
            case PopupPlacementMode.Absolute:
            default:
                break;
        }

        x += HorizontalOffset;
        y += VerticalOffset;
        return (x, y);
    }

    private bool TryGetPlacementTargetRect(out LayoutRect rect)
    {
        if (PlacementTarget == null)
        {
            rect = default;
            return false;
        }

        var slot = PlacementTarget.LayoutSlot;
        if (slot.Width > 0f || slot.Height > 0f)
        {
            rect = slot;
            return true;
        }

        if (PlacementTarget is FrameworkElement element &&
            element.VisualParent is Canvas)
        {
            rect = new LayoutRect(
                Canvas.GetLeft(element),
                Canvas.GetTop(element),
                element.Width,
                element.Height);
            return true;
        }

        rect = default;
        return false;
    }

    private Vector2 MeasureContentSizeFromItems()
    {
        var maxWidth = 0f;
        var totalHeight = 0f;
        foreach (var container in ItemContainers)
        {
            if (container is not FrameworkElement element)
            {
                continue;
            }

            element.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
            maxWidth = MathF.Max(maxWidth, element.DesiredSize.X);
            totalHeight += element.DesiredSize.Y;
        }

        var border = BorderThickness * 2f;
        var padding = Padding;
        return new Vector2(
            maxWidth + border + padding.Horizontal,
            totalHeight + border + padding.Vertical);
    }

    private IReadOnlyList<MenuItem> GetRootItems()
    {
        var items = new List<MenuItem>();
        foreach (var container in ItemContainers)
        {
            if (container is MenuItem menuItem)
            {
                items.Add(menuItem);
            }
        }

        return items;
    }

    private void HighlightFirstRootItem()
    {
        if (GetFirstRootItem() is not { } first)
        {
            return;
        }

        HighlightRoot(first);
    }

    private MenuItem? GetFirstRootItem()
    {
        var roots = GetRootItems();
        return roots.Count > 0 ? roots[0] : null;
    }

    private MenuItem? GetHighlightedRootItem()
    {
        foreach (var root in GetRootItems())
        {
            if (root.IsHighlighted)
            {
                return root;
            }
        }

        return null;
    }

    private void HighlightRoot(MenuItem target)
    {
        if (ReferenceEquals(GetHighlightedRootItem(), target))
        {
            return;
        }

        foreach (var root in GetRootItems())
        {
            if (ReferenceEquals(root, target))
            {
                root.IsHighlighted = true;
                continue;
            }

            root.CloseSubmenuRecursive(clearHighlight: true);
        }
    }

    private bool MoveRootSibling(MenuItem current, int delta)
    {
        var roots = GetRootItems();
        if (roots.Count == 0)
        {
            return false;
        }

        var index = -1;
        for (var i = 0; i < roots.Count; i++)
        {
            if (ReferenceEquals(roots[i], current))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            index = 0;
        }

        var next = (index + delta) % roots.Count;
        if (next < 0)
        {
            next += roots.Count;
        }

        HighlightRoot(roots[next]);
        return true;
    }

    private static MenuItem GetDeepestHighlightedItem(MenuItem root)
    {
        var current = root;
        while (current.GetHighlightedChild() is { } child)
        {
            current = child;
        }

        return current;
    }

    internal bool TryHitTestMenuItem(Vector2 point, out MenuItem menuItem)
    {
        return TryHitTestMenuItem(point, out menuItem, out _, out _);
    }

    internal bool TryHitTestMenuItem(Vector2 point, out MenuItem menuItem, out int nodesVisited, out int openBranchCount)
    {
        var resolved = TryHitTestMenuItem(point, out menuItem, out var stats);
        nodesVisited = stats.NodesVisited;
        openBranchCount = stats.OpenBranchesVisited;
        return resolved;
    }

    internal bool TryHitTestMenuItem(Vector2 point, out MenuItem menuItem, out ContextMenuHitTestStats hitTestStats)
    {
        var hitTestStart = Stopwatch.GetTimestamp();
        var nodesVisited = 0;
        var openBranchCount = 0;
        var rootsVisited = 0;
        var rowChecks = 0;
        var boundsChecks = 0;
        var maxDepth = 0;
        var uninitializedBoundsFallbackCount = 0;
        foreach (var container in ItemContainers)
        {
            if (container is not MenuItem root)
            {
                continue;
            }

            rootsVisited++;
            if (TryHitTestMenuItemRecursive(
                    root,
                    point,
                    out menuItem,
                    ref nodesVisited,
                    ref openBranchCount,
                    depth: 1,
                    ref maxDepth,
                    ref rowChecks,
                    ref boundsChecks,
                    ref uninitializedBoundsFallbackCount))
            {
                hitTestStats = new ContextMenuHitTestStats(
                    NodesVisited: nodesVisited,
                    OpenBranchesVisited: openBranchCount,
                    RootItemsVisited: rootsVisited,
                    RowChecks: rowChecks,
                    BoundsChecks: boundsChecks,
                    MaxDepth: maxDepth,
                    UninitializedBoundsFallbackCount: uninitializedBoundsFallbackCount,
                    InternalElapsedMs: Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds);
                return true;
            }
        }

        hitTestStats = new ContextMenuHitTestStats(
            NodesVisited: nodesVisited,
            OpenBranchesVisited: openBranchCount,
            RootItemsVisited: rootsVisited,
            RowChecks: rowChecks,
            BoundsChecks: boundsChecks,
            MaxDepth: maxDepth,
            UninitializedBoundsFallbackCount: uninitializedBoundsFallbackCount,
            InternalElapsedMs: Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds);
        menuItem = null!;
        return false;
    }

    private static bool TryHitTestMenuItemRecursive(
        MenuItem candidate,
        Vector2 point,
        out MenuItem hit,
        ref int nodesVisited,
        ref int openBranchCount,
        int depth,
        ref int maxDepth,
        ref int rowChecks,
        ref int boundsChecks,
        ref int uninitializedBoundsFallbackCount)
    {
        nodesVisited++;
        if (depth > maxDepth)
        {
            maxDepth = depth;
        }

        if (candidate.IsSubmenuOpen)
        {
            openBranchCount++;
            MenuItem? rowMatch = null;
            var children = candidate.GetChildItemContainersForTraversal();
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is not MenuItem child)
                {
                    continue;
                }

                // If pointer is on a row, prefer that branch first.
                rowChecks++;
                if (child.HitTestRowFast(point))
                {
                    rowMatch = child;
                    break;
                }
            }

            if (rowMatch != null &&
                TryHitTestMenuItemRecursive(
                    rowMatch,
                    point,
                    out hit,
                    ref nodesVisited,
                    ref openBranchCount,
                    depth + 1,
                    ref maxDepth,
                    ref rowChecks,
                    ref boundsChecks,
                    ref uninitializedBoundsFallbackCount))
            {
                return true;
            }

            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is not MenuItem child || ReferenceEquals(child, rowMatch))
                {
                    continue;
                }

                // Always follow open branches. A deeper descendant submenu can extend
                // beyond this immediate child's submenu bounds.
                if (!child.IsSubmenuOpen)
                {
                    boundsChecks++;
                    if (!child.HitTestOpenSubmenuBoundsFast(point, out var usedUninitializedBoundsFallback))
                    {
                        continue;
                    }

                    if (usedUninitializedBoundsFallback)
                    {
                        uninitializedBoundsFallbackCount++;
                    }
                }

                if (TryHitTestMenuItemRecursive(
                        child,
                        point,
                        out hit,
                        ref nodesVisited,
                        ref openBranchCount,
                        depth + 1,
                        ref maxDepth,
                        ref rowChecks,
                        ref boundsChecks,
                        ref uninitializedBoundsFallbackCount))
                {
                    return true;
                }
            }
        }

        rowChecks++;
        if (candidate.HitTestRowFast(point))
        {
            hit = candidate;
            return true;
        }

        hit = null!;
        return false;
    }
}
