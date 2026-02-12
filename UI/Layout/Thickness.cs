namespace InkkSlinger;

public readonly struct Thickness
{
    public Thickness(float uniform)
        : this(uniform, uniform, uniform, uniform)
    {
    }

    public Thickness(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public float Left { get; }

    public float Top { get; }

    public float Right { get; }

    public float Bottom { get; }

    public float Horizontal => Left + Right;

    public float Vertical => Top + Bottom;

    public static Thickness Empty => new(0f);
}
