using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class MouseRoutedEventArgs : RoutedEventArgs
{
    public MouseRoutedEventArgs(RoutedEvent routedEvent, Vector2 position, MouseButton button, ModifierKeys modifiers = ModifierKeys.None)
        : base(routedEvent)
    {
        Position = position;
        Button = button;
        Modifiers = modifiers;
    }

    public Vector2 Position { get; }

    public MouseButton Button { get; }

    public ModifierKeys Modifiers { get; }
}
