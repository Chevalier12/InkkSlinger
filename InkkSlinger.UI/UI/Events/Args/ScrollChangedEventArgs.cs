namespace InkkSlinger;

public sealed class ScrollChangedEventArgs : RoutedEventArgs
{
    public ScrollChangedEventArgs(
        RoutedEvent routedEvent,
        float horizontalOffset,
        float verticalOffset,
        float horizontalChange,
        float verticalChange,
        float viewportWidth,
        float viewportHeight,
        float viewportWidthChange,
        float viewportHeightChange,
        float extentWidth,
        float extentHeight,
        float extentWidthChange,
        float extentHeightChange)
        : base(routedEvent)
    {
        HorizontalOffset = horizontalOffset;
        VerticalOffset = verticalOffset;
        HorizontalChange = horizontalChange;
        VerticalChange = verticalChange;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        ViewportWidthChange = viewportWidthChange;
        ViewportHeightChange = viewportHeightChange;
        ExtentWidth = extentWidth;
        ExtentHeight = extentHeight;
        ExtentWidthChange = extentWidthChange;
        ExtentHeightChange = extentHeightChange;
    }

    public float HorizontalOffset { get; }

    public float VerticalOffset { get; }

    public float HorizontalChange { get; }

    public float VerticalChange { get; }

    public float ViewportWidth { get; }

    public float ViewportHeight { get; }

    public float ViewportWidthChange { get; }

    public float ViewportHeightChange { get; }

    public float ExtentWidth { get; }

    public float ExtentHeight { get; }

    public float ExtentWidthChange { get; }

    public float ExtentHeightChange { get; }
}
