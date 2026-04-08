using System;

namespace InkkSlinger;

public readonly struct CornerRadius
{
    public CornerRadius(float uniformRadius)
        : this(uniformRadius, uniformRadius, uniformRadius, uniformRadius)
    {
    }

    public CornerRadius(float topLeft, float topRight, float bottomRight, float bottomLeft)
    {
        TopLeft = topLeft;
        TopRight = topRight;
        BottomRight = bottomRight;
        BottomLeft = bottomLeft;
    }

    public float TopLeft { get; }

    public float TopRight { get; }

    public float BottomRight { get; }

    public float BottomLeft { get; }

    public static CornerRadius Empty => new(0f);

    public CornerRadius ClampNonNegative()
    {
        return new CornerRadius(
            MathF.Max(0f, TopLeft),
            MathF.Max(0f, TopRight),
            MathF.Max(0f, BottomRight),
            MathF.Max(0f, BottomLeft));
    }

    public static implicit operator CornerRadius(float uniformRadius)
    {
        return new CornerRadius(uniformRadius);
    }
}