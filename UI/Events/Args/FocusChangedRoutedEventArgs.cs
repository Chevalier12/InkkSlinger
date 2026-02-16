namespace InkkSlinger;

public sealed class FocusChangedRoutedEventArgs : RoutedEventArgs
{
    public FocusChangedRoutedEventArgs(RoutedEvent routedEvent, UIElement? oldFocus, UIElement? newFocus)
        : base(routedEvent)
    {
        OldFocus = oldFocus;
        NewFocus = newFocus;
    }

    public UIElement? OldFocus { get; }

    public UIElement? NewFocus { get; }
}
