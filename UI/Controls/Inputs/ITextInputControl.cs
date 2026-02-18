using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public interface ITextInputControl
{
    bool HandleTextInputFromInput(char character);

    bool HandleKeyDownFromInput(Keys key, ModifierKeys modifiers);

    bool HandlePointerDownFromInput(Vector2 pointerPosition, bool extendSelection);

    bool HandlePointerMoveFromInput(Vector2 pointerPosition);

    bool HandlePointerUpFromInput();

    bool HandleMouseWheelFromInput(int delta);

    void SetMouseOverFromInput(bool isMouseOver);

    void SetFocusedFromInput(bool isFocused);
}
