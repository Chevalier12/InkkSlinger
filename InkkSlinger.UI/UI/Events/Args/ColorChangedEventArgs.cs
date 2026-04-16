using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class ColorChangedEventArgs : RoutedEventArgs
{
    public ColorChangedEventArgs(RoutedEvent routedEvent, Color oldColor, Color newColor)
        : base(routedEvent)
    {
        OldColor = oldColor;
        NewColor = newColor;
    }

    public Color OldColor { get; }

    public Color NewColor { get; }
}