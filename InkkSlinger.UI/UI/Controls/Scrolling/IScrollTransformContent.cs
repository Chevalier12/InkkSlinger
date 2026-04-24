using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal interface IScrollTransformContent
{
}

internal interface IScrollViewerMeasureConstraintProvider
{
    Vector2 GetScrollViewerMeasureConstraint(
        float viewportWidth,
        float viewportHeight,
        bool canScrollHorizontally,
        bool canScrollVertically);
}
