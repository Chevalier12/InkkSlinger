using System;

namespace InkkSlinger;

[Flags]
public enum UiRedrawReason
{
    None = 0,
    AnimationActive = 1 << 0,
    CaretBlink = 1 << 1,
    HoverChanged = 1 << 2,
    FocusChanged = 1 << 3,
    CursorChanged = 1 << 4,
    ViewportResized = 1 << 5,
    ExplicitFullInvalidation = 1 << 6
}

public enum UiRedrawScope
{
    None = 0,
    Region = 1,
    Full = 2
}
