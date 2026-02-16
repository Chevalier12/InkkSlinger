using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class Canvas : Panel
{
    public static readonly DependencyProperty LeftProperty =
        DependencyProperty.RegisterAttached(
            "Left",
            typeof(float),
            typeof(Canvas),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty TopProperty =
        DependencyProperty.RegisterAttached(
            "Top",
            typeof(float),
            typeof(Canvas),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static float GetLeft(UIElement element)
    {
        return element.GetValue<float>(LeftProperty);
    }

    public static void SetLeft(UIElement element, float value)
    {
        element.SetValue(LeftProperty, value);
    }

    public static float GetTop(UIElement element)
    {
        return element.GetValue<float>(TopProperty);
    }

    public static void SetTop(UIElement element, float value)
    {
        element.SetValue(TopProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = Vector2.Zero;

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            frameworkChild.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));

            var left = GetLeft(frameworkChild);
            var top = GetTop(frameworkChild);
            if (float.IsNaN(left))
            {
                left = 0f;
            }

            if (float.IsNaN(top))
            {
                top = 0f;
            }

            desired.X = MathF.Max(desired.X, left + frameworkChild.DesiredSize.X);
            desired.Y = MathF.Max(desired.Y, top + frameworkChild.DesiredSize.Y);
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            var left = GetLeft(frameworkChild);
            var top = GetTop(frameworkChild);
            if (float.IsNaN(left))
            {
                left = 0f;
            }

            if (float.IsNaN(top))
            {
                top = 0f;
            }

            frameworkChild.Arrange(new LayoutRect(
                LayoutSlot.X + left,
                LayoutSlot.Y + top,
                frameworkChild.DesiredSize.X,
                frameworkChild.DesiredSize.Y));
        }

        return finalSize;
    }

    protected override bool TryGetClipRect(out LayoutRect clipRect)
    {
        clipRect = LayoutSlot;
        return true;
    }
}
