using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed class RoutedKeyEventArgs : RoutedEventArgs
{
    public RoutedKeyEventArgs(RoutedEvent routedEvent, Keys key, bool isRepeat, ModifierKeys modifiers)
        : base(routedEvent)
    {
        Key = key;
        IsRepeat = isRepeat;
        Modifiers = modifiers;
    }

    public Keys Key { get; }

    public bool IsRepeat { get; }

    public ModifierKeys Modifiers { get; }
}

public sealed class RoutedTextInputEventArgs : RoutedEventArgs
{
    public RoutedTextInputEventArgs(RoutedEvent routedEvent, char character)
        : base(routedEvent)
    {
        Character = character;
    }

    public char Character { get; }
}

public sealed class RoutedFocusEventArgs : RoutedEventArgs
{
    public RoutedFocusEventArgs(RoutedEvent routedEvent, UIElement? relatedTarget)
        : base(routedEvent)
    {
        RelatedTarget = relatedTarget;
    }

    public UIElement? RelatedTarget { get; }
}

public sealed class RoutedMouseCaptureEventArgs : RoutedEventArgs
{
    public RoutedMouseCaptureEventArgs(RoutedEvent routedEvent)
        : base(routedEvent)
    {
    }
}
