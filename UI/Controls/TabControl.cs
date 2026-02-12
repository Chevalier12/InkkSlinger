using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public class TabControl : Selector
{
    public static readonly DependencyProperty FontProperty =
        DependencyProperty.Register(
            nameof(Font),
            typeof(SpriteFont),
            typeof(TabControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color),
            typeof(TabControl),
            new FrameworkPropertyMetadata(Color.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty =
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

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(TabControl),
            new FrameworkPropertyMetadata(new Color(95, 95, 95), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(TabControl),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float f && f >= 0f ? f : 0f));

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
        Focusable = true;
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

        if (Items.Count > 0 && SelectedIndex < 0)
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
            selectedElement.Measure(new Vector2(availableSize.X, MathF.Max(0f, availableSize.Y - headerHeight - BorderThickness)));
            desired.X = MathF.Max(desired.X, selectedElement.DesiredSize.X);
            desired.Y = MathF.Max(desired.Y, headerHeight + selectedElement.DesiredSize.Y + BorderThickness);
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
            selectedElement.Arrange(new LayoutRect(
                LayoutSlot.X,
                LayoutSlot.Y + headerHeight,
                finalSize.X,
                MathF.Max(0f, finalSize.Y - headerHeight)));
        }

        return finalSize;
    }

    protected override void OnRender(SpriteBatch spriteBatch)
    {
        var slot = LayoutSlot;
        UiDrawing.DrawFilledRect(spriteBatch, slot, Background, Opacity);

        foreach (var header in _headerSlots)
        {
            var color = header.Index == SelectedIndex
                ? SelectedHeaderBackground
                : HeaderBackground;
            UiDrawing.DrawFilledRect(spriteBatch, header.Rect, color, Opacity);
            UiDrawing.DrawRectStroke(spriteBatch, header.Rect, 1f, BorderBrush, Opacity);

            if (Font == null)
            {
                continue;
            }

            var text = header.Item.Header;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var textWidth = FontStashTextRenderer.MeasureWidth(Font, text);
            var x = header.Rect.X + ((header.Rect.Width - textWidth) / 2f);
            var y = header.Rect.Y + ((header.Rect.Height - FontStashTextRenderer.GetLineHeight(Font)) / 2f);
            FontStashTextRenderer.DrawString(spriteBatch, Font, text, new Vector2(x, y), Foreground * Opacity);
        }

        if (BorderThickness > 0f)
        {
            UiDrawing.DrawRectStroke(spriteBatch, slot, BorderThickness, BorderBrush, Opacity);
        }
    }

    protected override void OnMouseLeftButtonDown(RoutedMouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonDown(args);

        for (var i = 0; i < _headerSlots.Count; i++)
        {
            var header = _headerSlots[i];
            if (!Contains(header.Rect, args.Position))
            {
                continue;
            }

            SetSelectedIndexInternal(header.Index);
            Focus();
            args.Handled = true;
            return;
        }
    }

    protected override void OnKeyDown(RoutedKeyEventArgs args)
    {
        base.OnKeyDown(args);

        if (!IsEnabled || Items.Count == 0)
        {
            return;
        }

        var handled = false;
        if (args.Key == Keys.Left)
        {
            SetSelectedIndexInternal(Math.Max(0, SelectedIndex - 1));
            handled = true;
        }
        else if (args.Key == Keys.Right)
        {
            var next = SelectedIndex < 0 ? 0 : SelectedIndex + 1;
            SetSelectedIndexInternal(Math.Min(Items.Count - 1, next));
            handled = true;
        }
        else if (args.Key == Keys.Home)
        {
            SetSelectedIndexInternal(0);
            handled = true;
        }
        else if (args.Key == Keys.End)
        {
            SetSelectedIndexInternal(Items.Count - 1);
            handled = true;
        }

        if (handled)
        {
            args.Handled = true;
        }
    }

    private float GetHeaderHeight()
    {
        var padding = HeaderPadding;
        var textHeight = FontStashTextRenderer.GetLineHeight(Font);
        return padding.Vertical + textHeight;
    }

    private float MeasureHeaderWidth(TabItem item)
    {
        var padding = HeaderPadding;
        var textWidth = 26f;
        if (Font != null && !string.IsNullOrEmpty(item.Header))
        {
            textWidth = FontStashTextRenderer.MeasureWidth(Font, item.Header);
        }

        return MathF.Max(36f, padding.Horizontal + textWidth);
    }

    private static bool Contains(LayoutRect rect, Vector2 point)
    {
        return point.X >= rect.X && point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y && point.Y <= rect.Y + rect.Height;
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
