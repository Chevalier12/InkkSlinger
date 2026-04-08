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

    internal override Color SampleColor(LayoutRect bounds, Vector2 point)
    {
        _ = bounds;
        _ = point;
        return Color;
    }

    internal override string GetRenderSignature()
    {
        return $"solid:{Color.PackedValue:X8}";
    }

    public override Color ToColor()
    {
        return Color;
    }
}
