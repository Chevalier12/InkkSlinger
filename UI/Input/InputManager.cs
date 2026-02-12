using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public static class InputManager
{
    private static readonly bool EnableInputTrace = false;
    [System.Flags]
    public enum InputVisualStateChangeFlags
    {
        None = 0,
        HoverChanged = 1 << 0,
        FocusChanged = 1 << 1,
        CursorChanged = 1 << 2
    }

    private static MouseState _previousMouseState;
    private static KeyboardState _previousKeyboardState;
    private static readonly Dictionary<Keys, double> NextKeyRepeatTimes = new();
    private static UIElement? _hoveredElement;
    private static UIElement? _mouseCapturedElement;
    private static MouseButton _lastClickButton = MouseButton.None;
    private static double _lastClickTimeMs;
    private static Vector2 _lastClickPosition;
    private static UiCursor _activeCursor = UiCursor.Arrow;
    private static bool _cursorApiAvailable = true;
    private static bool _visualStateChangedThisFrame;
    private static InputVisualStateChangeFlags _visualStateChangeFlagsThisFrame;
    private const double KeyRepeatInitialDelaySeconds = 0.35d;
    private const double KeyRepeatIntervalSeconds = 0.045d;

    public static UIElement? MouseCapturedElement => _mouseCapturedElement;

    public static UiCursor ActiveCursor => _activeCursor;

    public static bool VisualStateChangedThisFrame => _visualStateChangedThisFrame;

    public static InputVisualStateChangeFlags VisualStateChangeFlagsThisFrame => _visualStateChangeFlagsThisFrame;

    public static bool CaptureMouse(UIElement element)
    {
        if (_mouseCapturedElement == element)
        {
            return true;
        }

        _mouseCapturedElement?.NotifyLostMouseCapture();
        _mouseCapturedElement = element;
        _mouseCapturedElement.NotifyGotMouseCapture();
        return true;
    }

    public static void ReleaseMouseCapture(UIElement element)
    {
        if (_mouseCapturedElement != element)
        {
            return;
        }

        _mouseCapturedElement.NotifyLostMouseCapture();
        _mouseCapturedElement = null;
    }

    public static void Update(UIElement root, GameTime gameTime)
    {
        _visualStateChangedThisFrame = false;
        _visualStateChangeFlagsThisFrame = InputVisualStateChangeFlags.None;
        var focusedBeforeUpdate = FocusManager.FocusedElement;
        EnsureInputStateIsValid(root);

        var mouseState = Mouse.GetState();
        var keyboardState = Keyboard.GetState();
        var modifiers = GetModifierKeys(keyboardState);

        var pointerPosition = new Vector2(mouseState.X, mouseState.Y);
        var hitTestStart = Stopwatch.GetTimestamp();
        var hitElement = VisualTreeHelper.HitTest(root, pointerPosition);
        var hitTestMs = Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
        var eventTarget = _mouseCapturedElement ?? hitElement;
        UpdateCursor(_mouseCapturedElement ?? hitElement);
        if (EnableInputTrace)
        {
            Console.WriteLine(
                $"[Input] t={Environment.TickCount64} ptr=({pointerPosition.X:0.#},{pointerPosition.Y:0.#}) " +
                $"wheel={mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue} " +
                $"hit={hitElement?.GetType().Name ?? "null"} target={eventTarget?.GetType().Name ?? "null"} " +
                $"hitMs={hitTestMs:0.###}");
        }

        if (_hoveredElement != hitElement)
        {
            _hoveredElement?.NotifyMouseLeave(pointerPosition, modifiers);
            hitElement?.NotifyMouseEnter(pointerPosition, modifiers);
            _hoveredElement = hitElement;
            _visualStateChangedThisFrame = true;
            _visualStateChangeFlagsThisFrame |= InputVisualStateChangeFlags.HoverChanged;
        }

        if (eventTarget != null && (mouseState.X != _previousMouseState.X || mouseState.Y != _previousMouseState.Y))
        {
            eventTarget.NotifyMouseMove(pointerPosition, modifiers);
        }

        ProcessMouseButton(mouseState.LeftButton, _previousMouseState.LeftButton, MouseButton.Left, eventTarget, pointerPosition, modifiers, gameTime);
        ProcessMouseButton(mouseState.RightButton, _previousMouseState.RightButton, MouseButton.Right, eventTarget, pointerPosition, modifiers, gameTime);
        ProcessMouseButton(mouseState.MiddleButton, _previousMouseState.MiddleButton, MouseButton.Middle, eventTarget, pointerPosition, modifiers, gameTime);

        var wheelDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
        if (eventTarget != null && wheelDelta != 0)
        {
            eventTarget.NotifyMouseWheel(pointerPosition, wheelDelta, modifiers);
        }

        if (FocusManager.FocusedElement != null)
        {
            ProcessKeyboard(root, FocusManager.FocusedElement, keyboardState, modifiers, gameTime.TotalGameTime.TotalSeconds);
        }

        if (!ReferenceEquals(focusedBeforeUpdate, FocusManager.FocusedElement))
        {
            _visualStateChangedThisFrame = true;
            _visualStateChangeFlagsThisFrame |= InputVisualStateChangeFlags.FocusChanged;
        }

        _previousMouseState = mouseState;
        _previousKeyboardState = keyboardState;
    }

    public static void ProcessTextInput(char character)
    {
        FocusManager.FocusedElement?.NotifyTextInput(character);
    }

    internal static void EnsureInputStateIsValid(UIElement root)
    {
        if (_mouseCapturedElement != null)
        {
            var captured = _mouseCapturedElement;
            var captureIsInvalid = !IsInTree(root, captured) || !captured.IsEnabled || !captured.IsVisible;
            if (captureIsInvalid)
            {
                _mouseCapturedElement = null;
                captured.NotifyLostMouseCapture();
            }
        }

        if (FocusManager.FocusedElement != null)
        {
            var focused = FocusManager.FocusedElement;
            var focusIsInvalid = focused == null ||
                                 !IsInTree(root, focused) ||
                                 !FocusManager.IsFocusableElement(focused);
            if (focusIsInvalid)
            {
                FocusManager.SetFocusedElement(null);
            }
        }
    }

    internal static void NotifyElementStateInvalidated(UIElement element)
    {
        if (ReferenceEquals(_mouseCapturedElement, element))
        {
            _mouseCapturedElement = null;
            element.NotifyLostMouseCapture();
        }

        if (ReferenceEquals(FocusManager.FocusedElement, element))
        {
            FocusManager.SetFocusedElement(null);
        }
    }

    internal static void ResetForTests()
    {
        _previousMouseState = default;
        _previousKeyboardState = default;
        _hoveredElement = null;
        _visualStateChangedThisFrame = false;
        _visualStateChangeFlagsThisFrame = InputVisualStateChangeFlags.None;

        if (_mouseCapturedElement != null)
        {
            var captured = _mouseCapturedElement;
            _mouseCapturedElement = null;
            captured.NotifyLostMouseCapture();
        }

        _lastClickButton = MouseButton.None;
        _lastClickTimeMs = 0d;
        _lastClickPosition = Vector2.Zero;
        _activeCursor = UiCursor.Arrow;
        NextKeyRepeatTimes.Clear();
    }

    private static void ProcessMouseButton(
        ButtonState current,
        ButtonState previous,
        MouseButton button,
        UIElement? target,
        Vector2 position,
        ModifierKeys modifiers,
        GameTime gameTime)
    {
        if (target == null)
        {
            return;
        }

        if (previous == ButtonState.Released && current == ButtonState.Pressed)
        {
            var clickCount = CalculateClickCount(button, position, gameTime.TotalGameTime.TotalMilliseconds);
            target.NotifyMouseDown(position, button, clickCount, modifiers);
            if (target.Focusable)
            {
                FocusManager.SetFocusedElement(target);
            }
        }

        if (previous == ButtonState.Pressed && current == ButtonState.Released)
        {
            target.NotifyMouseUp(position, button, 1, modifiers);
        }
    }

    private static void ProcessKeyboard(
        UIElement root,
        UIElement target,
        KeyboardState keyboardState,
        ModifierKeys modifiers,
        double totalSeconds)
    {
        var previousKeys = new HashSet<Keys>(_previousKeyboardState.GetPressedKeys());
        var currentKeys = keyboardState.GetPressedKeys();

        foreach (var key in currentKeys)
        {
            var wasPressed = previousKeys.Contains(key);
            var isRepeat = false;
            if (wasPressed)
            {
                if (!NextKeyRepeatTimes.TryGetValue(key, out var nextRepeatTime))
                {
                    nextRepeatTime = totalSeconds + KeyRepeatInitialDelaySeconds;
                    NextKeyRepeatTimes[key] = nextRepeatTime;
                    previousKeys.Remove(key);
                    continue;
                }

                if (totalSeconds < nextRepeatTime)
                {
                    previousKeys.Remove(key);
                    continue;
                }

                isRepeat = true;
                NextKeyRepeatTimes[key] = totalSeconds + KeyRepeatIntervalSeconds;
            }
            else
            {
                NextKeyRepeatTimes[key] = totalSeconds + KeyRepeatInitialDelaySeconds;
            }

            if (!isRepeat && key == Keys.Tab)
            {
                var moved = FocusManager.MoveFocus(root, backwards: (modifiers & ModifierKeys.Shift) != 0);
                if (moved)
                {
                    previousKeys.Remove(key);
                    continue;
                }
            }

            target.NotifyKeyDown(key, isRepeat, modifiers);
            previousKeys.Remove(key);
        }

        foreach (var releasedKey in previousKeys)
        {
            target.NotifyKeyUp(releasedKey, modifiers);
            NextKeyRepeatTimes.Remove(releasedKey);
        }
    }

    private static int CalculateClickCount(MouseButton button, Vector2 position, double nowMilliseconds)
    {
        const double maxDoubleClickTime = 500d;
        const float maxDistance = 4f;

        var isSameButton = button == _lastClickButton;
        var isWithinTime = (nowMilliseconds - _lastClickTimeMs) <= maxDoubleClickTime;
        var isWithinDistance = Vector2.Distance(position, _lastClickPosition) <= maxDistance;

        _lastClickButton = button;
        _lastClickTimeMs = nowMilliseconds;
        _lastClickPosition = position;

        return isSameButton && isWithinTime && isWithinDistance ? 2 : 1;
    }

    private static ModifierKeys GetModifierKeys(KeyboardState keyboardState)
    {
        var modifiers = ModifierKeys.None;

        if (keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift))
        {
            modifiers |= ModifierKeys.Shift;
        }

        if (keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl))
        {
            modifiers |= ModifierKeys.Control;
        }

        if (keyboardState.IsKeyDown(Keys.LeftAlt) || keyboardState.IsKeyDown(Keys.RightAlt))
        {
            modifiers |= ModifierKeys.Alt;
        }

        return modifiers;
    }

    private static bool IsInTree(UIElement root, UIElement target)
    {
        return IsInTree(root, target, new HashSet<UIElement>());
    }

    private static bool IsInTree(UIElement current, UIElement target, ISet<UIElement> visited)
    {
        if (!visited.Add(current))
        {
            return false;
        }

        if (ReferenceEquals(current, target))
        {
            return true;
        }

        foreach (var child in current.GetVisualChildren())
        {
            if (IsInTree(child, target, visited))
            {
                return true;
            }
        }

        foreach (var child in current.GetLogicalChildren())
        {
            if (IsInTree(child, target, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static void UpdateCursor(UIElement? source)
    {
        var requested = source?.Cursor ?? UiCursor.Arrow;
        if (requested == _activeCursor)
        {
            return;
        }

        _activeCursor = requested;
        _visualStateChangedThisFrame = true;
        _visualStateChangeFlagsThisFrame |= InputVisualStateChangeFlags.CursorChanged;
        if (!_cursorApiAvailable)
        {
            return;
        }

        try
        {
            Mouse.SetCursor(MapCursor(requested));
        }
        catch
        {
            // Some platforms/backends may not support runtime cursor switches.
            _cursorApiAvailable = false;
        }
    }

    private static MouseCursor MapCursor(UiCursor cursor)
    {
        return cursor switch
        {
            UiCursor.Hand => MouseCursor.Hand,
            UiCursor.IBeam => MouseCursor.IBeam,
            UiCursor.Cross => MouseCursor.Crosshair,
            UiCursor.SizeWE => MouseCursor.SizeWE,
            UiCursor.SizeNS => MouseCursor.SizeNS,
            UiCursor.SizeNWSE => MouseCursor.SizeAll,
            UiCursor.SizeAll => MouseCursor.SizeAll,
            UiCursor.Wait => MouseCursor.Wait,
            _ => MouseCursor.Arrow
        };
    }

    internal static void SetVisualStateChangeFlagsForTests(InputVisualStateChangeFlags flags)
    {
        _visualStateChangeFlagsThisFrame = flags;
        _visualStateChangedThisFrame = flags != InputVisualStateChangeFlags.None;
    }
}
