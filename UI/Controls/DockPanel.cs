using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class DockPanel : Panel
{
    public static readonly DependencyProperty DockProperty =
        DependencyProperty.RegisterAttached(
            "Dock",
            typeof(Dock),
            typeof(DockPanel),
            new FrameworkPropertyMetadata(
                Dock.Left,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange,
                static (dependencyObject, _) =>
                {
                    if (dependencyObject is UIElement element && element.VisualParent is DockPanel panel)
                    {
                        panel.InvalidateMeasure();
                    }
                }));

    public static readonly DependencyProperty LastChildFillProperty =
        DependencyProperty.Register(
            nameof(LastChildFill),
            typeof(bool),
            typeof(DockPanel),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public static Dock GetDock(UIElement element)
    {
        return element.GetValue<Dock>(DockProperty);
    }

    public static void SetDock(UIElement element, Dock value)
    {
        element.SetValue(DockProperty, value);
    }

    public bool LastChildFill
    {
        get => GetValue<bool>(LastChildFillProperty);
        set => SetValue(LastChildFillProperty, value);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var usedLeft = 0f;
        var usedTop = 0f;
        var usedRight = 0f;
        var usedBottom = 0f;
        var desired = Vector2.Zero;

        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not FrameworkElement child)
            {
                continue;
            }

            var isFillChild = LastChildFill && i == Children.Count - 1;
            var remainingWidth = MathF.Max(0f, availableSize.X - usedLeft - usedRight);
            var remainingHeight = MathF.Max(0f, availableSize.Y - usedTop - usedBottom);

            child.Measure(new Vector2(remainingWidth, remainingHeight));
            var childDesired = child.DesiredSize;

            if (isFillChild)
            {
                desired.X = MathF.Max(desired.X, usedLeft + usedRight + childDesired.X);
                desired.Y = MathF.Max(desired.Y, usedTop + usedBottom + childDesired.Y);
                continue;
            }

            switch (GetDock(child))
            {
                case Dock.Left:
                case Dock.Right:
                    usedLeft += GetDock(child) == Dock.Left ? childDesired.X : 0f;
                    usedRight += GetDock(child) == Dock.Right ? childDesired.X : 0f;
                    desired.X = MathF.Max(desired.X, usedLeft + usedRight);
                    desired.Y = MathF.Max(desired.Y, usedTop + usedBottom + childDesired.Y);
                    break;
                case Dock.Top:
                case Dock.Bottom:
                    usedTop += GetDock(child) == Dock.Top ? childDesired.Y : 0f;
                    usedBottom += GetDock(child) == Dock.Bottom ? childDesired.Y : 0f;
                    desired.X = MathF.Max(desired.X, usedLeft + usedRight + childDesired.X);
                    desired.Y = MathF.Max(desired.Y, usedTop + usedBottom);
                    break;
            }
        }

        return desired;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var left = LayoutSlot.X;
        var top = LayoutSlot.Y;
        var right = LayoutSlot.X + finalSize.X;
        var bottom = LayoutSlot.Y + finalSize.Y;

        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not FrameworkElement child)
            {
                continue;
            }

            var isFillChild = LastChildFill && i == Children.Count - 1;
            if (isFillChild)
            {
                child.Arrange(new LayoutRect(left, top, MathF.Max(0f, right - left), MathF.Max(0f, bottom - top)));
                continue;
            }

            var dock = GetDock(child);
            switch (dock)
            {
                case Dock.Left:
                {
                    var width = MathF.Min(child.DesiredSize.X, MathF.Max(0f, right - left));
                    child.Arrange(new LayoutRect(left, top, width, MathF.Max(0f, bottom - top)));
                    left += width;
                    break;
                }
                case Dock.Right:
                {
                    var width = MathF.Min(child.DesiredSize.X, MathF.Max(0f, right - left));
                    child.Arrange(new LayoutRect(right - width, top, width, MathF.Max(0f, bottom - top)));
                    right -= width;
                    break;
                }
                case Dock.Top:
                {
                    var height = MathF.Min(child.DesiredSize.Y, MathF.Max(0f, bottom - top));
                    child.Arrange(new LayoutRect(left, top, MathF.Max(0f, right - left), height));
                    top += height;
                    break;
                }
                case Dock.Bottom:
                {
                    var height = MathF.Min(child.DesiredSize.Y, MathF.Max(0f, bottom - top));
                    child.Arrange(new LayoutRect(left, bottom - height, MathF.Max(0f, right - left), height));
                    bottom -= height;
                    break;
                }
            }
        }

        return finalSize;
    }
}
