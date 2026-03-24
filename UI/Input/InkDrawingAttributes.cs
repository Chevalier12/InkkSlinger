using System;
using Microsoft.Xna.Framework;

namespace InkkSlinger;

/// <summary>
/// Describes the visual attributes applied to an ink stroke.
/// Mirrors the WPF <c>System.Windows.Ink.DrawingAttributes</c> surface at a practical parity level.
/// </summary>
public sealed class InkDrawingAttributes
{
    public Color Color { get; set; } = Color.Black;

    public float Width { get; set; } = 2f;

    public float Height { get; set; } = 2f;

    public float Opacity { get; set; } = 1f;

    public InkDrawingAttributes Clone()
    {
        return new InkDrawingAttributes
        {
            Color = Color,
            Width = Width,
            Height = Height,
            Opacity = Opacity
        };
    }
}
