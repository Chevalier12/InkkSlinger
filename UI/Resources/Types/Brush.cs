using Microsoft.Xna.Framework;

namespace InkkSlinger;

public abstract class Brush : Freezable
{
    public new Brush Clone()
    {
        return (Brush)base.Clone();
    }

    public new Brush CloneCurrentValue()
    {
        return (Brush)base.CloneCurrentValue();
    }

    public abstract Color ToColor();
}
