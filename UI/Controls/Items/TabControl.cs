using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class TabControl : Selector
{
    public new static readonly DependencyProperty FontProperty = Control.FontProperty;

    public new static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(TabControl),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(TabControl),
            new FrameworkPropertyMetadata(new Color(20, 20, 20), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HeaderBackgroundProperty =
        DependencyProperty.Register(
            nameof(HeaderBackground),
            typeof(Color),
            typeof(TabControl),
            new FrameworkPropertyMetadata(new Color(42, 42, 42), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedHeaderBackgroundProperty =
        DependencyProperty.Register(
            nameof(SelectedHeaderBackground),
            typeof(Color),
            typeof(TabControl),
            new FrameworkPropertyMetadata(new Color(71, 102, 145), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(TabControl),
            new FrameworkPropertyMetadata(new Color(95, 95, 95), FrameworkPropertyMetadataOptions.AffectsRender));

    public new static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(Thickness),
            typeof(TabControl),
            new FrameworkPropertyMetadata(
                new Thickness(1f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is Thickness thickness
                    ? new Thickness(
                        MathF.Max(0f, thickness.Left),
                        MathF.Max(0f, thickness.Top),
                        MathF.Max(0f, thickness.Right),
                        MathF.Max(0f, thickness.Bottom))
                    : Thickness.Empty));

    public static readonly DependencyProperty HeaderPaddingProperty =
        DependencyProperty.Register(
            nameof(HeaderPadding),
            typeof(Thickness),
            typeof(TabControl),
            new FrameworkPropertyMetadata(new Thickness(10f, 6f, 10f, 6f), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly List<HeaderSlot> _headerSlots = new();

    protected override bool IncludeGeneratedChildrenInVisualTree => false;

    public TabControl()
    {
        AddHandler<MouseRoutedEventArgs>(UIElement.MouseDownEvent, OnMouseDownSelectTab);
    }

    public new SpriteFont? Font
    {
        get => GetValue<SpriteFont>(FontProperty);
        set => SetValue(FontProperty, value);
    }

    public new Color Foreground
    {
        get => GetValue<Color>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public new Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color HeaderBackground
    {
        get => GetValue<Color>(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    public Color SelectedHeaderBackground
    {
        get => GetValue<Color>(SelectedHeaderBackgroundProperty);
        set => SetValue(SelectedHeaderBackgroundProperty, value);
    }

    public new Color BorderBrush
    {
        get => GetValue<Color>(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public new Thickness BorderThickness
    {
        get => GetValue<Thickness>(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Thickness HeaderPadding
    {
        get => GetValue<Thickness>(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }

    public override IEnumerable<UIElement> GetVisualChildren()
    {
        foreach (var child in base.GetVisualChildren())
        {
            yield return child;
        }

        if (SelectedItem is TabItem selected)
        {
            yield return selected;
        }
    }

    internal override int GetVisualChildCountForTraversal()
    {
        return base.GetVisualChildCountForTraversal() + (SelectedItem is TabItem ? 1 : 0);
    }

    internal override UIElement GetVisualChildAtForTraversal(int index)
    {
        var baseCount = base.GetVisualChildCountForTraversal();
        if (index < baseCount)
        {
            return base.GetVisualChildAtForTraversal(index);
        }

        if (index == baseCount && SelectedItem is TabItem selected)
        {
            return selected;
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    public override IEnumerable<UIElement> GetLogicalChildren()
    {
        foreach (var child in base.GetLogicalChildren())
        {
            yield return child;
        }

        if (SelectedItem is TabItem selected)
        {
            yield return selected;
        }
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is TabItem;
    }

    protected override UIElement CreateContainerForItemOverride(object item)
    {
        return new TabItem
        {
            Header = item?.ToString() ?? string.Empty
        };
    }

    protected override void OnItemsChanged()
    {
        base.OnItemsChanged();

        if (ItemContainers.Count > 0 && SelectedIndex < 0)
        {
            SetSelectedIndexInternal(0);
        }
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs args)
    {
        base.OnSelectionChanged(args);

        foreach (var item in ItemContainers)
        {
            if (item is TabItem tab)
            {
                tab.IsSelected = ReferenceEquals(tab, SelectedItem);
            }
        }

        InvalidateMeasure();
        InvalidateArrange();
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = base.MeasureOverride(availableSize);

        var headerHeight = GetHeaderHeight();
        var totalHeaderWidth = 0f;
        foreach (var item in ItemContainers)
        {
            if (item is not TabItem tab)
            {
                continue;
            }

            totalHeaderWidth += MeasureHeaderWidth(tab);
        }

        desired.X = MathF.Max(desired.X, totalHeaderWidth);
        desired.Y = MathF.Max(desired.Y, headerHeight);

        if (SelectedItem is TabItem selected && selected is FrameworkElement selectedElement)
        {
            var border = BorderThickness;
            selectedElement.Measure(new Vector2(
                MathF.Max(0f, availableSize.X - border.Horizontal),
                MathF.Max(0f, availableSize.Y - headerHeight - border.Vertical)));
            desired.X = MathF.Max(desired.X, selectedElement.DesiredSize.X);
            desired.Y = MathF.Max(desired.Y, headerHeight + selectedElement.DesiredSize.Y + border.Vertical);
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        base.ArrangeOverride(finalSize);

        _headerSlots.Clear();

        var headerHeight = GetHeaderHeight();
        var currentX = LayoutSlot.X;

        for (var i = 0; i < ItemContainers.Count; i++)
        {
            if (ItemContainers[i] is not TabItem tab)
            {
                continue;
            }

            var width = MeasureHeaderWidth(tab);
            _headerSlots.Add(new HeaderSlot(i, new LayoutRect(currentX, LayoutSlot.Y, width, headerHeight), tab));
            currentX += width;
        }

        if (SelectedItem is TabItem selected && selected is FrameworkElement selectedElement)
        {
            var border = BorderThickness;
            selectedElement.Arrange(new LayoutRect(
                LayoutSlot.X + border.Left,
                LayoutSlot.Y + headerHeight,
                MathF.Max(0f, finalSize.X - border.Horizontal),
                MathF.Max(0f, finalSize.Y - headerHeight - border.Bottom)));
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        foreach (var header in _headerSlots)
        {
            var headerBackgroundSource = header.Item.GetValueSource(Control.BackgroundProperty);
            var color = headerBackgroundSource == DependencyPropertyValueSource.Default
                ? (header.Index == SelectedIndex ? SelectedHeaderBackground : HeaderBackground)
                : header.Item.Background;
            UiDrawing.DrawFilledRect(spriteBatch, header.Rect, color, Opacity);

            var headerBorderSource = header.Item.GetValueSource(Control.BorderBrushProperty);
            var borderBrush = headerBorderSource == DependencyPropertyValueSource.Default
                ? BorderBrush
                : header.Item.BorderBrush;
            UiDrawing.DrawRectStroke(spriteBatch, header.Rect, 1f, borderBrush, Opacity);

            var font = UiTextRenderer.ResolveFont(Font);
            if (font == null)
            {
                continue;
            }

            var text = header.Item.Header;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var textColorSource = header.Item.GetValueSource(Control.ForegroundProperty);
            var textColor = textColorSource == DependencyPropertyValueSource.Default
                ? Foreground
                : header.Item.Foreground;

            var textWidth = UiTextRenderer.MeasureWidth(font, text, FontSize);
            var x = header.Rect.X + ((header.Rect.Width - textWidth) / 2f);
            var y = header.Rect.Y + ((header.Rect.Height - UiTextRenderer.GetLineHeight(font, FontSize)) / 2f);
            UiTextRenderer.DrawString(spriteBatch, font, text, new Vector2(x, y), textColor * Opacity, FontSize);
        }

        var border = BorderThickness;
        if (border.Left > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, slot.Y, border.Left, slot.Height), BorderBrush, Opacity);
        }

        if (border.Top > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, slot.Y, slot.Width, border.Top), BorderBrush, Opacity);
        }

        if (border.Right > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X + slot.Width - border.Right, slot.Y, border.Right, slot.Height), BorderBrush, Opacity);
        }

        if (border.Bottom > 0f)
        {
            UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(slot.X, slot.Y + slot.Height - border.Bottom, slot.Width, border.Bottom), BorderBrush, Opacity);
        }
    }



    private float GetHeaderHeight()
    {
        var padding = HeaderPadding;
        var textHeight = UiTextRenderer.GetLineHeight(Font, FontSize);
        return padding.Vertical + textHeight;
    }

    private float MeasureHeaderWidth(TabItem item)
    {
        var padding = HeaderPadding;
        var textWidth = 26f;
        if (Font != null && !string.IsNullOrEmpty(item.Header))
        {
            textWidth = UiTextRenderer.MeasureWidth(Font, item.Header, FontSize);
        }

        return MathF.Max(36f, padding.Horizontal + textWidth);
    }

    private static bool Contains(LayoutRect rect, Vector2 point)
    {
        return point.X >= rect.X && point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
    }

    private void OnMouseDownSelectTab(object? sender, MouseRoutedEventArgs args)
    {
        _ = sender;
        if (!IsEnabled || args.Button != MouseButton.Left)
        {
            return;
        }

        if (TryGetHeaderIndexAtPoint(args.Position, out var clickedIndex) &&
            clickedIndex != SelectedIndex)
        {
            SetSelectedIndexInternal(clickedIndex);
        }
    }

    private bool TryGetHeaderIndexAtPoint(Vector2 pointerPosition, out int headerIndex)
    {
        for (var i = 0; i < _headerSlots.Count; i++)
        {
            if (Contains(_headerSlots[i].Rect, pointerPosition))
            {
                headerIndex = _headerSlots[i].Index;
                return true;
            }
        }

        headerIndex = -1;
        return false;
    }

    private readonly struct HeaderSlot
    {
        public HeaderSlot(int index, LayoutRect rect, TabItem item)
        {
            Index = index;
            Rect = rect;
            Item = item;
        }

        public int Index { get; }

        public LayoutRect Rect { get; }

        public TabItem Item { get; }
    }
}

