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

    private struct PointerState
    {
        public int X;
        public int Y;
        public int ScrollWheelValue;
        public ButtonState LeftButton;
        public ButtonState RightButton;
        public ButtonState MiddleButton;

        public static PointerState FromMouseState(MouseState state)
        {
            return new PointerState
            {
                X = state.X,
                Y = state.Y,
                ScrollWheelValue = state.ScrollWheelValue,
                LeftButton = state.LeftButton,
                RightButton = state.RightButton,
                MiddleButton = state.MiddleButton
            };
        }
    }

    private static PointerState _previousPointerState;
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
    private static bool _hasCachedHitTest;
    private static UIElement? _cachedHitRoot;
    private static UIElement? _cachedHitCapturedElement;
    private static Vector2 _cachedHitPosition;
    private static UIElement? _cachedHitElement;
    private static int _hoverReuseAttempts;
    private static int _hoverReuseSuccesses;
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
        InvalidateHitTestCache();
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
        InvalidateHitTestCache();
    }

    public static void Update(UIElement root, GameTime gameTime)
    {
        var pointerState = PointerState.FromMouseState(Mouse.GetState());
        var keyboardState = Keyboard.GetState();
        UpdateCore(root, gameTime, pointerState, keyboardState);
    }

    internal static void UpdateForTesting(
        UIElement root,
        GameTime gameTime,
        Vector2 pointerPosition,
        int scrollWheelValue = 0,
        ButtonState leftButton = ButtonState.Released,
        ButtonState rightButton = ButtonState.Released,
        ButtonState middleButton = ButtonState.Released,
        KeyboardState keyboardState = default)
    {
        var pointerState = new PointerState
        {
            X = (int)pointerPosition.X,
            Y = (int)pointerPosition.Y,
            ScrollWheelValue = scrollWheelValue,
            LeftButton = leftButton,
            RightButton = rightButton,
            MiddleButton = middleButton
        };
        UpdateCore(root, gameTime, pointerState, keyboardState);
    }

    private static void UpdateCore(
        UIElement root,
        GameTime gameTime,
        PointerState pointerState,
        KeyboardState keyboardState)
    {
        _visualStateChangedThisFrame = false;
        _visualStateChangeFlagsThisFrame = InputVisualStateChangeFlags.None;
        var focusedBeforeUpdate = FocusManager.FocusedElement;
        EnsureInputStateIsValid(root);

        var modifiers = GetModifierKeys(keyboardState);

        var pointerPosition = new Vector2(pointerState.X, pointerState.Y);
        var pointerMoved = pointerState.X != _previousPointerState.X ||
                           pointerState.Y != _previousPointerState.Y;
        var wheelDelta = pointerState.ScrollWheelValue - _previousPointerState.ScrollWheelValue;
        var wheelChanged = wheelDelta != 0;
        var mouseButtonsChanged =
            pointerState.LeftButton != _previousPointerState.LeftButton ||
            pointerState.RightButton != _previousPointerState.RightButton ||
            pointerState.MiddleButton != _previousPointerState.MiddleButton;

        var pointerInputChanged = pointerMoved || wheelChanged || mouseButtonsChanged;
        var canReuseCachedHit = _hasCachedHitTest &&
                                ReferenceEquals(_cachedHitRoot, root) &&
                                ReferenceEquals(_cachedHitCapturedElement, _mouseCapturedElement) &&
                                _cachedHitPosition == pointerPosition;

        UIElement? hitElement = _hoveredElement;
        var hitTestMs = 0d;
        if (pointerInputChanged)
        {
            if (canReuseCachedHit)
            {
                hitElement = _cachedHitElement;
            }
            else
            {
                _hoverReuseAttempts++;
                if (!TryReuseHoveredListItemHit(root, pointerPosition, out hitElement))
                {
                    var hitTestStart = Stopwatch.GetTimestamp();
                    hitElement = VisualTreeHelper.HitTest(root, pointerPosition);
                    hitTestMs = Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
                }
                else
                {
                    _hoverReuseSuccesses++;
                }

                CacheHitTestResult(root, pointerPosition, _mouseCapturedElement, hitElement);
            }
        }
        else if (canReuseCachedHit)
        {
            hitElement = _cachedHitElement;
        }

        var eventTarget = _mouseCapturedElement ?? hitElement;
        var shouldUpdateCursor = pointerInputChanged ||
                                 !ReferenceEquals(eventTarget, _hoveredElement) ||
                                 _mouseCapturedElement != null;
        if (shouldUpdateCursor)
        {
            UpdateCursor(eventTarget ?? _hoveredElement);
        }
        if (EnableInputTrace)
        {
            Console.WriteLine(
                $"[Input] t={Environment.TickCount64} ptr=({pointerPosition.X:0.#},{pointerPosition.Y:0.#}) " +
                $"wheel={wheelDelta} " +
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

        if (eventTarget != null && pointerMoved)
        {
            eventTarget.NotifyMouseMove(pointerPosition, modifiers);
        }

        if (mouseButtonsChanged)
        {
            ProcessMouseButton(pointerState.LeftButton, _previousPointerState.LeftButton, MouseButton.Left, eventTarget, pointerPosition, modifiers, gameTime);
            ProcessMouseButton(pointerState.RightButton, _previousPointerState.RightButton, MouseButton.Right, eventTarget, pointerPosition, modifiers, gameTime);
            ProcessMouseButton(pointerState.MiddleButton, _previousPointerState.MiddleButton, MouseButton.Middle, eventTarget, pointerPosition, modifiers, gameTime);
        }

        if (eventTarget != null && wheelChanged)
        {
            eventTarget.NotifyMouseWheel(pointerPosition, wheelDelta, modifiers);
        }

        if (FocusManager.FocusedElement != null)
        {
            var keyboardUnchanged = keyboardState.Equals(_previousKeyboardState);
            if (!keyboardUnchanged || NextKeyRepeatTimes.Count > 0)
            {
                ProcessKeyboard(root, FocusManager.FocusedElement, keyboardState, modifiers, gameTime.TotalGameTime.TotalSeconds);
            }
        }

        if (!ReferenceEquals(focusedBeforeUpdate, FocusManager.FocusedElement))
        {
            _visualStateChangedThisFrame = true;
            _visualStateChangeFlagsThisFrame |= InputVisualStateChangeFlags.FocusChanged;
        }

        _previousPointerState = pointerState;
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
                InvalidateHitTestCache();
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
            InvalidateHitTestCache();
        }

        if (ReferenceEquals(_hoveredElement, element))
        {
            _hoveredElement = null;
            InvalidateHitTestCache();
        }

        if (ReferenceEquals(FocusManager.FocusedElement, element))
        {
            FocusManager.SetFocusedElement(null);
            InvalidateHitTestCache();
        }
    }

    internal static void ResetForTests()
    {
        _previousPointerState = default;
        _previousKeyboardState = default;
        _hoveredElement = null;
        _visualStateChangedThisFrame = false;
        _visualStateChangeFlagsThisFrame = InputVisualStateChangeFlags.None;
        InvalidateHitTestCache();

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
        _hoverReuseAttempts = 0;
        _hoverReuseSuccesses = 0;
    }

    internal static (int Attempts, int Successes) GetHoverReuseStatsForTests()
    {
        return (_hoverReuseAttempts, _hoverReuseSuccesses);
    }

    private static void CacheHitTestResult(
        UIElement root,
        Vector2 pointerPosition,
        UIElement? capturedElement,
        UIElement? hitElement)
    {
        _hasCachedHitTest = true;
        _cachedHitRoot = root;
        _cachedHitCapturedElement = capturedElement;
        _cachedHitPosition = pointerPosition;
        _cachedHitElement = hitElement;
    }

    private static void InvalidateHitTestCache()
    {
        _hasCachedHitTest = false;
        _cachedHitRoot = null;
        _cachedHitCapturedElement = null;
        _cachedHitPosition = Vector2.Zero;
        _cachedHitElement = null;
    }

    internal static void NotifyHitTestGeometryChanged()
    {
        InvalidateHitTestCache();
    }

    private static bool TryReuseHoveredListItemHit(
        UIElement root,
        Vector2 pointerPosition,
        out UIElement? hitElement)
    {
        hitElement = null;
        if (_mouseCapturedElement != null)
        {
            return false;
        }

        if (_hoveredElement == null)
        {
            return false;
        }

        if (!TryGetListBoxItemAncestor(_hoveredElement, root, out _))
        {
            return false;
        }

        if (!IsElementOnVisualChainToRoot(root, _hoveredElement))
        {
            return false;
        }

        if (!IsPointerInsideElementAndAncestors(root, _hoveredElement, pointerPosition))
        {
            return false;
        }

        hitElement = _hoveredElement;
        return true;
    }

    private static bool TryGetListBoxItemAncestor(
        UIElement element,
        UIElement root,
        out ListBoxItem? listBoxItem)
    {
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (current is ListBoxItem found)
            {
                listBoxItem = found;
                return true;
            }

            if (ReferenceEquals(current, root))
            {
                break;
            }
        }

        listBoxItem = null;
        return false;
    }

    private static bool IsElementOnVisualChainToRoot(UIElement root, UIElement element)
    {
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointerInsideElementAndAncestors(
        UIElement root,
        UIElement element,
        Vector2 pointerPosition)
    {
        for (var current = element; current != null; current = current.VisualParent)
        {
            if (!current.IsVisible || !current.IsEnabled || !current.IsHitTestVisible)
            {
                return false;
            }

            if (current is ScrollViewer scrollViewer &&
                (scrollViewer.HorizontalOffset != 0f || scrollViewer.VerticalOffset != 0f))
            {
                // Scroll offsets are applied via render transforms. Fall back to full hit-test for correctness.
                return false;
            }

            if (current is FrameworkElement frameworkElement)
            {
                var slot = frameworkElement.LayoutSlot;
                if (pointerPosition.X < slot.X ||
                    pointerPosition.X > slot.X + slot.Width ||
                    pointerPosition.Y < slot.Y ||
                    pointerPosition.Y > slot.Y + slot.Height)
                {
                    return false;
                }
            }
            else if (!current.HitTest(pointerPosition))
            {
                return false;
            }

            if (ReferenceEquals(current, root))
            {
                return true;
            }
        }

        return false;
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
