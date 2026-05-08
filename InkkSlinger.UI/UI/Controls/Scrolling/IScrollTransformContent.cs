using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal readonly record struct ScrollTransformContentMetrics(
    Vector2 LogicalExtent,
    Vector2 Scale,
    Vector2 Offset);

internal interface IScrollTransformContent
{
    bool TryGetScrollTransformContentMetrics(out ScrollTransformContentMetrics metrics)
    {
        metrics = default;
        return false;
    }
}

internal interface IScrollViewerMeasureConstraintProvider
{
    Vector2 GetScrollViewerMeasureConstraint(
        float viewportWidth,
        float viewportHeight,
        bool canScrollHorizontally,
        bool canScrollVertically);
}

internal interface IScrollViewerVirtualizedContent
{
    bool OwnsHorizontalScrollOffset { get; }

    bool OwnsVerticalScrollOffset { get; }
}

public interface IScrollInfo
{
    ScrollViewer? ScrollOwner { get; set; }

    float ExtentWidth { get; }

    float ExtentHeight { get; }

    float ViewportWidth { get; }

    float ViewportHeight { get; }

    float HorizontalOffset { get; }

    float VerticalOffset { get; }

    void LineUp();

    void LineDown();

    void LineLeft();

    void LineRight();

    void PageUp();

    void PageDown();

    void PageLeft();

    void PageRight();

    void MouseWheelUp();

    void MouseWheelDown();

    void MouseWheelLeft();

    void MouseWheelRight();

    void SetHorizontalOffset(float offset);

    void SetVerticalOffset(float offset);

    LayoutRect MakeVisible(UIElement visual, LayoutRect rectangle);
}
