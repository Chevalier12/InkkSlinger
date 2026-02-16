using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public class StatusBar : ItemsControl
{
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(
            nameof(Background),
            typeof(Color),
            typeof(StatusBar),
            new FrameworkPropertyMetadata(new Color(18, 27, 40), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(
            nameof(BorderBrush),
            typeof(Color),
            typeof(StatusBar),
            new FrameworkPropertyMetadata(new Color(61, 89, 121), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BorderThicknessProperty =
        DependencyProperty.Register(
            nameof(BorderThickness),
            typeof(float),
            typeof(StatusBar),
            new FrameworkPropertyMetadata(
                1f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(
            nameof(Padding),
            typeof(Thickness),
            typeof(StatusBar),
            new FrameworkPropertyMetadata(
                new Thickness(6f, 2f, 6f, 2f),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemSpacingProperty =
        DependencyProperty.Register(
            nameof(ItemSpacing),
            typeof(float),
            typeof(StatusBar),
            new FrameworkPropertyMetadata(
                6f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is StatusBarItem;
    }

    protected override UIElement CreateContainerForItemOverride(object item)
    {
        return new StatusBarItem
        {
            Content = item
        };
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

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var padding = Padding;
        var border = BorderThickness;
        var innerWidth = MathF.Max(0f, availableSize.X - (border * 2f) - padding.Horizontal);
        var innerHeight = MathF.Max(0f, availableSize.Y - (border * 2f) - padding.Vertical);

        var leftWidth = 0f;
        var rightWidth = 0f;
        var maxHeight = 0f;
        var leftCount = 0;
        var rightCount = 0;

        foreach (var item in GetStatusItems())
        {
            item.Measure(new Vector2(innerWidth, innerHeight));
            maxHeight = MathF.Max(maxHeight, item.DesiredSize.Y);

            if (item.HorizontalContentAlignment == HorizontalAlignment.Right)
            {
                if (rightCount > 0)
                {
                    rightWidth += ItemSpacing;
                }

                rightWidth += item.DesiredSize.X;
                rightCount++;
            }
            else
            {
                if (leftCount > 0)
                {
                    leftWidth += ItemSpacing;
                }

                leftWidth += item.DesiredSize.X;
                leftCount++;
            }
        }

        var totalWidth = leftWidth + rightWidth;
        if (leftCount > 0 && rightCount > 0)
        {
            totalWidth += ItemSpacing;
        }

        return new Vector2(
            totalWidth + (border * 2f) + padding.Horizontal,
            maxHeight + (border * 2f) + padding.Vertical);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var items = GetStatusItems();
        var padding = Padding;
        var border = BorderThickness;
        var contentX = LayoutSlot.X + border + padding.Left;
        var contentY = LayoutSlot.Y + border + padding.Top;
        var contentWidth = MathF.Max(0f, finalSize.X - (border * 2f) - padding.Horizontal);
        var contentHeight = MathF.Max(0f, finalSize.Y - (border * 2f) - padding.Vertical);

        var rightStart = contentX + contentWidth;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (item.HorizontalContentAlignment != HorizontalAlignment.Right)
            {
                continue;
            }

            rightStart -= item.DesiredSize.X;
            item.Arrange(new LayoutRect(rightStart, contentY, item.DesiredSize.X, contentHeight));
            rightStart -= ItemSpacing;
        }

        var leftX = contentX;
        foreach (var item in items)
        {
            if (item.HorizontalContentAlignment == HorizontalAlignment.Right)
            {
                continue;
            }

            var width = item.DesiredSize.X;
            item.Arrange(new LayoutRect(leftX, contentY, width, contentHeight));
            leftX += width + ItemSpacing;
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

    private IReadOnlyList<StatusBarItem> GetStatusItems()
    {
        var result = new List<StatusBarItem>();
        foreach (var child in ItemContainers)
        {
            if (child is StatusBarItem item)
            {
                result.Add(item);
            }
        }

        return result;
    }
}
