using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class StackPanel : Panel
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(
            nameof(Orientation),
            typeof(Orientation),
            typeof(StackPanel),
            new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var desired = Vector2.Zero;
        var childAvailable = Orientation == Orientation.Vertical
            ? new Vector2(availableSize.X, float.PositiveInfinity)
            : new Vector2(float.PositiveInfinity, availableSize.Y);

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            frameworkChild.Measure(childAvailable);
            var childDesired = frameworkChild.DesiredSize;
            if (Orientation == Orientation.Vertical)
            {
                desired.X = System.MathF.Max(desired.X, childDesired.X);
                desired.Y += childDesired.Y;
                continue;
            }

            desired.X += childDesired.X;
            desired.Y = System.MathF.Max(desired.Y, childDesired.Y);
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var currentX = LayoutSlot.X;
        var currentY = LayoutSlot.Y;

        foreach (var child in Children)
        {
            if (child is not FrameworkElement frameworkChild)
            {
                continue;
            }

            if (Orientation == Orientation.Vertical)
            {
                var height = frameworkChild.DesiredSize.Y;
                frameworkChild.Arrange(new LayoutRect(LayoutSlot.X, currentY, finalSize.X, height));
                currentY += height;
                continue;
            }

            var width = frameworkChild.DesiredSize.X;
            frameworkChild.Arrange(new LayoutRect(currentX, LayoutSlot.Y, width, finalSize.Y));
            currentX += width;
        }

        return finalSize;
    }
}
