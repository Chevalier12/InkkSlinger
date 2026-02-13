namespace InkkSlinger;

public sealed class ScrollChangedEventArgs : RoutedEventArgs
{
    public ScrollChangedEventArgs(
        RoutedEvent routedEvent,
        float extentWidth,
        float extentHeight,
        float viewportWidth,
        float viewportHeight,
        float horizontalOffset,
        float verticalOffset,
        float extentWidthChange,
        float extentHeightChange,
        float viewportWidthChange,
        float viewportHeightChange,
        float horizontalChange,
        float verticalChange)
        : base(routedEvent)
    {
        ExtentWidth = extentWidth;
        ExtentHeight = extentHeight;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        HorizontalOffset = horizontalOffset;
        VerticalOffset = verticalOffset;
        ExtentWidthChange = extentWidthChange;
        ExtentHeightChange = extentHeightChange;
        ViewportWidthChange = viewportWidthChange;
        ViewportHeightChange = viewportHeightChange;
        HorizontalChange = horizontalChange;
        VerticalChange = verticalChange;
    }

    public float ExtentWidth { get; }

    public float ExtentHeight { get; }

    public float ViewportWidth { get; }

    public float ViewportHeight { get; }

    public float HorizontalOffset { get; }

    public float VerticalOffset { get; }

    public float ExtentWidthChange { get; }

    public float ExtentHeightChange { get; }

    public float ViewportWidthChange { get; }

    public float ViewportHeightChange { get; }

    public float HorizontalChange { get; }

    public float VerticalChange { get; }
}
