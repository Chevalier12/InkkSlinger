using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed class InputManager
{
    private readonly List<char> _queuedText = new(32);
    private readonly List<char> _frameText = new(32);
    private readonly List<Keys> _pressedKeys = new(16);
    private readonly List<Keys> _releasedKeys = new(16);
    private readonly HashSet<Keys> _keyUnion = new();

    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;
    private MouseState _currentMouse;
    private MouseState _previousMouse;
    private bool _initialized;

    public void EnqueueTextInput(char character)
    {
        if (character == '\0')
        {
            return;
        }

        _queuedText.Add(character);
    }

    public InputDelta Capture()
    {
        _previousKeyboard = _currentKeyboard;
        _previousMouse = _currentMouse;
        _currentKeyboard = Keyboard.GetState();
        _currentMouse = Mouse.GetState();
        if (!_initialized)
        {
            _previousKeyboard = _currentKeyboard;
            _previousMouse = _currentMouse;
            _initialized = true;
        }

        _pressedKeys.Clear();
        _releasedKeys.Clear();
        _keyUnion.Clear();
        _frameText.Clear();

        var currentKeys = _currentKeyboard.GetPressedKeys();
        var previousKeys = _previousKeyboard.GetPressedKeys();
        for (var i = 0; i < currentKeys.Length; i++)
        {
            _keyUnion.Add(currentKeys[i]);
        }

        for (var i = 0; i < previousKeys.Length; i++)
        {
            _keyUnion.Add(previousKeys[i]);
        }

        foreach (var key in _keyUnion)
        {
            var isDown = _currentKeyboard.IsKeyDown(key);
            var wasDown = _previousKeyboard.IsKeyDown(key);
            if (isDown && !wasDown)
            {
                _pressedKeys.Add(key);
            }
            else if (!isDown && wasDown)
            {
                _releasedKeys.Add(key);
            }
        }

        if (_queuedText.Count > 0)
        {
            _frameText.AddRange(_queuedText);
            _queuedText.Clear();
        }

        var currentPointer = new Vector2(_currentMouse.X, _currentMouse.Y);
        var previousPointer = new Vector2(_previousMouse.X, _previousMouse.Y);
        return new InputDelta
        {
            Previous = new InputSnapshot(_previousKeyboard, _previousMouse, previousPointer),
            Current = new InputSnapshot(_currentKeyboard, _currentMouse, currentPointer),
            PressedKeys = _pressedKeys,
            ReleasedKeys = _releasedKeys,
            TextInput = _frameText,
            PointerMoved = _currentMouse.X != _previousMouse.X || _currentMouse.Y != _previousMouse.Y,
            WheelDelta = _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue,
            LeftPressed = _currentMouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed,
            LeftReleased = _currentMouse.LeftButton != ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Pressed,
            RightPressed = _currentMouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton != ButtonState.Pressed,
            RightReleased = _currentMouse.RightButton != ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Pressed,
            MiddlePressed = _currentMouse.MiddleButton == ButtonState.Pressed && _previousMouse.MiddleButton != ButtonState.Pressed,
            MiddleReleased = _currentMouse.MiddleButton != ButtonState.Pressed && _previousMouse.MiddleButton == ButtonState.Pressed
        };
    }
}
