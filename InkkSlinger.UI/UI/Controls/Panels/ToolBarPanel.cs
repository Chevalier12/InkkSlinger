using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class ToolBarPanel : Panel
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(ToolBarPanel),
            new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemSpacingProperty =
        DependencyProperty.Register(
            nameof(ItemSpacing),
            typeof(float),
            typeof(ToolBarPanel),
            new FrameworkPropertyMetadata(
                2f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public float ItemSpacing
    {
        get => GetValue<float>(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = Vector2.Zero;
        var first = true;

        foreach (var child in Children)
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            element.Measure(availableSize);
            var childSize = element.DesiredSize;
            if (Orientation == Orientation.Horizontal)
            {
                if (!first)
                {
                    desired.X += ItemSpacing;
                }

                desired.X += childSize.X;
                desired.Y = MathF.Max(desired.Y, childSize.Y);
            }
            else
            {
                if (!first)
                {
                    desired.Y += ItemSpacing;
                }

                desired.Y += childSize.Y;
                desired.X = MathF.Max(desired.X, childSize.X);
            }

            first = false;
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var x = LayoutSlot.X;
        var y = LayoutSlot.Y;
        var first = true;

        foreach (var child in Children)
        {
            if (child is not FrameworkElement element)
            {
                continue;
            }

            if (!first)
            {
                if (Orientation == Orientation.Horizontal)
                {
                    x += ItemSpacing;
                }
                else
                {
                    y += ItemSpacing;
                }
            }

            if (Orientation == Orientation.Horizontal)
            {
                var width = element.DesiredSize.X;
                element.Arrange(new LayoutRect(x, y, width, finalSize.Y));
                x += width;
            }
            else
            {
                var height = element.DesiredSize.Y;
                element.Arrange(new LayoutRect(x, y, finalSize.X, height));
                y += height;
            }

            first = false;
        }

        return finalSize;
    }
}
