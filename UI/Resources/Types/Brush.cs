using Microsoft.Xna.Framework;

namespace InkkSlinger;

public abstract class Brush : Freezable
{
    public static implicit operator Brush(Color color)
    {
        return new SolidColorBrush(color);
    }

    public new Brush Clone()
    {
        return (Brush)base.Clone();
    }

    public new Brush CloneCurrentValue()
    {
        return (Brush)base.CloneCurrentValue();
    }

    internal abstract Color SampleColor(LayoutRect bounds, Vector2 point);

    internal abstract string GetRenderSignature();

    public abstract Color ToColor();
}
