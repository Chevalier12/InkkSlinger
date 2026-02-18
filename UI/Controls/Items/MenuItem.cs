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
    private float _submenuHeight;
    private LayoutRect _submenuBounds;

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
        _submenuHeight = 0f;

        foreach (var child in GetChildMenuItems())
        {
            child.Measure(availableSize);
            _submenuWidth = MathF.Max(_submenuWidth, child.DesiredSize.X);
            _submenuHeight += child.DesiredSize.Y;
        }

        _submenuWidth = MathF.Max(_submenuWidth, rowWidth);
        return new Vector2(rowWidth, rowHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (!IsSubmenuOpen || !HasChildItems)
        {
            _submenuBounds = default;
            return finalSize;
        }

        var submenuX = IsTopLevelItem()
            ? LayoutSlot.X
            : LayoutSlot.X + LayoutSlot.Width;
        var submenuY = IsTopLevelItem()
            ? LayoutSlot.Y + LayoutSlot.Height
            : LayoutSlot.Y;

        var currentY = submenuY;
        foreach (var child in GetChildMenuItems())
        {
            var childHeight = child.DesiredSize.Y;
            child.Arrange(new LayoutRect(submenuX, currentY, _submenuWidth, childHeight));
            currentY += childHeight;
        }

        _submenuBounds = new LayoutRect(submenuX, submenuY, _submenuWidth, _submenuHeight);
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

        foreach (var child in GetChildMenuItems())
        {
            child.SetOwnerMenu(ownerMenu);
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
            var childItems = GetChildMenuItems();
            if (childItems.Count > 0)
            {
                for (var i = 1; i < childItems.Count; i++)
                {
                    childItems[i].IsHighlighted = false;
                }

                childItems[0].IsHighlighted = true;
            }
        }
    }

    internal void CloseSubmenuRecursive(bool clearHighlight)
    {
        foreach (var child in GetChildMenuItems())
        {
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
        return VisualParent is Menu || LogicalParent is Menu;
    }

    private void OnIsSubmenuOpenChanged(bool isOpen)
    {
        if (!isOpen)
        {
            foreach (var child in GetChildMenuItems())
            {
                child.CloseSubmenuRecursive(clearHighlight: true);
            }
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
        var children = GetChildMenuItems();
        foreach (var child in children)
        {
            if (ReferenceEquals(child, hoveredChild))
            {
                child.IsHighlighted = true;
                if (child.HasChildItems)
                {
                    child.IsSubmenuOpen = true;
                }

                continue;
            }

            child.CloseSubmenuRecursive(clearHighlight: true);
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
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - 2f, center.Y - 3f, 1f, 7f), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X - 1f, center.Y - 2f, 1f, 5f), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X, center.Y - 1f, 1f, 3f), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(center.X + 1f, center.Y, 1f, 1f), color, 1f);
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
        var children = GetChildMenuItems();
        return children.Count > 0 ? children[0] : null;
    }

    internal MenuItem? GetLastChildMenuItem()
    {
        var children = GetChildMenuItems();
        return children.Count > 0 ? children[^1] : null;
    }

    internal MenuItem? GetHighlightedChild()
    {
        foreach (var child in GetChildMenuItems())
        {
            if (child.IsHighlighted)
            {
                return child;
            }
        }

        return null;
    }

    internal bool HandlePointerDownFromInput()
    {
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
