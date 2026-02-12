using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class ToolBar : ItemsControl
{
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(ToolBar),
            new FrameworkPropertyMetadata(new Color(21, 31, 45), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(ToolBar),
            new FrameworkPropertyMetadata(new Color(72, 103, 136), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(ToolBar),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(ToolBar),
            new FrameworkPropertyMetadata(
                new Thickness(4f, 2f, 4f, 2f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemSpacingProperty =
        DependencyProperty.Register(
            nameof(ItemSpacing),
            typeof(float),
            typeof(ToolBar),
            new FrameworkPropertyMetadata(
                2f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty OverflowButtonTextProperty =
        DependencyProperty.Register(
            nameof(OverflowButtonText),
            typeof(string),
            typeof(ToolBar),
            new FrameworkPropertyMetadata("...", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsOverflowOpenProperty =
        DependencyProperty.Register(
            nameof(IsOverflowOpen),
            typeof(bool),
            typeof(ToolBar),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                propertyChangedCallback: static (dependencyObject, args) =>
                {
                    if (dependencyObject is ToolBar toolBar)
                    {
                        toolBar.OnIsOverflowOpenChanged(args.NewValue is bool value && value);
                    }
                }));

    private readonly Button _overflowButton;
    private readonly ToolBarOverflowPanel _overflowPanel;
    private int _visibleItemCount;
    private int _overflowItemCount;
    private LayoutRect _overflowPanelRect;
    private Panel? _hostPanel;

    public ToolBar()
    {
        _overflowButton = new Button
        {
            Text = OverflowButtonText,
            MinWidth = 24f,
            Padding = new Thickness(6f, 4f, 6f, 4f)
        };
        _overflowButton.Click += (_, _) => IsOverflowOpen = !IsOverflowOpen;
        _overflowButton.SetVisualParent(this);
        _overflowButton.SetLogicalParent(this);

        _overflowPanel = new ToolBarOverflowPanel
        {
            IsVisible = false
        };
        _overflowPanel.SetVisualParent(this);
        _overflowPanel.SetLogicalParent(this);
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

    public string OverflowButtonText
    {
        get => GetValue<string>(OverflowButtonTextProperty) ?? "...";
        set => SetValue(OverflowButtonTextProperty, value);
    }

    public bool IsOverflowOpen
    {
        get => GetValue<bool>(IsOverflowOpenProperty);
        set => SetValue(IsOverflowOpenProperty, value);
    }

    protected internal int VisibleItemCountForTesting => _visibleItemCount;
    protected internal int OverflowItemCountForTesting => _overflowItemCount;
    protected internal Button OverflowButtonForTesting => _overflowButton;

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }

        yield return _overflowButton;
        yield return _overflowPanel;
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in base.GetLogicalChildren())
        {
            yield return child;
        }

        yield return _overflowButton;
        yield return _overflowPanel;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var items = GetToolbarItems();
        var padding = Padding;
        var border = BorderThickness;
        var innerWidth = MathF.Max(0f, availableSize.X - (border * 2f) - padding.Horizontal);
        var innerHeight = MathF.Max(0f, availableSize.Y - (border * 2f) - padding.Vertical);

        var itemWidths = new float[items.Count];
        var itemHeights = new float[items.Count];
        var maxItemHeight = 0f;

        for (var i = 0; i < items.Count; i++)
        {
            items[i].Measure(new Vector2(innerWidth, innerHeight));
            itemWidths[i] = items[i].DesiredSize.X;
            itemHeights[i] = items[i].DesiredSize.Y;
            maxItemHeight = MathF.Max(maxItemHeight, itemHeights[i]);
        }

        _overflowButton.Text = OverflowButtonText;
        _overflowButton.Measure(new Vector2(innerWidth, innerHeight));
        var overflowButtonWidth = _overflowButton.DesiredSize.X;
        var overflowButtonHeight = _overflowButton.DesiredSize.Y;

        var canConstrain = !float.IsInfinity(innerWidth);
        _visibleItemCount = items.Count;

        if (canConstrain)
        {
            _visibleItemCount = CalculateVisibleItems(itemWidths, innerWidth, overflowButtonWidth);
        }

        _overflowItemCount = items.Count - _visibleItemCount;

        var mainWidth = CalculateWidth(itemWidths, _visibleItemCount);
        if (_overflowItemCount > 0)
        {
            if (_visibleItemCount > 0)
            {
                mainWidth += ItemSpacing;
            }

            mainWidth += overflowButtonWidth;
        }

        var rowHeight = MathF.Max(maxItemHeight, overflowButtonHeight);

        var overflowWidth = 0f;
        var overflowHeight = 0f;
        if (_overflowItemCount > 0 && IsOverflowOpen)
        {
            for (var i = _visibleItemCount; i < items.Count; i++)
            {
                overflowWidth = MathF.Max(overflowWidth, itemWidths[i]);
                overflowHeight += itemHeights[i];
                if (i < items.Count - 1)
                {
                    overflowHeight += ItemSpacing;
                }
            }

            overflowWidth = MathF.Max(overflowWidth + (padding.Horizontal * 0.5f), 80f);
        }

        _overflowPanel.Measure(new Vector2(overflowWidth, overflowHeight));

        var desiredWidth = mainWidth + (border * 2f) + padding.Horizontal;
        var desiredHeight = rowHeight + (border * 2f) + padding.Vertical;
        if (_overflowItemCount > 0 && IsOverflowOpen)
        {
            desiredHeight += ItemSpacing + overflowHeight + (_overflowPanel.BorderThickness * 2f);
        }

        return new Vector2(desiredWidth, desiredHeight);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var items = GetToolbarItems();
        var padding = Padding;
        var border = BorderThickness;
        var availableContentWidth = MathF.Max(0f, finalSize.X - (border * 2f) - padding.Horizontal);
        RecalculateVisibility(items, availableContentWidth);

        var contentX = LayoutSlot.X + border + padding.Left;
        var contentY = LayoutSlot.Y + border + padding.Top;
        var contentHeight = MathF.Max(0f, finalSize.Y - (border * 2f) - padding.Vertical);

        var x = contentX;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var isVisibleInMainRow = i < _visibleItemCount;
            if (!isVisibleInMainRow)
            {
                item.Arrange(new LayoutRect(0f, 0f, 0f, 0f));
                continue;
            }

            var width = item.DesiredSize.X;
            item.Arrange(new LayoutRect(x, contentY, width, contentHeight));
            x += width + ItemSpacing;
        }

        if (_overflowItemCount > 0)
        {
            var overflowButtonWidth = _overflowButton.DesiredSize.X;
            _overflowButton.IsVisible = true;
            _overflowButton.Arrange(new LayoutRect(x, contentY, overflowButtonWidth, contentHeight));
        }
        else
        {
            _overflowButton.IsVisible = false;
            _overflowButton.Arrange(new LayoutRect(0f, 0f, 0f, 0f));
        }

        if (_overflowItemCount > 0 && IsOverflowOpen)
        {
            var panelX = contentX;
            var panelY = contentY + contentHeight + ItemSpacing;
            var panelWidth = MathF.Max(_overflowButton.LayoutSlot.X + _overflowButton.LayoutSlot.Width - contentX, 100f);
            var panelHeight = 0f;
            for (var i = _visibleItemCount; i < items.Count; i++)
            {
                panelHeight += items[i].DesiredSize.Y;
                if (i < items.Count - 1)
                {
                    panelHeight += ItemSpacing;
                }
            }

            panelHeight += _overflowPanel.BorderThickness * 2f;
            _overflowPanelRect = new LayoutRect(panelX, panelY, panelWidth, panelHeight);
            _overflowPanel.IsVisible = true;
            _overflowPanel.Arrange(_overflowPanelRect);

            var overflowY = panelY + _overflowPanel.BorderThickness;
            for (var i = _visibleItemCount; i < items.Count; i++)
            {
                var height = items[i].DesiredSize.Y;
                items[i].Arrange(new LayoutRect(panelX + _overflowPanel.BorderThickness, overflowY, panelWidth - (_overflowPanel.BorderThickness * 2f), height));
                overflowY += height + ItemSpacing;
            }
        }
        else
        {
            _overflowPanel.IsVisible = false;
            _overflowPanel.Arrange(new LayoutRect(0f, 0f, 0f, 0f));
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

    protected override void OnDependencyPropertyChanged(DependencyPropertyChangedEventArgs args)
    {
        base.OnDependencyPropertyChanged(args);

        if (args.Property == OverflowButtonTextProperty)
        {
            _overflowButton.Text = OverflowButtonText;
        }
    }

    private void OnIsOverflowOpenChanged(bool isOpen)
    {
        if (!isOpen)
        {
            return;
        }

        if (_overflowItemCount <= 0)
        {
            IsOverflowOpen = false;
        }
    }

    private IReadOnlyList<FrameworkElement> GetToolbarItems()
    {
        var result = new List<FrameworkElement>();
        foreach (var item in ItemContainers)
        {
            if (item is FrameworkElement element)
            {
                result.Add(element);
            }
        }

        return result;
    }

    private int CalculateVisibleItems(IReadOnlyList<float> itemWidths, float innerWidth, float overflowButtonWidth)
    {
        var visible = itemWidths.Count;
        while (visible > 0)
        {
            var width = CalculateWidth(itemWidths, visible);
            var total = width;
            if (visible < itemWidths.Count)
            {
                if (visible > 0)
                {
                    total += ItemSpacing;
                }

                total += overflowButtonWidth;
            }

            if (total <= innerWidth)
            {
                return visible;
            }

            visible--;
        }

        return 0;
    }

    private void RecalculateVisibility(IReadOnlyList<FrameworkElement> items, float innerWidth)
    {
        if (float.IsInfinity(innerWidth))
        {
            _visibleItemCount = items.Count;
            _overflowItemCount = 0;
            return;
        }

        var widths = new float[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            widths[i] = items[i].DesiredSize.X;
        }

        var overflowButtonWidth = _overflowButton.DesiredSize.X;
        _visibleItemCount = CalculateVisibleItems(widths, innerWidth, overflowButtonWidth);
        _overflowItemCount = items.Count - _visibleItemCount;
        if (_overflowItemCount <= 0)
        {
            IsOverflowOpen = false;
        }
    }

    private float CalculateWidth(IReadOnlyList<float> itemWidths, int count)
    {
        var width = 0f;
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                width += ItemSpacing;
            }

            width += itemWidths[i];
        }

        return width;
    }

    private void RefreshHostSubscriptions()
    {
        if (_hostPanel != null)
        {
            _hostPanel.PreviewMouseDown -= OnHostPreviewMouseDown;
            _hostPanel = null;
        }

        _hostPanel = FindHostPanel();
        if (_hostPanel == null)
        {
            return;
        }

        _hostPanel.PreviewMouseDown += OnHostPreviewMouseDown;
    }

    private void OnHostPreviewMouseDown(object? sender, RoutedMouseButtonEventArgs args)
    {
        if (!IsOverflowOpen)
        {
            return;
        }

        var source = args.OriginalSource;
        if (source != null && IsSelfOrDescendant(source))
        {
            return;
        }

        IsOverflowOpen = false;
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
}
