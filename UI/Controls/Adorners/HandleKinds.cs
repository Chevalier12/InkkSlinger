using System;

namespace InkkSlinger;

[Flags]
public enum HandleSet
{
    None = 0,
    TopLeft = 1 << 0,
    Top = 1 << 1,
    TopRight = 1 << 2,
    Left = 1 << 3,
    Right = 1 << 4,
    BottomLeft = 1 << 5,
    Bottom = 1 << 6,
    BottomRight = 1 << 7,
    Corners = TopLeft | TopRight | BottomLeft | BottomRight,
    Edges = Top | Left | Right | Bottom,
    All = Corners | Edges
}

public enum HandleKind
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}

public sealed class HandleDragDeltaEventArgs : EventArgs
{
    public HandleDragDeltaEventArgs(HandleKind handle, float horizontalChange, float verticalChange)
    {
        Handle = handle;
        HorizontalChange = horizontalChange;
        VerticalChange = verticalChange;
    }

    public HandleKind Handle { get; }

    public float HorizontalChange { get; }

    public float VerticalChange { get; }
}
