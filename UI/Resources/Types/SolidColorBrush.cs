using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class SolidColorBrush : Brush
{
    public SolidColorBrush()
        : this(Color.Transparent)
    {
    }

    public SolidColorBrush(Color color)
    {
        Color = color;
    }

    public Color Color { get; set; }

    public override Color ToColor()
    {
        return Color;
    }
}

