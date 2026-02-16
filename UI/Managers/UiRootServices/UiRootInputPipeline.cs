using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private void RunInputAndEventsPhase(GameTime gameTime)
    {
        _lastInputHitTestCount = 0;
        _lastInputRoutedEventCount = 0;
        _lastInputKeyEventCount = 0;
        _lastInputTextEventCount = 0;
        _lastInputPointerEventCount = 0;
        _lastInputCaptureMs = 0d;
        _lastInputDispatchMs = 0d;
        _lastInputPointerDispatchMs = 0d;
        _lastInputPointerTargetResolveMs = 0d;
        _lastInputHoverUpdateMs = 0d;
        _lastInputPointerRouteMs = 0d;
        _lastInputKeyDispatchMs = 0d;
        _lastInputTextDispatchMs = 0d;
        _lastVisualUpdateMs = 0d;

        var enableInputPipeline = !string.Equals(
            Environment.GetEnvironmentVariable("INKKSLINGER_ENABLE_INPUT_PIPELINE"),
            "0",
            StringComparison.Ordinal);
        if (enableInputPipeline)
        {
            var captureStart = Stopwatch.GetTimestamp();
            var delta = _inputManager.Capture();
            _lastInputCaptureMs = Stopwatch.GetElapsedTime(captureStart).TotalMilliseconds;
            var dispatchStart = Stopwatch.GetTimestamp();
            ProcessInputDelta(delta);
            _lastInputDispatchMs = Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds;
        }

        var updateStart = Stopwatch.GetTimestamp();
        _visualRoot.Update(gameTime);
        _lastVisualUpdateMs = Stopwatch.GetElapsedTime(updateStart).TotalMilliseconds;
    }

    internal void RunInputDeltaForTests(InputDelta delta)
    {
        _lastInputHitTestCount = 0;
        _lastInputRoutedEventCount = 0;
        _lastInputKeyEventCount = 0;
        _lastInputTextEventCount = 0;
        _lastInputPointerEventCount = 0;
        _lastInputCaptureMs = 0d;
        _lastInputDispatchMs = 0d;
        _lastInputPointerDispatchMs = 0d;
        _lastInputPointerTargetResolveMs = 0d;
        _lastInputHoverUpdateMs = 0d;
        _lastInputPointerRouteMs = 0d;
        _lastInputKeyDispatchMs = 0d;
        _lastInputTextDispatchMs = 0d;
        ProcessInputDelta(delta);
    }

    private void ProcessInputDelta(InputDelta delta)
    {
        var pointerStart = Stopwatch.GetTimestamp();
        _inputState.CurrentModifiers = GetModifiers(delta.Current.Keyboard);
        var pointerResolveStart = Stopwatch.GetTimestamp();
        var pointerTarget = ResolvePointerTarget(delta);
        _lastInputPointerTargetResolveMs = Stopwatch.GetElapsedTime(pointerResolveStart).TotalMilliseconds;
        var hoverTicks = 0L;
        var pointerRouteTicks = 0L;
        if (delta.PointerMoved)
        {
            var previousHovered = _inputState.HoveredElement;
            var hoverStart = Stopwatch.GetTimestamp();
            UpdateHover(pointerTarget);
            hoverTicks += Stopwatch.GetTimestamp() - hoverStart;
            var shouldRouteMove = !ReferenceEquals(previousHovered, _inputState.HoveredElement) ||
                                  _inputState.CapturedPointerElement != null ||
                                  string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_ALWAYS_ROUTE_MOUSEMOVE"), "1", StringComparison.Ordinal);
            if (shouldRouteMove)
            {
                var routeStart = Stopwatch.GetTimestamp();
                DispatchPointerMove(pointerTarget, delta.Current.PointerPosition);
                pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
            }
        }

        if (delta.LeftPressed)
        {
            var routeStart = Stopwatch.GetTimestamp();
            DispatchMouseDown(pointerTarget, delta.Current.PointerPosition, MouseButton.Left);
            pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
        }

        if (delta.LeftReleased)
        {
            var routeStart = Stopwatch.GetTimestamp();
            DispatchMouseUp(pointerTarget, delta.Current.PointerPosition, MouseButton.Left);
            pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
        }

        if (delta.WheelDelta != 0)
        {
            var routeStart = Stopwatch.GetTimestamp();
            DispatchMouseWheel(pointerTarget, delta.Current.PointerPosition, delta.WheelDelta);
            pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
        }
        _lastInputHoverUpdateMs = (double)hoverTicks * 1000d / Stopwatch.Frequency;
        _lastInputPointerRouteMs = (double)pointerRouteTicks * 1000d / Stopwatch.Frequency;
        _lastInputPointerDispatchMs = Stopwatch.GetElapsedTime(pointerStart).TotalMilliseconds;

        if (delta.IsEmpty)
        {
            return;
        }

        var keyStart = Stopwatch.GetTimestamp();
        for (var i = 0; i < delta.PressedKeys.Count; i++)
        {
            DispatchKeyDown(delta.PressedKeys[i], _inputState.CurrentModifiers);
        }

        for (var i = 0; i < delta.ReleasedKeys.Count; i++)
        {
            DispatchKeyUp(delta.ReleasedKeys[i], _inputState.CurrentModifiers);
        }
        _lastInputKeyDispatchMs = Stopwatch.GetElapsedTime(keyStart).TotalMilliseconds;

        var textStart = Stopwatch.GetTimestamp();
        for (var i = 0; i < delta.TextInput.Count; i++)
        {
            DispatchTextInput(delta.TextInput[i]);
        }
        _lastInputTextDispatchMs = Stopwatch.GetElapsedTime(textStart).TotalMilliseconds;
    }

    private UIElement? ResolvePointerTarget(InputDelta delta)
    {
        if (_inputState.CapturedPointerElement != null)
        {
            return _inputState.CapturedPointerElement;
        }

        var requiresPreciseTarget = delta.LeftPressed || delta.LeftReleased;
        if (!requiresPreciseTarget)
        {
            // CPU-first path: keep current hover target during high-frequency move/wheel
            // and only re-resolve precisely on button transitions.
            if (_inputState.HoveredElement != null)
            {
                return _inputState.HoveredElement;
            }

            if (ForceBypassMoveHitTest || string.Equals(
                    Environment.GetEnvironmentVariable("INKKSLINGER_BYPASS_MOVE_HITTEST"),
                    "1",
                    StringComparison.Ordinal))
            {
                return _inputState.HoveredElement;
            }
        }

        if (!requiresPreciseTarget &&
            !delta.PointerMoved &&
            delta.WheelDelta == 0)
        {
            return _inputState.HoveredElement;
        }

        _lastInputHitTestCount++;
        return VisualTreeHelper.HitTest(_visualRoot, delta.Current.PointerPosition);
    }

    private void UpdateHover(UIElement? hovered)
    {
        if (ReferenceEquals(_inputState.HoveredElement, hovered))
        {
            return;
        }

        if (_inputState.HoveredElement is TextBox oldTextBox)
        {
            oldTextBox.SetMouseOverFromInput(false);
        }

        if (_inputState.HoveredElement is Button oldButton)
        {
            oldButton.SetMouseOverFromInput(false);
        }

        _inputState.HoveredElement = hovered;
        RefreshCachedWheelTargets(hovered);
        if (hovered is TextBox newTextBox)
        {
            newTextBox.SetMouseOverFromInput(true);
        }

        if (hovered is Button newButton)
        {
            newButton.SetMouseOverFromInput(true);
        }
    }

    private void DispatchPointerMove(UIElement? target, Vector2 pointerPosition)
    {
        var routedTarget = _inputState.CapturedPointerElement ?? target;
        if (routedTarget == null)
        {
            return;
        }

        _lastInputPointerEventCount++;
        _lastInputRoutedEventCount += 2;
        routedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseMoveEvent, new MouseRoutedEventArgs(UIElement.PreviewMouseMoveEvent, pointerPosition, MouseButton.Left));
        routedTarget.RaiseRoutedEventInternal(UIElement.MouseMoveEvent, new MouseRoutedEventArgs(UIElement.MouseMoveEvent, pointerPosition, MouseButton.Left));

        if (_inputState.CapturedPointerElement is TextBox dragTextBox)
        {
            dragTextBox.HandlePointerMoveFromInput(pointerPosition);
        }
    }

    private void DispatchMouseDown(UIElement? target, Vector2 pointerPosition, MouseButton button)
    {
        if (target == null)
        {
            return;
        }

        _lastInputPointerEventCount++;
        _lastInputRoutedEventCount += 2;
        target.RaiseRoutedEventInternal(UIElement.PreviewMouseDownEvent, new MouseRoutedEventArgs(UIElement.PreviewMouseDownEvent, pointerPosition, button));
        target.RaiseRoutedEventInternal(UIElement.MouseDownEvent, new MouseRoutedEventArgs(UIElement.MouseDownEvent, pointerPosition, button));
        SetFocus(target);

        if (target is Button pressedButton)
        {
            pressedButton.SetPressedFromInput(true);
            CapturePointer(target);
        }
        else if (target is TextBox textBox)
        {
            textBox.HandlePointerDownFromInput(pointerPosition, extendSelection: (_inputState.CurrentModifiers & ModifierKeys.Shift) != 0);
            CapturePointer(target);
        }
    }

    private void DispatchMouseUp(UIElement? target, Vector2 pointerPosition, MouseButton button)
    {
        var routedTarget = _inputState.CapturedPointerElement ?? target;
        if (routedTarget == null)
        {
            return;
        }

        _lastInputPointerEventCount++;
        _lastInputRoutedEventCount += 2;
        routedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseUpEvent, new MouseRoutedEventArgs(UIElement.PreviewMouseUpEvent, pointerPosition, button));
        routedTarget.RaiseRoutedEventInternal(UIElement.MouseUpEvent, new MouseRoutedEventArgs(UIElement.MouseUpEvent, pointerPosition, button));

        if (_inputState.CapturedPointerElement is Button pressedButton)
        {
            var shouldInvoke = ReferenceEquals(target, pressedButton);
            pressedButton.SetPressedFromInput(false);
            if (shouldInvoke)
            {
                pressedButton.InvokeFromInput();
            }
        }
        else if (_inputState.CapturedPointerElement is TextBox textBox)
        {
            textBox.HandlePointerUpFromInput();
        }

        ReleasePointer(_inputState.CapturedPointerElement);
    }

    private void DispatchMouseWheel(UIElement? target, Vector2 pointerPosition, int delta)
    {
        var resolvedTarget = target ?? _inputState.HoveredElement;
        if (resolvedTarget == null)
        {
            return;
        }

        _lastInputPointerEventCount++;
        _lastInputRoutedEventCount += 2;
        resolvedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseWheelEvent, new MouseWheelRoutedEventArgs(UIElement.PreviewMouseWheelEvent, pointerPosition, delta));
        resolvedTarget.RaiseRoutedEventInternal(UIElement.MouseWheelEvent, new MouseWheelRoutedEventArgs(UIElement.MouseWheelEvent, pointerPosition, delta));

        if (_cachedWheelTextBoxTarget != null &&
            _cachedWheelTextBoxTarget.HandleMouseWheelFromInput(delta))
        {
            return;
        }

        if (_cachedWheelScrollViewerTarget != null)
        {
            _ = _cachedWheelScrollViewerTarget.HandleMouseWheelFromInput(delta);
            return;
        }

        if (TryFindAncestor<TextBox>(resolvedTarget, out var textBox) &&
            textBox != null &&
            textBox.HandleMouseWheelFromInput(delta))
        {
            _cachedWheelTextBoxTarget = textBox;
            _cachedWheelScrollViewerTarget = null;
            return;
        }

        if (TryFindAncestor<ScrollViewer>(resolvedTarget, out var scrollViewer) && scrollViewer != null)
        {
            _cachedWheelScrollViewerTarget = scrollViewer;
            _cachedWheelTextBoxTarget = null;
            _ = scrollViewer.HandleMouseWheelFromInput(delta);
        }
    }

    private void RefreshCachedWheelTargets(UIElement? hovered)
    {
        _cachedWheelTextBoxTarget = null;
        _cachedWheelScrollViewerTarget = null;
        if (hovered == null)
        {
            return;
        }

        if (TryFindAncestor<TextBox>(hovered, out var textBox) && textBox != null)
        {
            _cachedWheelTextBoxTarget = textBox;
            return;
        }

        if (TryFindAncestor<ScrollViewer>(hovered, out var scrollViewer) && scrollViewer != null)
        {
            _cachedWheelScrollViewerTarget = scrollViewer;
        }
    }

    private void DispatchKeyDown(Keys key, ModifierKeys modifiers)
    {
        var target = _inputState.FocusedElement ?? _visualRoot;
        _lastInputKeyEventCount++;
        _lastInputRoutedEventCount += 2;
        target.RaiseRoutedEventInternal(UIElement.PreviewKeyDownEvent, new KeyRoutedEventArgs(UIElement.PreviewKeyDownEvent, key, modifiers));
        target.RaiseRoutedEventInternal(UIElement.KeyDownEvent, new KeyRoutedEventArgs(UIElement.KeyDownEvent, key, modifiers));

        if ((modifiers & ModifierKeys.Alt) != 0 && key is >= Keys.A and <= Keys.Z)
        {
            var accessKey = (char)('A' + (int)(key - Keys.A));
            if (TryHandleMenuAccessKey(accessKey))
            {
                return;
            }
        }

        if (_inputState.FocusedElement is TextBox textBox && textBox.HandleKeyDownFromInput(key, modifiers))
        {
            return;
        }

        if (_inputState.FocusedElement is Button button && (key == Keys.Enter || key == Keys.Space))
        {
            button.InvokeFromInput();
            return;
        }

        if (_inputState.FocusedElement is Menu focusedMenu && focusedMenu.TryHandleKeyDownFromInput(key))
        {
            return;
        }

        _ = InputGestureService.Execute(key, modifiers);
    }

    private void DispatchKeyUp(Keys key, ModifierKeys modifiers)
    {
        var target = _inputState.FocusedElement ?? _visualRoot;
        _lastInputKeyEventCount++;
        _lastInputRoutedEventCount += 2;
        target.RaiseRoutedEventInternal(UIElement.PreviewKeyUpEvent, new KeyRoutedEventArgs(UIElement.PreviewKeyUpEvent, key, modifiers));
        target.RaiseRoutedEventInternal(UIElement.KeyUpEvent, new KeyRoutedEventArgs(UIElement.KeyUpEvent, key, modifiers));
    }

    private void DispatchTextInput(char character)
    {
        var focused = _inputState.FocusedElement;
        if (focused == null)
        {
            return;
        }

        _lastInputTextEventCount++;
        _lastInputRoutedEventCount += 2;
        focused.RaiseRoutedEventInternal(UIElement.PreviewTextInputEvent, new TextInputRoutedEventArgs(UIElement.PreviewTextInputEvent, character));
        focused.RaiseRoutedEventInternal(UIElement.TextInputEvent, new TextInputRoutedEventArgs(UIElement.TextInputEvent, character));

        if (focused is TextBox textBox)
        {
            _ = textBox.HandleTextInputFromInput(character);
        }
    }

    private void SetFocus(UIElement? element)
    {
        if (ReferenceEquals(_inputState.FocusedElement, element))
        {
            return;
        }

        var old = _inputState.FocusedElement;
        _inputState.FocusedElement = element;
        FocusManager.SetFocus(element);

        if (old is TextBox oldTextBox)
        {
            oldTextBox.SetFocusedFromInput(false);
            old.RaiseRoutedEventInternal(UIElement.LostFocusEvent, new FocusChangedRoutedEventArgs(UIElement.LostFocusEvent, old, element));
            _lastInputRoutedEventCount++;
        }

        if (element is TextBox newTextBox)
        {
            newTextBox.SetFocusedFromInput(true);
            element.RaiseRoutedEventInternal(UIElement.GotFocusEvent, new FocusChangedRoutedEventArgs(UIElement.GotFocusEvent, old, element));
            _lastInputRoutedEventCount++;
        }
    }

    private void CapturePointer(UIElement? element)
    {
        _inputState.CapturedPointerElement = element;
        FocusManager.CapturePointer(element);
    }

    private void ReleasePointer(UIElement? element)
    {
        if (!ReferenceEquals(_inputState.CapturedPointerElement, element))
        {
            return;
        }

        _inputState.CapturedPointerElement = null;
        FocusManager.ReleasePointer(element);
    }

    private bool TryHandleMenuAccessKey(char accessKey)
    {
        var menu = FindFirstVisualOfType<Menu>(_visualRoot);
        return menu != null && menu.TryHandleAccessKeyFromInput(accessKey);
    }

    private static ModifierKeys GetModifiers(KeyboardState keyboard)
    {
        var modifiers = ModifierKeys.None;
        if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
        {
            modifiers |= ModifierKeys.Shift;
        }

        if (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl))
        {
            modifiers |= ModifierKeys.Control;
        }

        if (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt))
        {
            modifiers |= ModifierKeys.Alt;
        }

        return modifiers;
    }

    private static bool TryFindAncestor<TElement>(UIElement start, out TElement? result)
        where TElement : UIElement
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is TElement typed)
            {
                result = typed;
                return true;
            }
        }

        result = null;
        return false;
    }

    private static TElement? FindFirstVisualOfType<TElement>(UIElement root)
        where TElement : UIElement
    {
        if (root is TElement match)
        {
            return match;
        }

        foreach (var child in root.GetVisualChildren())
        {
            var found = FindFirstVisualOfType<TElement>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
