using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class SolidColorBrush : Brush
{
    private Color _color;

    public SolidColorBrush()
        : this(Color.Transparent)
    {
    }

    public SolidColorBrush(Color color)
    {
        _color = color;
    }

    public Color Color
    {
        get
        {
            ReadPreamble();
            return _color;
        }
        set
        {
            WritePreamble();
            if (_color == value)
            {
                return;
            }

            _color = value;
            WritePostscript();
        }
    }

    protected override Freezable CreateInstanceCore()
    {
        return new SolidColorBrush();
    }

    protected override void CloneCore(Freezable source)
    {
        var typedSource = (SolidColorBrush)source;
        _color = typedSource._color;
    }

    public override Color ToColor()
    {
        return Color;
    }
}
