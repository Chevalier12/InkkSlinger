using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class MenuItem : ItemsControl
{
    public static readonly RoutedEvent ClickEvent =
        new(nameof(Click), RoutingStrategy.Bubble);

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
            nameof(Header),
            typeof(string),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty InputGestureTextProperty =
        DependencyProperty.Register(
            nameof(InputGestureText),
            typeof(string),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(new Color(0, 0, 0, 0), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightBackgroundProperty =
        DependencyProperty.Register(
            nameof(HighlightBackground),
            typeof(Color),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(new Color(51, 84, 124), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OpenBackgroundProperty =
        DependencyProperty.Register(
            nameof(OpenBackground),
            typeof(Color),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(new Color(44, 70, 103), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(
                new Thickness(10f, 6f, 10f, 6f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty SubmenuBackgroundProperty =
        DependencyProperty.Register(
            nameof(SubmenuBackground),
            typeof(Color),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(new Color(25, 34, 48), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SubmenuBorderBrushProperty =
        DependencyProperty.Register(
            nameof(SubmenuBorderBrush),
            typeof(Color),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(new Color(62, 87, 117), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SubmenuBorderThicknessProperty =
        DependencyProperty.Register(
            nameof(SubmenuBorderThickness),
            typeof(float),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty IsSubmenuOpenProperty =
        DependencyProperty.Register(
            nameof(IsSubmenuOpen),
            typeof(bool),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is MenuItem menuItem)
                    {
                        menuItem.OnIsSubmenuOpenChanged(args.NewValue is bool value && value);
                    }
                }));

    public static readonly DependencyProperty IsHighlightedProperty =
        DependencyProperty.Register(
            nameof(IsHighlighted),
            typeof(bool),
            typeof(MenuItem),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private float _submenuWidth;
    private float _submenuContentHeight;
    private float _submenuVisibleHeight;
    private LayoutRect _submenuBounds;
    private float _submenuScrollOffset;
    private bool _submenuOpensLeft;
    private bool? _submenuDirectionBiasLeft;

    public MenuItem()
    {
    }

    public event EventHandler<RoutedSimpleEventArgs> Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    public string Header
    {
        get => GetValue<string>(HeaderProperty) ?? string.Empty;
        set => SetValue(HeaderProperty, value);
    }

    public string InputGestureText
    {
        get => GetValue<string>(InputGestureTextProperty) ?? string.Empty;
        set => SetValue(InputGestureTextProperty, value);
    }

    public SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    public Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color HighlightBackground
    {
        get => GetValue<Color>(HighlightBackgroundProperty);
        set => SetValue(HighlightBackgroundProperty, value);
    }

    public Color OpenBackground
    {
        get => GetValue<Color>(OpenBackgroundProperty);
        set => SetValue(OpenBackgroundProperty, value);
    }

    public Thickness Padding
    {
        get => GetValue<Thickness>(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public Color SubmenuBackground
    {
        get => GetValue<Color>(SubmenuBackgroundProperty);
        set => SetValue(SubmenuBackgroundProperty, value);
    }

    public Color SubmenuBorderBrush
    {
        get => GetValue<Color>(SubmenuBorderBrushProperty);
        set => SetValue(SubmenuBorderBrushProperty, value);
    }

    public float SubmenuBorderThickness
    {
        get => GetValue<float>(SubmenuBorderThicknessProperty);
        set => SetValue(SubmenuBorderThicknessProperty, value);
    }

    public bool IsSubmenuOpen
    {
        get => GetValue<bool>(IsSubmenuOpenProperty);
        set => SetValue(IsSubmenuOpenProperty, value);
    }

    public bool IsHighlighted
    {
        get => GetValue<bool>(IsHighlightedProperty);
        internal set => SetValue(IsHighlightedProperty, value);
    }

    internal Menu? OwnerMenu { get; private set; }

    internal ContextMenu? OwnerContextMenu { get; private set; }

    internal bool HasChildItems => Items.Count > 0;

    protected override bool IncludeGeneratedChildrenInVisualTree => IsSubmenuOpen;

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
            menuItem.SetOwnerMenu(OwnerMenu);
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
        var headerTextWidth = MeasureTextWidth(GetDisplayHeaderText());
        var gestureText = GetEffectiveInputGestureText();
        var gestureTextWidth = string.IsNullOrWhiteSpace(gestureText) ? 0f : MeasureTextWidth(gestureText) + 14f;
        var glyphWidth = HasChildItems ? 12f : 0f;

        var rowWidth = MathF.Max(20f, headerTextWidth + gestureTextWidth + glyphWidth + padding.Horizontal);
        var rowHeight = MathF.Max(20f, GetLineHeight() + padding.Vertical);

        _submenuWidth = 0f;
        _submenuContentHeight = 0f;
        _submenuVisibleHeight = 0f;

        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is not MenuItem child)
            {
                continue;
            }

            child.Measure(availableSize);
            _submenuWidth = MathF.Max(_submenuWidth, child.DesiredSize.X);
            _submenuContentHeight += child.DesiredSize.Y;
        }

        _submenuWidth = MathF.Max(_submenuWidth, rowWidth);
        return new Vector2(rowWidth, rowHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (!IsSubmenuOpen || !HasChildItems)
        {
            _submenuBounds = default;
            _submenuVisibleHeight = 0f;
            return finalSize;
        }

        var workArea = ResolveWorkAreaBounds();
        var isTopLevelItem = IsTopLevelItem();
        var preferredRightX = isTopLevelItem
            ? LayoutSlot.X
            : LayoutSlot.X + LayoutSlot.Width;
        var preferredLeftX = LayoutSlot.X - _submenuWidth;
        var preferredY = isTopLevelItem
            ? LayoutSlot.Y + LayoutSlot.Height
            : LayoutSlot.Y;

        var parentItem = GetParentMenuItem();
        var inheritedDirectionLeft = !isTopLevelItem &&
                                     ((parentItem?._submenuDirectionBiasLeft).GetValueOrDefault(parentItem?._submenuOpensLeft == true));
        var preferLeftFromParentDirection = inheritedDirectionLeft;
        _submenuOpensLeft = preferLeftFromParentDirection;
        var primaryX = preferLeftFromParentDirection ? preferredLeftX : preferredRightX;
        var secondaryX = preferLeftFromParentDirection ? preferredRightX : preferredLeftX;
        var submenuX = primaryX;

        if (!isTopLevelItem)
        {
            var primaryOverflow = ComputeHorizontalOverflow(primaryX, _submenuWidth, workArea);
            var secondaryOverflow = ComputeHorizontalOverflow(secondaryX, _submenuWidth, workArea);
            if (primaryOverflow > 0f && secondaryOverflow <= primaryOverflow)
            {
                submenuX = secondaryX;
                _submenuOpensLeft = !preferLeftFromParentDirection;
            }

            var rightSpace = MathF.Max(0f, (workArea.X + workArea.Width) - (LayoutSlot.X + LayoutSlot.Width));
            var leftSpace = MathF.Max(0f, LayoutSlot.X - workArea.X);
            if (!_submenuOpensLeft &&
                rightSpace + 0.5f < _submenuWidth &&
                leftSpace > rightSpace)
            {
                submenuX = preferredLeftX;
                _submenuOpensLeft = true;
            }
            else if (_submenuOpensLeft &&
                     leftSpace + 0.5f < _submenuWidth &&
                     rightSpace > leftSpace)
            {
                submenuX = preferredRightX;
                _submenuOpensLeft = false;
            }
        }

        var overflowAdjustedX = false;
        if (submenuX + _submenuWidth > workArea.X + workArea.Width)
        {
            submenuX = MathF.Max(workArea.X, (workArea.X + workArea.Width) - _submenuWidth);
            overflowAdjustedX = true;
        }
        else if (submenuX < workArea.X)
        {
            submenuX = workArea.X;
            overflowAdjustedX = true;
        }

        _submenuVisibleHeight = MathF.Min(_submenuContentHeight, workArea.Height);
        var maxSubmenuY = workArea.Y + MathF.Max(0f, workArea.Height - _submenuVisibleHeight);
        var submenuY = Math.Clamp(preferredY, workArea.Y, maxSubmenuY);
        var overflowAdjustedY = MathF.Abs(submenuY - preferredY) > 0.01f;

        var maxX = workArea.X + MathF.Max(0f, workArea.Width - _submenuWidth);
        var overflowAdjusted = overflowAdjustedX || overflowAdjustedY;
        var obstaclesForInitialPlacement = GatherOpenAncestorSubmenuBounds();
        var overlapsExistingOpenPanels = HasPanelOverlap(
            new LayoutRect(submenuX, submenuY, _submenuWidth, _submenuVisibleHeight),
            obstaclesForInitialPlacement);
        if (!isTopLevelItem &&
            (overflowAdjusted || overlapsExistingOpenPanels) &&
            parentItem is { } parentMenuItem)
        {
            // Overflow/collision rule:
            // 1) Keep a sticky branch direction, with hard edge-based flips.
            // 2) Start from one row below the expanding item.
            // 3) If still overlapping ancestor open panels, step down by row height.
            var branchPrefersLeft = _submenuDirectionBiasLeft ?? inheritedDirectionLeft;
            var rightSpace = MathF.Max(0f, (workArea.X + workArea.Width) - (LayoutSlot.X + LayoutSlot.Width));
            var leftSpace = MathF.Max(0f, LayoutSlot.X - workArea.X);
            if (!branchPrefersLeft &&
                rightSpace + 0.5f < _submenuWidth &&
                leftSpace > rightSpace)
            {
                branchPrefersLeft = true;
            }
            else if (branchPrefersLeft &&
                     leftSpace + 0.5f < _submenuWidth &&
                     rightSpace > leftSpace)
            {
                branchPrefersLeft = false;
            }

            // If current direction still collides while opposite doesn't, flip branch.
            var sameDirRawX = branchPrefersLeft ? preferredLeftX : preferredRightX;
            var oppositeDirRawX = branchPrefersLeft ? preferredRightX : preferredLeftX;
            var sameDirX = Math.Clamp(sameDirRawX, workArea.X, maxX);
            var oppositeDirX = Math.Clamp(oppositeDirRawX, workArea.X, maxX);
            var underRowYProbe = Math.Clamp(LayoutSlot.Y + LayoutSlot.Height, workArea.Y, maxSubmenuY);
            var obstacles = GatherOpenAncestorSubmenuBounds();
            var sameCollides = HasPanelOverlap(new LayoutRect(sameDirX, underRowYProbe, _submenuWidth, _submenuVisibleHeight), obstacles);
            var oppositeCollides = HasPanelOverlap(new LayoutRect(oppositeDirX, underRowYProbe, _submenuWidth, _submenuVisibleHeight), obstacles);
            if (sameCollides && !oppositeCollides)
            {
                branchPrefersLeft = !branchPrefersLeft;
            }

            var branchRawX = branchPrefersLeft ? preferredLeftX : preferredRightX;
            submenuX = Math.Clamp(branchRawX, workArea.X, maxX);
            _submenuOpensLeft = branchPrefersLeft;
            _submenuDirectionBiasLeft = branchPrefersLeft;

            var stepY = MathF.Max(1f, LayoutSlot.Height);
            submenuY = Math.Clamp(LayoutSlot.Y + LayoutSlot.Height, workArea.Y, maxSubmenuY);
            const int maxSteps = 6;
            var stepCount = 0;
            while (stepCount < maxSteps &&
                   HasPanelOverlap(new LayoutRect(submenuX, submenuY, _submenuWidth, _submenuVisibleHeight), obstacles))
            {
                var nextY = MathF.Min(maxSubmenuY, submenuY + stepY);
                if (nextY <= submenuY + 0.01f)
                {
                    break;
                }

                submenuY = nextY;
                stepCount++;
            }
        }
        else
        {
            _submenuDirectionBiasLeft = null;
        }

        submenuX = Math.Clamp(submenuX, workArea.X, maxX);
        submenuY = Math.Clamp(submenuY, workArea.Y, maxSubmenuY);
        var maxScroll = MathF.Max(0f, _submenuContentHeight - _submenuVisibleHeight);
        _submenuScrollOffset = Math.Clamp(_submenuScrollOffset, 0f, maxScroll);

        var currentY = submenuY - _submenuScrollOffset;
        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is not MenuItem child)
            {
                continue;
            }

            var childHeight = child.DesiredSize.Y;
            child.Arrange(new LayoutRect(submenuX, currentY, _submenuWidth, childHeight));
            currentY += childHeight;
        }

        _submenuBounds = new LayoutRect(submenuX, submenuY, _submenuWidth, _submenuVisibleHeight);
        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var rowBackground = Background;
        if (IsSubmenuOpen)
        {
            rowBackground = OpenBackground;
        }
        else if (IsHighlighted)
        {
            rowBackground = HighlightBackground;
        }

        if (rowBackground.A > 0)
        {
            UiDrawing.DrawFilledRect(spriteBatch, LayoutSlot, rowBackground, Opacity);
        }

        if (IsSubmenuOpen && HasChildItems)
        {
            UiDrawing.DrawFilledRect(spriteBatch, _submenuBounds, SubmenuBackground, Opacity);
            if (SubmenuBorderThickness > 0f)
            {
                UiDrawing.DrawRectStroke(spriteBatch, _submenuBounds, SubmenuBorderThickness, SubmenuBorderBrush, Opacity);
            }

            if (HasScrollableSubmenu)
            {
                DrawSubmenuScrollHints(spriteBatch);
            }
        }

        var padding = Padding;
        var textX = LayoutSlot.X + padding.Left;
        var textY = LayoutSlot.Y + ((LayoutSlot.Height - GetLineHeight()) / 2f);
        FontStashTextRenderer.DrawString(spriteBatch, Font, GetDisplayHeaderText(), new Vector2(textX, textY), Foreground * Opacity);

        var gestureText = GetEffectiveInputGestureText();
        if (!string.IsNullOrWhiteSpace(gestureText) && !IsTopLevelItem())
        {
            var gestureWidth = MeasureTextWidth(gestureText);
            var gestureX = LayoutSlot.X + LayoutSlot.Width - padding.Right - gestureWidth - (HasChildItems ? 12f : 0f);
            FontStashTextRenderer.DrawString(spriteBatch, Font, gestureText, new Vector2(gestureX, textY), Foreground * Opacity);
        }

        if (HasChildItems && !IsTopLevelItem())
        {
            DrawSubmenuArrow(spriteBatch, new Vector2(LayoutSlot.X + LayoutSlot.Width - padding.Right - 6f, LayoutSlot.Y + (LayoutSlot.Height / 2f)));
        }
    }






    internal void SetOwnerMenu(Menu? ownerMenu)
    {
        OwnerMenu = ownerMenu;
        OwnerContextMenu = null;

        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is MenuItem child)
            {
                child.SetOwnerMenu(ownerMenu);
            }
        }
    }

    internal void SetOwnerContextMenu(ContextMenu? ownerContextMenu)
    {
        OwnerContextMenu = ownerContextMenu;
        OwnerMenu = null;

        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is MenuItem child)
            {
                child.SetOwnerContextMenu(ownerContextMenu);
            }
        }
    }

    internal void OpenSubmenu(bool focusFirstItem)
    {
        if (!HasChildItems)
        {
            return;
        }

        if (GetParentMenuItem() is { } parent)
        {
            parent.OpenChildFromHover(this);
        }

        IsSubmenuOpen = true;
        IsHighlighted = true;

        if (focusFirstItem)
        {
            var firstChildFound = false;
            for (var i = 0; i < ItemContainers.Count; i++)
            {
                if (ItemContainers[i] is not MenuItem child)
                {
                    continue;
                }

                if (!firstChildFound)
                {
                    child.IsHighlighted = true;
                    firstChildFound = true;
                    continue;
                }

                child.IsHighlighted = false;
            }
        }
    }

    internal void CloseSubmenuRecursive(bool clearHighlight)
    {
        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is not MenuItem child)
            {
                continue;
            }

            child.CloseSubmenuRecursive(clearHighlight);
            if (clearHighlight)
            {
                child.IsHighlighted = false;
            }
        }

        IsSubmenuOpen = false;
        if (clearHighlight)
        {
            IsHighlighted = false;
        }
    }

    internal MenuItem GetTopLevelAncestor()
    {
        var current = this;
        while (current.GetParentMenuItem() is { } parent)
        {
            current = parent;
        }

        return current;
    }

    private bool IsTopLevelItem()
    {
        return (VisualParent is Menu || LogicalParent is Menu) &&
               OwnerContextMenu == null;
    }

    private void OnIsSubmenuOpenChanged(bool isOpen)
    {
        if (!isOpen)
        {
            for (var i = 0; i < ItemContainers.Count; i++)
            {
                if (ItemContainers[i] is MenuItem child)
                {
                    child.CloseSubmenuRecursive(clearHighlight: true);
                }
            }

            _submenuScrollOffset = 0f;
            _submenuDirectionBiasLeft = null;
        }
    }

    internal MenuItem? GetParentMenuItem()
    {
        return VisualParent as MenuItem ?? LogicalParent as MenuItem;
    }

    internal IReadOnlyList<MenuItem> GetChildMenuItems()
    {
        var children = new List<MenuItem>();
        foreach (var child in ItemContainers)
        {
            if (child is MenuItem menuItem)
            {
                children.Add(menuItem);
            }
        }

        return children;
    }

    internal bool MoveSibling(int delta)
    {
        var parent = GetParentMenuItem();
        if (parent == null)
        {
            return false;
        }

        var siblings = parent.GetChildMenuItems();
        if (siblings.Count == 0)
        {
            return false;
        }

        var index = -1;
        for (var i = 0; i < siblings.Count; i++)
        {
            if (ReferenceEquals(siblings[i], this))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            return false;
        }

        var nextIndex = (index + delta) % siblings.Count;
        if (nextIndex < 0)
        {
            nextIndex += siblings.Count;
        }

        var target = siblings[nextIndex];
        foreach (var sibling in siblings)
        {
            if (!ReferenceEquals(sibling, target))
            {
                sibling.CloseSubmenuRecursive(clearHighlight: true);
            }
        }

        target.IsHighlighted = true;
        return true;
    }

    internal void OpenChildFromHover(MenuItem hoveredChild)
    {
        if (ReferenceEquals(GetHighlightedChild(), hoveredChild) &&
            (!hoveredChild.HasChildItems || hoveredChild.IsSubmenuOpen))
        {
            return;
        }

        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is not MenuItem child)
            {
                continue;
            }

            if (ReferenceEquals(child, hoveredChild))
            {
                child.IsHighlighted = true;
                if (child.HasChildItems)
                {
                    child.IsSubmenuOpen = true;
                }

                continue;
            }

            if (child.IsSubmenuOpen || child.IsHighlighted)
            {
                child.CloseSubmenuRecursive(clearHighlight: true);
            }
        }
    }

    private void RaiseClick()
    {
        RaiseRoutedEvent(ClickEvent, new RoutedSimpleEventArgs(ClickEvent));
    }

    internal bool InvokeLeaf()
    {
        if (Command != null)
        {
            return ExecuteCommand();
        }

        RaiseClick();
        return true;
    }

    private float MeasureTextWidth(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0f;
        }

        if (Font == null)
        {
            return text.Length * 8f;
        }

        return FontStashTextRenderer.MeasureWidth(Font, text);
    }

    private float GetLineHeight()
    {
        return Font == null ? 14f : FontStashTextRenderer.GetLineHeight(Font);
    }

    private void DrawSubmenuArrow(SpriteBatch spriteBatch, Vector2 center)
    {
        var color = Foreground * Opacity;
        if (_submenuOpensLeft)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X + 2f, center.Y - 3f, 1f, 7f), color, 1f);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X + 1f, center.Y - 2f, 1f, 5f), color, 1f);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X, center.Y - 1f, 1f, 3f), color, 1f);
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - 1f, center.Y, 1f, 1f), color, 1f);
            return;
        }

        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - 2f, center.Y - 3f, 1f, 7f), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - 1f, center.Y - 2f, 1f, 5f), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X, center.Y - 1f, 1f, 3f), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X + 1f, center.Y, 1f, 1f), color, 1f);
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        if (IsSubmenuOpen && HasScrollableSubmenu)
        {
            clipRect = UnionRects(LayoutSlot, _submenuBounds);
            return true;
        }

        return base.TryGetClipRect(out clipRect);
    }

    private LayoutRect ResolveWorkAreaBounds()
    {
        if (OwnerContextMenu != null)
        {
            return OwnerContextMenu.GetWorkAreaBounds();
        }

        if (OwnerMenu?.VisualParent is FrameworkElement host)
        {
            return host.LayoutSlot;
        }

        return new LayoutRect(0f, 0f, MathF.Max(1f, LayoutSlot.Width), MathF.Max(1f, LayoutSlot.Height));
    }

    private bool HasScrollableSubmenu => IsSubmenuOpen && HasChildItems && _submenuContentHeight - _submenuVisibleHeight > 0.5f;

    private bool IsPointInsideOpenSubmenu(Vector2 point)
    {
        return point.X >= _submenuBounds.X &&
               point.X <= _submenuBounds.X + _submenuBounds.Width &&
               point.Y >= _submenuBounds.Y &&
               point.Y <= _submenuBounds.Y + _submenuBounds.Height;
    }

    private bool TryAdjustSubmenuScrollOffset(float delta)
    {
        var maxScroll = MathF.Max(0f, _submenuContentHeight - _submenuVisibleHeight);
        if (maxScroll <= 0f)
        {
            return false;
        }

        var next = Math.Clamp(_submenuScrollOffset + delta, 0f, maxScroll);
        if (MathF.Abs(next - _submenuScrollOffset) < 0.01f)
        {
            return false;
        }

        _submenuScrollOffset = next;
        InvalidateArrange();
        return true;
    }

    private float GetScrollLineHeight()
    {
        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is MenuItem child && child.DesiredSize.Y > 0f)
            {
                return child.DesiredSize.Y;
            }
        }

        return MathF.Max(20f, GetLineHeight() + Padding.Vertical);
    }

    private void DrawSubmenuScrollHints(SpriteBatch spriteBatch)
    {
        var hintColor = Foreground * Opacity;
        if (_submenuScrollOffset > 0.01f)
        {
            var topHint = new LayoutRect(_submenuBounds.X + 2f, _submenuBounds.Y + 1f, _submenuBounds.Width - 4f, 2f);
            UiDrawing.DrawFilledRect(spriteBatch, topHint, hintColor, 0.6f);
        }

        var maxScroll = MathF.Max(0f, _submenuContentHeight - _submenuVisibleHeight);
        if (_submenuScrollOffset < maxScroll - 0.01f)
        {
            var bottomHint = new LayoutRect(_submenuBounds.X + 2f, _submenuBounds.Y + _submenuBounds.Height - 3f, _submenuBounds.Width - 4f, 2f);
            UiDrawing.DrawFilledRect(spriteBatch, bottomHint, hintColor, 0.6f);
        }
    }

    private static LayoutRect UnionRects(LayoutRect a, LayoutRect b)
    {
        var minX = MathF.Min(a.X, b.X);
        var minY = MathF.Min(a.Y, b.Y);
        var maxX = MathF.Max(a.X + a.Width, b.X + b.Width);
        var maxY = MathF.Max(a.Y + a.Height, b.Y + b.Height);
        return new LayoutRect(minX, minY, MathF.Max(0f, maxX - minX), MathF.Max(0f, maxY - minY));
    }

    private List<LayoutRect> GatherOpenAncestorSubmenuBounds()
    {
        var bounds = new List<LayoutRect>();
        for (var current = GetParentMenuItem(); current != null; current = current.GetParentMenuItem())
        {
            if (current.TryGetOpenSubmenuBounds(out var currentBounds))
            {
                bounds.Add(currentBounds);
            }
        }

        if (OwnerContextMenu != null)
        {
            bounds.Add(OwnerContextMenu.LayoutSlot);
        }

        return bounds;
    }

    private static bool HasPanelOverlap(LayoutRect candidate, List<LayoutRect> obstacles)
    {
        for (var i = 0; i < obstacles.Count; i++)
        {
            if (ComputeOverlapArea(candidate, obstacles[i]) > 0.5f)
            {
                return true;
            }
        }

        return false;
    }

    private static float ComputeHorizontalOverflow(float candidateX, float width, LayoutRect workArea)
    {
        var leftOverflow = MathF.Max(0f, workArea.X - candidateX);
        var rightOverflow = MathF.Max(0f, (candidateX + width) - (workArea.X + workArea.Width));
        return leftOverflow + rightOverflow;
    }

    private static float ComputeOverlapArea(LayoutRect a, LayoutRect b)
    {
        var overlapX = MathF.Max(0f, MathF.Min(a.X + a.Width, b.X + b.Width) - MathF.Max(a.X, b.X));
        var overlapY = MathF.Max(0f, MathF.Min(a.Y + a.Height, b.Y + b.Height) - MathF.Max(a.Y, b.Y));
        return overlapX * overlapY;
    }

    private string GetEffectiveInputGestureText()
    {
        if (!string.IsNullOrWhiteSpace(InputGestureText))
        {
            return InputGestureText;
        }

        if (Command == null)
        {
            return string.Empty;
        }

        var routeStart = CommandTarget ?? this;
        return InputGestureService.TryGetFirstGestureTextForCommand(Command, routeStart, out var gestureText)
            ? gestureText
            : string.Empty;
    }

    internal MenuItem? GetFirstChildMenuItem()
    {
        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is MenuItem child)
            {
                return child;
            }
        }

        return null;
    }

    internal MenuItem? GetLastChildMenuItem()
    {
        for (var i = ItemContainers.Count - 1; i >= 0; i--)
        {
            if (ItemContainers[i] is MenuItem child)
            {
                return child;
            }
        }

        return null;
    }

    internal MenuItem? GetHighlightedChild()
    {
        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is MenuItem child &&
                child.IsHighlighted)
            {
                return child;
            }
        }

        return null;
    }

    internal IReadOnlyList<UIElement> GetChildItemContainersForTraversal()
    {
        return ItemContainers;
    }

    internal bool HitTestRowFast(Vector2 point)
    {
        var slot = LayoutSlot;
        if (slot.Width <= 0f || slot.Height <= 0f)
        {
            return false;
        }

        return point.X >= slot.X &&
               point.X <= slot.X + slot.Width &&
               point.Y >= slot.Y &&
               point.Y <= slot.Y + slot.Height;
    }

    internal bool HitTestOpenSubmenuBoundsFast(Vector2 point)
    {
        if (!IsSubmenuOpen || !HasChildItems)
        {
            return false;
        }

        // If bounds are not initialized yet in this frame, do not block recursive probing.
        if (_submenuBounds.Width <= 0f || _submenuBounds.Height <= 0f)
        {
            return true;
        }

        return point.X >= _submenuBounds.X &&
               point.X <= _submenuBounds.X + _submenuBounds.Width &&
               point.Y >= _submenuBounds.Y &&
               point.Y <= _submenuBounds.Y + _submenuBounds.Height;
    }

    internal bool HitTestOpenSubmenuBoundsFast(Vector2 point, out bool usedUninitializedBoundsFallback)
    {
        usedUninitializedBoundsFallback = false;
        if (!IsSubmenuOpen || !HasChildItems)
        {
            return false;
        }

        if (_submenuBounds.Width <= 0f || _submenuBounds.Height <= 0f)
        {
            usedUninitializedBoundsFallback = true;
            return true;
        }

        return point.X >= _submenuBounds.X &&
               point.X <= _submenuBounds.X + _submenuBounds.Width &&
               point.Y >= _submenuBounds.Y &&
               point.Y <= _submenuBounds.Y + _submenuBounds.Height;
    }

    internal bool TryGetOpenSubmenuBounds(out LayoutRect bounds)
    {
        if (!IsSubmenuOpen || !HasChildItems || _submenuBounds.Width <= 0f || _submenuBounds.Height <= 0f)
        {
            bounds = default;
            return false;
        }

        bounds = _submenuBounds;
        return true;
    }

    internal bool TryHandleWheelForOpenSubmenus(Vector2 pointerPosition, int wheelDelta)
    {
        if (!IsSubmenuOpen)
        {
            return false;
        }

        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is MenuItem child &&
                child.TryHandleWheelForOpenSubmenus(pointerPosition, wheelDelta))
            {
                return true;
            }
        }

        if (!IsPointInsideOpenSubmenu(pointerPosition) || !HasScrollableSubmenu)
        {
            return false;
        }

        var notchCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
        var scrollDelta = wheelDelta < 0
            ? GetScrollLineHeight() * notchCount
            : -GetScrollLineHeight() * notchCount;
        return TryAdjustSubmenuScrollOffset(scrollDelta);
    }

    internal bool HandlePointerDownFromInput()
    {
        if (OwnerContextMenu != null)
        {
            return OwnerContextMenu.HandlePointerDownFromInput(this);
        }

        if (OwnerMenu == null)
        {
            return false;
        }

        if (IsTopLevelItem())
        {
            if (OwnerMenu.IsMenuMode && IsSubmenuOpen)
            {
                OwnerMenu.CloseAllSubmenus(restoreFocus: true);
            }
            else
            {
                OwnerMenu.EnterMenuMode(this, FocusManager.GetFocusedElement());
                if (HasChildItems)
                {
                    OpenSubmenu(focusFirstItem: false);
                }
            }

            return true;
        }

        if (OwnerMenu.IsMenuMode)
        {
            if (GetParentMenuItem() is { } parent)
            {
                parent.OpenChildFromHover(this);
            }
            else
            {
                IsHighlighted = true;
            }

            if (HasChildItems)
            {
                OpenSubmenu(focusFirstItem: false);
            }

            return true;
        }

        return false;
    }

    internal bool HandlePointerUpFromInput()
    {
        if (OwnerContextMenu != null)
        {
            return OwnerContextMenu.HandlePointerUpFromInput(this);
        }

        if (OwnerMenu == null || !OwnerMenu.IsMenuMode)
        {
            return false;
        }

        if (HasChildItems)
        {
            OpenSubmenu(focusFirstItem: false);
            return true;
        }

        var invoked = InvokeLeaf();
        if (invoked)
        {
            OwnerMenu.NotifyLeafInvoked(this);
        }

        return invoked;
    }

    internal void HandlePointerMoveFromInput()
    {
        if (OwnerContextMenu != null)
        {
            OwnerContextMenu.HandlePointerMoveFromInput(this);
            return;
        }

        if (OwnerMenu == null || !OwnerMenu.IsMenuMode)
        {
            return;
        }

        if (IsTopLevelItem())
        {
            OwnerMenu.OpenFromHover(this);
            return;
        }

        if (GetParentMenuItem() is { } parent)
        {
            parent.OpenChildFromHover(this);
        }
    }

    private string GetDisplayHeaderText()
    {
        return MenuAccessText.StripAccessMarkers(Header);
    }
}
