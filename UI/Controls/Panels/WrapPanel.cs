using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class WrapPanel : Panel
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(WrapPanel),
            new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(
            nameof(ItemWidth),
            typeof(float),
            typeof(WrapPanel),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(
            nameof(ItemHeight),
            typeof(float),
            typeof(WrapPanel),
            new FrameworkPropertyMetadata(float.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public float ItemWidth
    {
        get => GetValue<float>(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public float ItemHeight
    {
        get => GetValue<float>(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var lineLimit = horizontal ? availableSize.X : availableSize.Y;
        if (float.IsInfinity(lineLimit) || float.IsNaN(lineLimit) || lineLimit <= 0f)
        {
            lineLimit = float.PositiveInfinity;
        }

        var lineMain = 0f;
        var lineCross = 0f;
        var desiredMain = 0f;
        var desiredCross = 0f;

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            var itemAvailable = new Vector2(
                float.IsNaN(ItemWidth) ? availableSize.X : ItemWidth,
                float.IsNaN(ItemHeight) ? availableSize.Y : ItemHeight);
            frameworkChild.Measure(itemAvailable);

            var childSize = GetChildSize(frameworkChild);
            var childMain = horizontal ? childSize.X : childSize.Y;
            var childCross = horizontal ? childSize.Y : childSize.X;

            if (lineMain > 0f && lineMain + childMain > lineLimit)
            {
                desiredMain = MathF.Max(desiredMain, lineMain);
                desiredCross += lineCross;
                lineMain = 0f;
                lineCross = 0f;
            }

            lineMain += childMain;
            lineCross = MathF.Max(lineCross, childCross);
        }

        desiredMain = MathF.Max(desiredMain, lineMain);
        desiredCross += lineCross;

        return horizontal
            ? new Vector2(desiredMain, desiredCross)
            : new Vector2(desiredCross, desiredMain);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var lineLimit = horizontal ? finalSize.X : finalSize.Y;

        var lineMain = 0f;
        var lineCross = 0f;
        var crossOffset = 0f;

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            var childSize = GetChildSize(frameworkChild);
            var childMain = horizontal ? childSize.X : childSize.Y;
            var childCross = horizontal ? childSize.Y : childSize.X;

            if (lineMain > 0f && lineMain + childMain > lineLimit)
            {
                lineMain = 0f;
                crossOffset += lineCross;
                lineCross = 0f;
            }

            var x = LayoutSlot.X + (horizontal ? lineMain : crossOffset);
            var y = LayoutSlot.Y + (horizontal ? crossOffset : lineMain);
            var width = horizontal ? childMain : childCross;
            var height = horizontal ? childCross : childMain;

            frameworkChild.Arrange(new LayoutRect(x, y, width, height));
            lineMain += childMain;
            lineCross = MathF.Max(lineCross, childCross);
        }

        return finalSize;
    }

    private Vector2 GetChildSize(FrameworkElement element)
    {
        var width = float.IsNaN(ItemWidth) ? element.DesiredSize.X : ItemWidth;
        var height = float.IsNaN(ItemHeight) ? element.DesiredSize.Y : ItemHeight;
        return new Vector2(width, height);
    }
}
