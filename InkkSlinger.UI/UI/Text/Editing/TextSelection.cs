namespace InkkSlinger;

public readonly struct TextSelection
{
    public TextSelection(int anchor, int caret)
    {
        Anchor = anchor;
        Caret = caret;
    }

    public int Anchor { get; }

    public int Caret { get; }

    public int Start => System.Math.Min(Anchor, Caret);

    public int End => System.Math.Max(Anchor, Caret);

    public int Length => End - Start;

    public bool IsEmpty => Anchor == Caret;
}
