namespace InkkSlinger;

public sealed class RequestBringIntoViewEventArgs : RoutedEventArgs
{
    public RequestBringIntoViewEventArgs(RoutedEvent routedEvent, UIElement targetObject, LayoutRect targetRect)
        : base(routedEvent)
    {
        TargetObject = targetObject;
        TargetRect = targetRect;
    }

    public UIElement TargetObject { get; }

    public LayoutRect TargetRect { get; }
}
