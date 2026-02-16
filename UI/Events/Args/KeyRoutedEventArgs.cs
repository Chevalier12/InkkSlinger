using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed class KeyRoutedEventArgs : RoutedEventArgs
{
    public KeyRoutedEventArgs(RoutedEvent routedEvent, Keys key, ModifierKeys modifiers)
        : base(routedEvent)
    {
        Key = key;
        Modifiers = modifiers;
    }

    public Keys Key { get; }

    public ModifierKeys Modifiers { get; }
}
