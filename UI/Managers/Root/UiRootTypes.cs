using System;
using InkkSlinger.UI.Telemetry;

namespace InkkSlinger;

public enum UiInvalidationType
{
    Measure,
    Arrange,
    Render
}

[Flags]
public enum UiRedrawReason
{
    None = 0,
    LayoutInvalidated = 1 << 0,
    RenderInvalidated = 1 << 1,
    AnimationActive = 1 << 2,
    CaretBlinkActive = 1 << 3,
    Resize = 1 << 4
}

public enum UiUpdatePhase
{
    InputAndEvents,
    BindingAndDeferred,
    Layout,
    Animation,
    RenderScheduling
}




