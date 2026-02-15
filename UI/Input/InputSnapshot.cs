using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public readonly struct InputSnapshot
{
    public InputSnapshot(KeyboardState keyboard, MouseState mouse, Vector2 pointerPosition)
    {
        Keyboard = keyboard;
        Mouse = mouse;
        PointerPosition = pointerPosition;
    }

    public KeyboardState Keyboard { get; }

    public MouseState Mouse { get; }

    public Vector2 PointerPosition { get; }
}
