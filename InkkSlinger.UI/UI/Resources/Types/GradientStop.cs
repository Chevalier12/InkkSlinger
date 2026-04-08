using System;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class GradientStop : Freezable
{
    private Color _color;
    private float _offset;

    public GradientStop()
    {
    }

    public GradientStop(Color color, float offset)
    {
        _color = color;
        _offset = offset;
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

    public float Offset
    {
        get
        {
            ReadPreamble();
            return _offset;
        }
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "GradientStop offset must be a finite number.");
            }

            WritePreamble();
            if (_offset == value)
            {
                return;
            }

            _offset = value;
            WritePostscript();
        }
    }

    internal string GetRenderSignature()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Offset:R}:{Color.PackedValue:X8}");
    }

    protected override Freezable CreateInstanceCore()
    {
        return new GradientStop();
    }

    protected override void CloneCore(Freezable source)
    {
        if (source is not GradientStop stop)
        {
            return;
        }

        _color = stop._color;
        _offset = stop._offset;
    }
}