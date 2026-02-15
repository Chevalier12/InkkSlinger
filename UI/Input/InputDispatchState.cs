using Microsoft.Xna.Framework;

namespace InkkSlinger;

public sealed class InputDispatchState
{
    public UIElement? FocusedElement { get; set; }

    public UIElement? HoveredElement { get; set; }

    public UIElement? CapturedPointerElement { get; set; }

    public Vector2 LastPointerPosition { get; set; }

    public ModifierKeys CurrentModifiers { get; set; }
}
