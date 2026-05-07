using System;

namespace InkkSlinger;

public partial class ScrollViewer
{
    private void UpdateDerivedScrollState()
    {
        SetIfChanged(ScrollableWidthProperty, MathF.Max(0f, ExtentWidth - ViewportWidth));
        SetIfChanged(ScrollableHeightProperty, MathF.Max(0f, ExtentHeight - ViewportHeight));
        SetIfChanged(
            ComputedHorizontalScrollBarVisibilityProperty,
            _showHorizontalBar ? Visibility.Visible : Visibility.Collapsed);
        SetIfChanged(
            ComputedVerticalScrollBarVisibilityProperty,
            _showVerticalBar ? Visibility.Visible : Visibility.Collapsed);
        UpdateContentOffsetsFromLiveOffsets();
    }

    private void UpdateContentOffsetsFromLiveOffsets()
    {
        if (IsDeferredScrollingEnabled &&
            (_horizontalBar.IsThumbDragInProgress || _verticalBar.IsThumbDragInProgress))
        {
            return;
        }

        SetIfChanged(ContentHorizontalOffsetProperty, HorizontalOffset);
        SetIfChanged(ContentVerticalOffsetProperty, VerticalOffset);
    }

    private void RaiseScrollChangedIfNeeded(
        float previousHorizontalOffset,
        float previousVerticalOffset,
        float previousExtentWidth,
        float previousExtentHeight,
        float previousViewportWidth,
        float previousViewportHeight,
        float previousScrollableWidth,
        float previousScrollableHeight)
    {
        var horizontalChange = HorizontalOffset - previousHorizontalOffset;
        var verticalChange = VerticalOffset - previousVerticalOffset;
        var extentWidthChange = ExtentWidth - previousExtentWidth;
        var extentHeightChange = ExtentHeight - previousExtentHeight;
        var viewportWidthChange = ViewportWidth - previousViewportWidth;
        var viewportHeightChange = ViewportHeight - previousViewportHeight;
        var scrollableWidthChange = ScrollableWidth - previousScrollableWidth;
        var scrollableHeightChange = ScrollableHeight - previousScrollableHeight;
        if (AreClose(horizontalChange, 0f) &&
            AreClose(verticalChange, 0f) &&
            AreClose(extentWidthChange, 0f) &&
            AreClose(extentHeightChange, 0f) &&
            AreClose(viewportWidthChange, 0f) &&
            AreClose(viewportHeightChange, 0f) &&
            AreClose(scrollableWidthChange, 0f) &&
            AreClose(scrollableHeightChange, 0f))
        {
            return;
        }

        RaiseRoutedEvent(
            ScrollChangedEvent,
            new ScrollChangedEventArgs(
                ScrollChangedEvent,
                HorizontalOffset,
                VerticalOffset,
                horizontalChange,
                verticalChange,
                ViewportWidth,
                ViewportHeight,
                viewportWidthChange,
                viewportHeightChange,
                ExtentWidth,
                ExtentHeight,
                extentWidthChange,
                extentHeightChange));
    }

    private void SetIfChanged(DependencyProperty property, Visibility value)
    {
        if (Equals(GetValue<Visibility>(property), value))
        {
            return;
        }

        SetValue(property, value);
    }
}
