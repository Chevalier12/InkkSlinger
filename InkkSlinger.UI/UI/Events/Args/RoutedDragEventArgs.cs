namespace InkkSlinger;

public sealed class DragStartedEventArgs : RoutedEventArgs
{
    public DragStartedEventArgs(RoutedEvent routedEvent, float horizontalOffset, float verticalOffset)
        : base(routedEvent)
    {
        HorizontalOffset = horizontalOffset;
        VerticalOffset = verticalOffset;
    }

    public float HorizontalOffset { get; }

    public float VerticalOffset { get; }
}

public sealed class DragDeltaEventArgs : RoutedEventArgs
{
    public DragDeltaEventArgs(RoutedEvent routedEvent, float horizontalChange, float verticalChange)
        : base(routedEvent)
    {
        HorizontalChange = horizontalChange;
        VerticalChange = verticalChange;
    }

    public float HorizontalChange { get; }

    public float VerticalChange { get; }
}

public sealed class DragCompletedEventArgs : RoutedEventArgs
{
    public DragCompletedEventArgs(RoutedEvent routedEvent, float horizontalChange, float verticalChange, bool canceled)
        : base(routedEvent)
    {
        HorizontalChange = horizontalChange;
        VerticalChange = verticalChange;
        Canceled = canceled;
    }

    public float HorizontalChange { get; }

    public float VerticalChange { get; }

    public bool Canceled { get; }
}
