using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class ToolBarTray : Panel
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(ToolBarTray),
            new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty BandSpacingProperty =
        DependencyProperty.Register(
            nameof(BandSpacing),
            typeof(float),
            typeof(ToolBarTray),
            new FrameworkPropertyMetadata(
                2f,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                coerceValueCallback: static (_, value) => value is float v && v >= 0f ? v : 0f));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public float BandSpacing
    {
        get => GetValue<float>(BandSpacingProperty);
        set => SetValue(BandSpacingProperty, value);
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
            if (Orientation == Orientation.Vertical)
            {
                if (!first)
                {
                    desired.Y += BandSpacing;
                }

                desired.Y += childSize.Y;
                desired.X = MathF.Max(desired.X, childSize.X);
            }
            else
            {
                if (!first)
                {
                    desired.X += BandSpacing;
                }

                desired.X += childSize.X;
                desired.Y = MathF.Max(desired.Y, childSize.Y);
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
                if (Orientation == Orientation.Vertical)
                {
                    y += BandSpacing;
                }
                else
                {
                    x += BandSpacing;
                }
            }

            if (Orientation == Orientation.Vertical)
            {
                var height = element.DesiredSize.Y;
                element.Arrange(new LayoutRect(x, y, finalSize.X, height));
                y += height;
            }
            else
            {
                var width = element.DesiredSize.X;
                element.Arrange(new LayoutRect(x, y, width, finalSize.Y));
                x += width;
            }

            first = false;
        }

        return finalSize;
    }
}
