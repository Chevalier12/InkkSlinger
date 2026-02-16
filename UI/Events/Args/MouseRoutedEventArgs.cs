using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class MouseRoutedEventArgs : RoutedEventArgs
{
    public MouseRoutedEventArgs(RoutedEvent routedEvent, Vector2 position, MouseButton button)
        : base(routedEvent)
    {
        Position = position;
        Button = button;
    }

    public Vector2 Position { get; }

    public MouseButton Button { get; }
}
