using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

internal sealed class ScrollViewerContentPresenter
{
    private readonly ScrollViewer _owner;
    private LayoutRect _lastArrangedContentRect;
    private LayoutRect _lastArrangedViewportRect;
    private FrameworkElement? _lastArrangedContentElement;
    private bool _hasArrangedContentRect;

    public ScrollViewerContentPresenter(ScrollViewer owner)
    {
        _owner = owner;
    }

    public void ArrangeForCurrentOffsets(LayoutRect previousViewportRect)
    {
        var startTicks = Stopwatch.GetTimestamp();
        ScrollViewer._diagArrangeContentForCurrentOffsetsCallCount++;
        _owner._runtimeArrangeContentForCurrentOffsetsCallCount++;
        HookupScrollInfo();
        if (_owner.ContentElementForScrollPresenter is not { } content)
        {
            ClearArrangeCache();
            ScrollViewer._diagArrangeContentSkippedNoContentCount++;
            _owner._runtimeArrangeContentSkippedNoContentCount++;
            RecordArrangeElapsed(startTicks);
            return;
        }

        if (_owner._contentViewportRect.Width <= 0f || _owner._contentViewportRect.Height <= 0f)
        {
            ClearArrangeCache();
            ScrollViewer._diagArrangeContentSkippedZeroViewportCount++;
            _owner._runtimeArrangeContentSkippedZeroViewportCount++;
            RecordArrangeElapsed(startTicks);
            return;
        }

        var arrangedWidth = ResolveArrangeWidth(content, previousViewportRect);
        var arrangedHeight = ResolveArrangeHeight(content, previousViewportRect);
        var useTransformScrolling = UsesTransformBasedScrolling();
        if (useTransformScrolling)
        {
            ScrollViewer._diagArrangeContentTransformPathCount++;
            _owner._runtimeArrangeContentTransformPathCount++;
        }
        else
        {
            ScrollViewer._diagArrangeContentOffsetPathCount++;
            _owner._runtimeArrangeContentOffsetPathCount++;
        }

        var contentOwnsHorizontalOffset = ShouldArrangeVirtualizedContentToViewport(content, horizontalAxis: true);
        var contentOwnsVerticalOffset = ShouldArrangeVirtualizedContentToViewport(content, horizontalAxis: false);
        var contentX = useTransformScrolling || contentOwnsHorizontalOffset
            ? _owner._contentViewportRect.X
            : _owner._contentViewportRect.X - _owner.HorizontalOffset;
        var contentY = useTransformScrolling || contentOwnsVerticalOffset
            ? _owner._contentViewportRect.Y
            : _owner._contentViewportRect.Y - _owner.VerticalOffset;

        var arrangeRect = new LayoutRect(
            contentX,
            contentY,
            arrangedWidth,
            arrangedHeight);

        if (CanReuseExistingArrange(content, arrangeRect))
        {
            CacheArrange(content, arrangeRect);
            RecordArrangeElapsed(startTicks);
            return;
        }

        if (CanTranslateExistingArrange(content, arrangeRect) &&
            content.TryTranslateArrangedSubtree(arrangeRect))
        {
            CacheArrange(content, arrangeRect);
            content.InvalidateLayoutBoundsMetadata();
            RecordArrangeElapsed(startTicks);
            return;
        }

        content.Arrange(arrangeRect);
        CacheArrange(content, arrangeRect);
        if (content.CanRetainRenderContentDuringLayoutMetadataUpdate)
        {
            content.InvalidateLayoutBoundsMetadata();
        }
        else
        {
            content.InvalidateVisual();
        }
        RecordArrangeElapsed(startTicks);
    }

    public bool CanReuseExistingArrange(FrameworkElement content, LayoutRect nextArrangeRect)
    {
        if (content.NeedsMeasure || content.NeedsArrange)
        {
            return false;
        }

        if (_hasArrangedContentRect &&
            ReferenceEquals(_lastArrangedContentElement, content) &&
            ScrollViewer.AreLayoutRectsClose(_lastArrangedContentRect, nextArrangeRect))
        {
            return true;
        }

        return content.IsMeasureValidForTests &&
               content.IsArrangeValidForTests &&
               ScrollViewer.AreLayoutRectsClose(content.LayoutSlot, nextArrangeRect);
    }

    public float ResolveArrangeWidth(FrameworkElement content, LayoutRect previousViewportRect)
    {
        if (ShouldArrangeVirtualizedContentToViewport(content, horizontalAxis: true))
        {
            return _owner.ViewportWidth;
        }

        var arrangedWidth = _owner.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled
            ? _owner.ViewportWidth
            : MathF.Max(_owner.ViewportWidth, _owner.ExtentWidth);

        if (_owner.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)
        {
            return arrangedWidth;
        }

        return TryPreservePreviousArrangeSpan(content, previousViewportRect, horizontalAxis: true, out var preservedWidth)
            ? preservedWidth
            : arrangedWidth;
    }

    public float ResolveArrangeHeight(FrameworkElement content, LayoutRect previousViewportRect)
    {
        if (ShouldArrangeVirtualizedContentToViewport(content, horizontalAxis: false))
        {
            return _owner.ViewportHeight;
        }

        var arrangedHeight = MathF.Max(_owner.ViewportHeight, _owner.ExtentHeight);

        if (_owner.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
        {
            return arrangedHeight;
        }

        return TryPreservePreviousArrangeSpan(content, previousViewportRect, horizontalAxis: false, out var preservedHeight)
            ? preservedHeight
            : arrangedHeight;
    }

    public Vector2 CreateMeasureConstraint(float viewportWidth, float viewportHeight)
    {
        var canScrollHorizontally = _owner.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        var canScrollVertically = _owner.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
        if (_owner.ContentElementForScrollPresenter is IScrollViewerMeasureConstraintProvider constraintProvider)
        {
            return constraintProvider.GetScrollViewerMeasureConstraint(
                viewportWidth,
                viewportHeight,
                canScrollHorizontally,
                canScrollVertically);
        }

        return new Vector2(
            canScrollHorizontally ? float.PositiveInfinity : MathF.Max(0f, viewportWidth),
            canScrollVertically ? float.PositiveInfinity : MathF.Max(0f, viewportHeight));
    }

    public void MeasureContent(float viewportWidth, float viewportHeight, out float extentWidth, out float extentHeight)
    {
        var startTicks = Stopwatch.GetTimestamp();

        if (_owner.ContentElementForScrollPresenter is { } content)
        {
            content.Measure(CreateMeasureConstraint(viewportWidth, viewportHeight));
        }

        if (GetLogicalScrollInfo() is { } scrollInfo)
        {
            extentWidth = scrollInfo.ExtentWidth;
            extentHeight = scrollInfo.ExtentHeight;
        }
        else
        {
            extentWidth = _owner.ContentElementForScrollPresenter?.DesiredSize.X ?? 0f;
            extentHeight = _owner.ContentElementForScrollPresenter?.DesiredSize.Y ?? 0f;
        }

        ApplyTransformContentMetrics(ref extentWidth, ref extentHeight);
        ScrollViewer._diagMeasureContentCallCount++;
        ScrollViewer._diagMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
        _owner._runtimeMeasureContentCallCount++;
        _owner._runtimeMeasureContentElapsedTicks += Stopwatch.GetTimestamp() - startTicks;
    }

    public bool TryReuseCurrentMeasureForViewport(float viewportWidth, float viewportHeight, out float extentWidth, out float extentHeight)
    {
        if (!_owner._hasPreviousScrollBarResolution)
        {
            extentWidth = 0f;
            extentHeight = 0f;
            return false;
        }

        return TryReuseMeasure(
            _owner._contentViewportRect.Width,
            _owner._contentViewportRect.Height,
            viewportWidth,
            viewportHeight,
            out extentWidth,
            out extentHeight);
    }

    public bool TryReuseMeasure(
        float previousViewportWidth,
        float previousViewportHeight,
        float nextViewportWidth,
        float nextViewportHeight,
        out float extentWidth,
        out float extentHeight)
    {
        if (_owner.ContentElementForScrollPresenter is not { } content)
        {
            extentWidth = 0f;
            extentHeight = 0f;
            return false;
        }

        if (content.HasPendingMeasureInvalidationInVisualSubtreeForLayout())
        {
            extentWidth = 0f;
            extentHeight = 0f;
            return false;
        }

        var previousConstraint = CreateMeasureConstraint(previousViewportWidth, previousViewportHeight);
        var nextConstraint = CreateMeasureConstraint(nextViewportWidth, nextViewportHeight);
        if (!content.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousConstraint, nextConstraint))
        {
            extentWidth = 0f;
            extentHeight = 0f;
            return false;
        }

        extentWidth = content.DesiredSize.X;
        extentHeight = content.DesiredSize.Y;
        ApplyTransformContentMetrics(ref extentWidth, ref extentHeight);
        return true;
    }

    public bool UsesTransformBasedScrolling()
    {
        return _owner.ContentElementForScrollPresenter is UIElement contentElement &&
               contentElement is IScrollTransformContent &&
               ScrollViewer.GetUseTransformContentScrolling(contentElement);
    }

    public bool TryGetTransformContentExtent(out float extentWidth, out float extentHeight)
    {
        extentWidth = 0f;
        extentHeight = 0f;
        if (!UsesTransformBasedScrolling() ||
            _owner.ContentElementForScrollPresenter is not IScrollTransformContent transformContent ||
            !transformContent.TryGetScrollTransformContentMetrics(out var metrics))
        {
            return false;
        }

        ApplyTransformContentMetrics(metrics, ref extentWidth, ref extentHeight);
        return true;
    }

    public IScrollInfo? GetLogicalScrollInfo()
    {
        return _owner.CanContentScroll && _owner.ContentElementForScrollPresenter is IScrollInfo scrollInfo
            ? scrollInfo
            : null;
    }

    public void HookupScrollInfo()
    {
        if (_owner.ContentElementForScrollPresenter is IScrollInfo scrollInfo)
        {
            scrollInfo.ScrollOwner = _owner.CanContentScroll ? _owner : null;
        }
    }

    public void OnOwnerTranslated(Vector2 delta)
    {
        if (_hasArrangedContentRect)
        {
            _lastArrangedContentRect = ScrollViewer.OffsetCachedRect(_lastArrangedContentRect, delta);
        }
    }

    private bool CanTranslateExistingArrange(FrameworkElement content, LayoutRect nextArrangeRect)
    {
        return UsesTransformBasedScrolling() &&
               _hasArrangedContentRect &&
               ReferenceEquals(_lastArrangedContentElement, content) &&
               ScrollViewer.AreFloatsClose(_lastArrangedContentRect.Width, nextArrangeRect.Width) &&
               ScrollViewer.AreFloatsClose(_lastArrangedContentRect.Height, nextArrangeRect.Height) &&
               (!ScrollViewer.AreFloatsClose(_lastArrangedContentRect.X, nextArrangeRect.X) ||
                !ScrollViewer.AreFloatsClose(_lastArrangedContentRect.Y, nextArrangeRect.Y));
    }

    private bool TryPreservePreviousArrangeSpan(
        FrameworkElement content,
        LayoutRect previousViewportRect,
        bool horizontalAxis,
        out float preservedSpan)
    {
        preservedSpan = 0f;

        if (!_hasArrangedContentRect ||
            !ReferenceEquals(_lastArrangedContentElement, content) ||
            content.NeedsMeasure)
        {
            return false;
        }

        var previousViewportSpan = horizontalAxis ? _lastArrangedViewportRect.Width : _lastArrangedViewportRect.Height;
        var nextViewportSpan = horizontalAxis ? _owner._contentViewportRect.Width : _owner._contentViewportRect.Height;
        var previousArrangedSpan = horizontalAxis ? _lastArrangedContentRect.Width : _lastArrangedContentRect.Height;
        if (previousArrangedSpan <= nextViewportSpan + 0.01f ||
            previousViewportSpan <= 0f ||
            nextViewportSpan <= 0f)
        {
            return false;
        }

        var previousAvailable = CreateMeasureConstraint(_lastArrangedViewportRect.Width, _lastArrangedViewportRect.Height);
        var nextAvailable = CreateMeasureConstraint(_owner._contentViewportRect.Width, _owner._contentViewportRect.Height);
        if (!content.CanReuseMeasureForAvailableSizeChangeForParentLayout(previousAvailable, nextAvailable))
        {
            return false;
        }

        preservedSpan = previousArrangedSpan;
        return true;
    }

    private static bool ShouldArrangeVirtualizedContentToViewport(FrameworkElement content, bool horizontalAxis)
    {
        if (content is IScrollViewerVirtualizedContent virtualizedContent)
        {
            return horizontalAxis
                ? virtualizedContent.OwnsHorizontalScrollOffset
                : virtualizedContent.OwnsVerticalScrollOffset;
        }

        if (content is not VirtualizingStackPanel panel)
        {
            return false;
        }

        return panel.IsVirtualizing &&
               (horizontalAxis
                   ? panel.Orientation == Orientation.Horizontal
                   : panel.Orientation == Orientation.Vertical);
    }

    private void ApplyTransformContentMetrics(ref float extentWidth, ref float extentHeight)
    {
        if (!UsesTransformBasedScrolling() ||
            _owner.ContentElementForScrollPresenter is not IScrollTransformContent transformContent ||
            !transformContent.TryGetScrollTransformContentMetrics(out var metrics))
        {
            return;
        }

        ApplyTransformContentMetrics(metrics, ref extentWidth, ref extentHeight);
    }

    private static void ApplyTransformContentMetrics(ScrollTransformContentMetrics metrics, ref float extentWidth, ref float extentHeight)
    {
        var logicalWidth = CoerceNonNegativeFinite(metrics.LogicalExtent.X, extentWidth);
        var logicalHeight = CoerceNonNegativeFinite(metrics.LogicalExtent.Y, extentHeight);
        var scaleX = CoerceFinite(metrics.Scale.X, 1f);
        var scaleY = CoerceFinite(metrics.Scale.Y, 1f);
        var offsetX = CoerceFinite(metrics.Offset.X, 0f);
        var offsetY = CoerceFinite(metrics.Offset.Y, 0f);
        extentWidth = ResolveTransformedExtent(logicalWidth, scaleX, offsetX);
        extentHeight = ResolveTransformedExtent(logicalHeight, scaleY, offsetY);
    }

    private static float ResolveTransformedExtent(float logicalExtent, float scale, float offset)
    {
        var transformedEnd = offset + (logicalExtent * scale);
        var min = MathF.Min(0f, MathF.Min(offset, transformedEnd));
        var max = MathF.Max(0f, MathF.Max(offset, transformedEnd));
        return MathF.Max(0f, max - min);
    }

    private static float CoerceFinite(float candidate, float fallback)
    {
        return float.IsFinite(candidate) ? candidate : fallback;
    }

    private static float CoerceNonNegativeFinite(float candidate, float fallback)
    {
        if (float.IsFinite(candidate) && candidate >= 0f)
        {
            return candidate;
        }

        return float.IsFinite(fallback) && fallback >= 0f
            ? fallback
            : 0f;
    }

    private void CacheArrange(FrameworkElement content, LayoutRect arrangeRect)
    {
        _hasArrangedContentRect = true;
        _lastArrangedContentRect = arrangeRect;
        _lastArrangedViewportRect = _owner._contentViewportRect;
        _lastArrangedContentElement = content;
    }

    private void ClearArrangeCache()
    {
        _hasArrangedContentRect = false;
        _lastArrangedContentElement = null;
    }

    private void RecordArrangeElapsed(long startTicks)
    {
        var elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
        ScrollViewer._diagArrangeContentForCurrentOffsetsElapsedTicks += elapsedTicks;
        _owner._runtimeArrangeContentForCurrentOffsetsElapsedTicks += elapsedTicks;
    }
}
