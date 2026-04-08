namespace InkkSlinger;

public sealed class TextInputRoutedEventArgs : RoutedEventArgs
{
    public TextInputRoutedEventArgs(RoutedEvent routedEvent, char character)
        : base(routedEvent)
    {
        Character = character;
    }

    public char Character { get; }
}
