using Microsoft.Xna.Framework;

namespace InkkSlinger;

public class RoutedMouseEventArgs : RoutedEventArgs
{
    public RoutedMouseEventArgs(RoutedEvent routedEvent, Vector2 position, ModifierKeys modifiers)
        : base(routedEvent)
    {
        Position = position;
        Modifiers = modifiers;
    }

    public Vector2 Position { get; }

    public ModifierKeys Modifiers { get; }
}

public sealed class RoutedMouseButtonEventArgs : RoutedMouseEventArgs
{
    public RoutedMouseButtonEventArgs(
        RoutedEvent routedEvent,
        Vector2 position,
        MouseButton button,
        int clickCount,
        ModifierKeys modifiers)
        : base(routedEvent, position, modifiers)
    {
        Button = button;
        ClickCount = clickCount;
    }

    public MouseButton Button { get; }

    public int ClickCount { get; }
}

public sealed class RoutedMouseWheelEventArgs : RoutedMouseEventArgs
{
    public RoutedMouseWheelEventArgs(
        RoutedEvent routedEvent,
        Vector2 position,
        int delta,
        ModifierKeys modifiers)
        : base(routedEvent, position, modifiers)
    {
        Delta = delta;
    }

    public int Delta { get; }
}
