using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class MouseWheelRoutedEventArgs : RoutedEventArgs
{
    public MouseWheelRoutedEventArgs(RoutedEvent routedEvent, Vector2 position, int delta)
        : base(routedEvent)
    {
        Position = position;
        Delta = delta;
    }

    public Vector2 Position { get; }

    public int Delta { get; }
}
