using System.Collections.Generic;

namespace InkkSlinger;

public sealed class InputDelta
{
    public required InputSnapshot Previous { get; init; }

    public required InputSnapshot Current { get; init; }

    public required IReadOnlyList<Microsoft.Xna.Framework.Input.Keys> PressedKeys { get; init; }

    public required IReadOnlyList<Microsoft.Xna.Framework.Input.Keys> ReleasedKeys { get; init; }

    public required IReadOnlyList<char> TextInput { get; init; }

    public required bool PointerMoved { get; init; }

    public required int WheelDelta { get; init; }

    public required bool LeftPressed { get; init; }

    public required bool LeftReleased { get; init; }

    public required bool RightPressed { get; init; }

    public required bool RightReleased { get; init; }

    public required bool MiddlePressed { get; init; }

    public required bool MiddleReleased { get; init; }

    public bool IsEmpty =>
        !PointerMoved &&
        WheelDelta == 0 &&
        !LeftPressed &&
        !LeftReleased &&
        !RightPressed &&
        !RightReleased &&
        !MiddlePressed &&
        !MiddleReleased &&
        PressedKeys.Count == 0 &&
        ReleasedKeys.Count == 0 &&
        TextInput.Count == 0;
}
