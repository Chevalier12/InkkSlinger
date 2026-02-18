using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class Menu : ItemsControl
{
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(Menu),
            new FrameworkPropertyMetadata(new Color(22, 30, 44), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(Menu),
            new FrameworkPropertyMetadata(new Color(62, 87, 117), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(Menu),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(Menu),
            new FrameworkPropertyMetadata(
                new Thickness(4f, 2f, 4f, 2f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemSpacingProperty =
        DependencyProperty.Register(
            nameof(ItemSpacing),
            typeof(float),
            typeof(Menu),
            new FrameworkPropertyMetadata(
                2f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    private Panel? _hostPanel;
    private bool _isMenuMode;
    private bool _hostZRaised;
    private int _hostOriginalZIndex;
    private UIElement? _focusBeforeMenuMode;
    private UIElement? _pendingFocusRestore;
    private MenuItem? _activeTopLevelItem;

    public Menu()
    {
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

    public float ItemSpacing
    {
        get => GetValue<float>(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    internal bool IsMenuMode => _isMenuMode;

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is MenuItem;
    }

    protected override UIElement CreateContainerForItemOverride(object item)
    {
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
            menuItem.SetOwnerMenu(this);
        }
    }

    protected override void ClearContainerForItemOverride(UIElement element, object item)
    {
        if (element is MenuItem menuItem)
        {
            menuItem.SetOwnerMenu(null);
        }

        base.ClearContainerForItemOverride(element, item);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var padding = Padding;
        var border = BorderThickness * 2f;
        var innerWidth = MathF.Max(0f, availableSize.X - padding.Horizontal - border);
        var innerHeight = MathF.Max(0f, availableSize.Y - padding.Vertical - border);

        var totalWidth = 0f;
        var maxHeight = 0f;
        var first = true;
        foreach (var child in GetTopLevelItems())
        {
            child.Measure(new Vector2(innerWidth, innerHeight));
            if (!first)
            {
                totalWidth += ItemSpacing;
            }

            totalWidth += child.DesiredSize.X;
            maxHeight = MathF.Max(maxHeight, child.DesiredSize.Y);
            first = false;
        }

        return new Vector2(
            totalWidth + padding.Horizontal + border,
            maxHeight + padding.Vertical + border);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var padding = Padding;
        var border = BorderThickness;
        var x = LayoutSlot.X + border + padding.Left;
        var y = LayoutSlot.Y + border + padding.Top;
        var height = MathF.Max(0f, finalSize.Y - (border * 2f) - padding.Vertical);

        foreach (var child in GetTopLevelItems())
        {
            var childWidth = child.DesiredSize.X;
            child.Arrange(new LayoutRect(x, y, childWidth, height));
            x += childWidth + ItemSpacing;
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


    protected override void OnVisualParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnVisualParentChanged(oldParent, newParent);
        RefreshHostSubscriptions();
    }

    protected override void OnLogicalParentChanged(UIElement? oldParent, UIElement? newParent)
    {
        base.OnLogicalParentChanged(oldParent, newParent);
        RefreshHostSubscriptions();
    }

    internal IReadOnlyList<MenuItem> GetTopLevelItems()
    {
        var result = new List<MenuItem>();
        foreach (var child in ItemContainers)
        {
            if (child is MenuItem menuItem)
            {
                result.Add(menuItem);
            }
        }

        return result;
    }

    internal void EnterMenuMode(MenuItem source, UIElement? previousFocus = null)
    {
        var topLevel = source.GetTopLevelAncestor();
        if (!_isMenuMode)
        {
            _focusBeforeMenuMode = previousFocus;
        }

        _isMenuMode = true;
        EnsureOnTopOfHost();
        _activeTopLevelItem = topLevel;

        foreach (var item in GetTopLevelItems())
        {
            if (!ReferenceEquals(item, topLevel))
            {
                item.CloseSubmenuRecursive(clearHighlight: true);
            }
        }

        topLevel.IsHighlighted = true;
    }

    internal void ExitMenuMode()
    {
        _isMenuMode = false;
        _activeTopLevelItem = null;
        RestoreHostZIndex();
    }

    internal void CloseAllSubmenus(bool restoreFocus)
    {
        foreach (var item in GetTopLevelItems())
        {
            item.CloseSubmenuRecursive(clearHighlight: true);
        }

        _isMenuMode = false;
        _activeTopLevelItem = null;
        RestoreHostZIndex();

        if (restoreFocus)
        {
            _pendingFocusRestore = _focusBeforeMenuMode;
        }

        _focusBeforeMenuMode = null;
    }

    internal void CloseAllSubmenus()
    {
        CloseAllSubmenus(restoreFocus: false);
    }

    internal void NotifyLeafInvoked(MenuItem source)
    {
        CloseAllSubmenus(restoreFocus: true);
        if (source.GetTopLevelAncestor() is { } topLevel)
        {
            topLevel.IsHighlighted = false;
        }
    }

    internal bool MoveAcrossTopLevel(MenuItem current, int delta, bool openSubmenu)
    {
        var topLevelItems = GetTopLevelItems();
        if (topLevelItems.Count == 0)
        {
            return false;
        }

        var currentTopLevel = current.GetTopLevelAncestor();
        var currentIndex = -1;
        for (var i = 0; i < topLevelItems.Count; i++)
        {
            if (ReferenceEquals(topLevelItems[i], currentTopLevel))
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        var nextIndex = (currentIndex + delta) % topLevelItems.Count;
        if (nextIndex < 0)
        {
            nextIndex += topLevelItems.Count;
        }

        var target = topLevelItems[nextIndex];
        _activeTopLevelItem = target;
        target.IsHighlighted = true;

        foreach (var item in topLevelItems)
        {
            if (!ReferenceEquals(item, target))
            {
                item.CloseSubmenuRecursive(clearHighlight: true);
            }
        }

        if (openSubmenu)
        {
            target.OpenSubmenu(focusFirstItem: false);
            EnterMenuMode(target, _focusBeforeMenuMode);
        }

        return true;
    }

    internal void OpenFromHover(MenuItem topLevelItem)
    {
        if (!_isMenuMode)
        {
            return;
        }

        foreach (var item in GetTopLevelItems())
        {
            if (ReferenceEquals(item, topLevelItem))
            {
                item.IsHighlighted = true;
                _activeTopLevelItem = item;
                if (item.Items.Count > 0)
                {
                    item.IsSubmenuOpen = true;
                }

                continue;
            }

            item.CloseSubmenuRecursive(clearHighlight: true);
        }
    }

    private void RefreshHostSubscriptions()
    {
        if (_hostPanel != null)
        {
            RestoreHostZIndex();
            _hostPanel = null;
        }

        _hostPanel = FindHostPanel();
        if (_hostPanel == null)
        {
            return;
        }

    }



    private Panel? FindHostPanel()
    {
        for (var current = VisualParent ?? LogicalParent;
             current != null;
             current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Panel panel)
            {
                return panel;
            }
        }

        return null;
    }

    private bool IsSelfOrDescendant(UIElement element)
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

    private void EnsureOnTopOfHost()
    {
        if (_hostPanel == null || _hostZRaised)
        {
            return;
        }

        _hostOriginalZIndex = Panel.GetZIndex(this);
        var topZ = _hostOriginalZIndex;
        foreach (var child in _hostPanel.Children)
        {
            topZ = Math.Max(topZ, Panel.GetZIndex(child));
        }

        var raisedZ = topZ + 1;
        if (raisedZ != _hostOriginalZIndex)
        {
            Panel.SetZIndex(this, raisedZ);
            _hostZRaised = true;
        }
    }

    private void RestoreHostZIndex()
    {
        if (!_hostZRaised)
        {
            return;
        }

        Panel.SetZIndex(this, _hostOriginalZIndex);
        _hostZRaised = false;
    }

    private MenuItem? FindTopLevelAccessKeyTarget(char accessKey)
    {
        foreach (var item in GetTopLevelItems())
        {
            if (MenuAccessText.TryExtractAccessKey(item.Header, out var key) && key == accessKey)
            {
                return item;
            }
        }

        return null;
    }

    internal bool TryHandleAccessKeyFromInput(char accessKey, UIElement? previousFocus)
    {
        var target = FindTopLevelAccessKeyTarget(char.ToUpperInvariant(accessKey));
        if (target == null)
        {
            return false;
        }

        EnterMenuMode(target, previousFocus);
        target.OpenSubmenu(focusFirstItem: false);
        return true;
    }

    internal bool TryHandleKeyDownFromInput(Keys key, ModifierKeys modifiers)
    {
        if (!_isMenuMode)
        {
            return false;
        }

        _ = modifiers;
        var currentTop = GetHighlightedTopLevelItem();
        if (currentTop == null && _activeTopLevelItem != null)
        {
            currentTop = _activeTopLevelItem;
            currentTop.IsHighlighted = true;
        }

        currentTop ??= GetFirstTopLevelItem();
        if (currentTop == null)
        {
            return false;
        }

        var current = GetDeepestHighlightedItem(currentTop);
        switch (key)
        {
            case Keys.Escape:
                CloseAllSubmenus(restoreFocus: true);
                return true;
            case Keys.Left:
                if (!ReferenceEquals(current, currentTop))
                {
                    if (current.GetParentMenuItem() is { } firstLevelParent &&
                        ReferenceEquals(firstLevelParent, currentTop))
                    {
                        return MoveAcrossTopLevel(currentTop, -1, openSubmenu: true);
                    }

                    return TryMoveLeftWithinSubmenu(current);
                }

                return MoveAcrossTopLevel(currentTop, -1, openSubmenu: true);
            case Keys.Right:
                if (!ReferenceEquals(current, currentTop))
                {
                    if (current.HasChildItems)
                    {
                        current.OpenSubmenu(focusFirstItem: true);
                        return true;
                    }

                    if (current.GetParentMenuItem() is { } firstLevelParent &&
                        ReferenceEquals(firstLevelParent, currentTop))
                    {
                        return MoveAcrossTopLevel(currentTop, 1, openSubmenu: true);
                    }

                    return false;
                }

                return MoveAcrossTopLevel(currentTop, 1, openSubmenu: true);
            case Keys.Down:
                if (ReferenceEquals(current, currentTop))
                {
                    currentTop.OpenSubmenu(focusFirstItem: true);
                    return true;
                }

                return current.MoveSibling(1);
            case Keys.Up:
                if (ReferenceEquals(current, currentTop))
                {
                    currentTop.OpenSubmenu(focusFirstItem: false);
                    if (currentTop.GetLastChildMenuItem() is { } last)
                    {
                        last.IsHighlighted = true;
                        return true;
                    }

                    return false;
                }

                return current.MoveSibling(-1);
            case Keys.Enter:
            case Keys.Space:
                if (ReferenceEquals(current, currentTop))
                {
                    currentTop.OpenSubmenu(focusFirstItem: true);
                    return true;
                }

                if (current.HasChildItems)
                {
                    current.OpenSubmenu(focusFirstItem: true);
                    return true;
                }

                if (current.InvokeLeaf())
                {
                    NotifyLeafInvoked(current);
                    return true;
                }

                return true;
            default:
                return false;
        }
    }

    internal bool TryActivateMenuBarFromKeyboard(UIElement? previousFocus)
    {
        var first = GetFirstTopLevelItem();
        if (first == null)
        {
            return false;
        }

        EnterMenuMode(first, previousFocus);
        foreach (var item in GetTopLevelItems())
        {
            item.CloseSubmenuRecursive(clearHighlight: true);
        }

        first.IsHighlighted = true;
        _activeTopLevelItem = first;
        return true;
    }

    internal bool ContainsElement(UIElement element)
    {
        return IsSelfOrDescendant(element);
    }

    internal bool TryConsumePendingFocusRestore(out UIElement? element)
    {
        element = _pendingFocusRestore;
        _pendingFocusRestore = null;
        return element != null;
    }

    private MenuItem? GetHighlightedTopLevelItem()
    {
        var items = GetTopLevelItems();
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].IsHighlighted)
            {
                return items[i];
            }
        }

        return null;
    }

    private MenuItem? GetFirstTopLevelItem()
    {
        var items = GetTopLevelItems();
        return items.Count > 0 ? items[0] : null;
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

    private bool TryMoveLeftWithinSubmenu(MenuItem current)
    {
        var parent = current.GetParentMenuItem();
        if (parent == null)
        {
            return false;
        }

        current.CloseSubmenuRecursive(clearHighlight: true);
        parent.IsHighlighted = true;
        return true;
    }

}
