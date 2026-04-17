using System;

namespace InkkSlinger;

public abstract class RoutedEventArgs : EventArgs
{
    protected RoutedEventArgs(RoutedEvent routedEvent)
    {
        RoutedEvent = routedEvent;
    }

    public RoutedEvent RoutedEvent { get; internal set; }

    public bool Handled { get; set; }

    public UIElement? Source { get; internal set; }

    public UIElement? OriginalSource { get; internal set; }
}
