using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private const double WheelPreciseRetargetCooldownMs = 90d;
    private const float WheelPointerMoveThresholdSquared = 4f;
    private const float ClickPointerReuseThresholdSquared = 9f;
    private string _lastPointerResolvePath = "None";
    private HitTestMetrics? _lastPointerResolveHitTestMetrics;

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
        var clickResolveHitTests = 0;
        var pointerResolveStart = Stopwatch.GetTimestamp();
        var clickHitTestsBeforeResolve = _lastInputHitTestCount;
        var pointerTarget = ResolvePointerTarget(delta);
        if (delta.LeftPressed || delta.LeftReleased)
        {
            clickResolveHitTests = Math.Max(0, _lastInputHitTestCount - clickHitTestsBeforeResolve);
        }
        _lastInputPointerTargetResolveMs = Stopwatch.GetElapsedTime(pointerResolveStart).TotalMilliseconds;
        var hoverTicks = 0L;
        var pointerRouteTicks = 0L;
        if (delta.PointerMoved)
        {
            var moveHitTestsBefore = _lastInputHitTestCount;
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

            ObserveMoveCpuPointerDispatch(_lastInputHitTestCount - moveHitTestsBefore);
        }

        if (delta.LeftPressed)
        {
            _lastClickDownTarget = pointerTarget;
            _lastClickDownPointerPosition = delta.Current.PointerPosition;
            _hasLastClickDownPointerPosition = true;
            var routeStart = Stopwatch.GetTimestamp();
            var clickHitTestsBefore = _lastInputHitTestCount;
            ListBoxSelectCPUDiagnostics.ObservePointerDownCandidate(
                pointerTarget,
                delta.Current.PointerPosition,
                _lastInputPointerTargetResolveMs,
                Math.Max(0, clickResolveHitTests),
                _lastPointerResolvePath,
                _lastPointerResolveHitTestMetrics);
            var clickDispatchStart = Stopwatch.GetTimestamp();
            DispatchMouseDown(pointerTarget, delta.Current.PointerPosition, MouseButton.Left);
            var clickHandleMs = Stopwatch.GetElapsedTime(clickDispatchStart).TotalMilliseconds;
            var clickHitTests = Math.Max(0, _lastInputHitTestCount - clickHitTestsBefore) + clickResolveHitTests;
            clickResolveHitTests = 0;
            ObserveClickCpuPointerDispatch(isDown: true, clickHitTests, clickHandleMs);
            pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
        }

        if (delta.LeftReleased)
        {
            var routeStart = Stopwatch.GetTimestamp();
            var clickHitTestsBefore = _lastInputHitTestCount;
            var clickDispatchStart = Stopwatch.GetTimestamp();
            DispatchMouseUp(pointerTarget, delta.Current.PointerPosition, MouseButton.Left);
            var clickHandleMs = Stopwatch.GetElapsedTime(clickDispatchStart).TotalMilliseconds;
            var clickHitTests = Math.Max(0, _lastInputHitTestCount - clickHitTestsBefore) + clickResolveHitTests;
            clickResolveHitTests = 0;
            ObserveClickCpuPointerDispatch(isDown: false, clickHitTests, clickHandleMs);
            _lastClickUpTarget = pointerTarget;
            _lastClickUpPointerPosition = delta.Current.PointerPosition;
            _hasLastClickUpPointerPosition = true;
            _lastClickDownTarget = null;
            _hasLastClickDownPointerPosition = false;
            pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
        }

        if (delta.WheelDelta != 0)
        {
            TraceWheelRouting($"InputDelta wheel detected: delta={delta.WheelDelta}, pointer={FormatWheelPointer(delta.Current.PointerPosition)}, pointerResolvePath={_lastPointerResolvePath}, pointerTarget={DescribeWheelElement(pointerTarget)}, hovered={DescribeWheelElement(_inputState.HoveredElement)}");
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
        _lastPointerResolvePath = "None";
        _lastPointerResolveHitTestMetrics = null;
        if (_inputState.CapturedPointerElement != null)
        {
            _lastPointerResolvePath = "Captured";
            if (delta.LeftPressed || delta.LeftReleased)
            {
                _clickCpuResolveCapturedCount++;
            }
            return _inputState.CapturedPointerElement;
        }

        if (delta.LeftPressed &&
            !delta.PointerMoved &&
            _lastClickUpTarget != null &&
            _hasLastClickUpPointerPosition &&
            Vector2.DistanceSquared(_lastClickUpPointerPosition, delta.Current.PointerPosition) <= ClickPointerReuseThresholdSquared)
        {
            _lastPointerResolvePath = "ReuseLastClickUp";
            _clickCpuResolveCachedCount++;
            return _lastClickUpTarget;
        }

        if (delta.LeftReleased &&
            !delta.PointerMoved &&
            _lastClickDownTarget != null &&
            _hasLastClickDownPointerPosition &&
            Vector2.DistanceSquared(_lastClickDownPointerPosition, delta.Current.PointerPosition) <= ClickPointerReuseThresholdSquared)
        {
            _lastPointerResolvePath = "ReuseLastClickDown";
            _clickCpuResolveCachedCount++;
            return _lastClickDownTarget;
        }

        var requiresPreciseTarget = delta.LeftPressed || delta.LeftReleased;
        var pointerPosition = delta.Current.PointerPosition;
        if (!requiresPreciseTarget)
        {
            // CPU-first path: keep current hover target during high-frequency move/wheel
            // and only re-resolve precisely on button transitions.
            if (_inputState.HoveredElement != null)
            {
                _lastPointerResolvePath = "HoverReuse";
                return _inputState.HoveredElement;
            }

            if ((ForceBypassMoveHitTest || string.Equals(
                    Environment.GetEnvironmentVariable("INKKSLINGER_BYPASS_MOVE_HITTEST"),
                    "1",
                    StringComparison.Ordinal)) &&
                _inputState.HoveredElement != null)
            {
                _lastPointerResolvePath = "HoverBypass";
                return _inputState.HoveredElement;
            }
        }

        if (!requiresPreciseTarget &&
            !delta.PointerMoved &&
            delta.WheelDelta == 0)
        {
            _lastPointerResolvePath = "HoverNoInput";
            return _inputState.HoveredElement;
        }

        if (requiresPreciseTarget)
        {
            if (TryResolvePreciseClickTargetWithinAnchorSubtree(_cachedClickTarget, pointerPosition, out var cachedAnchorTarget) &&
                cachedAnchorTarget != null)
            {
                _lastPointerResolvePath = "CachedAnchorSubtreeHitTest";
                _clickCpuResolveCachedCount++;
                return cachedAnchorTarget;
            }

            if (_cachedClickTarget != null &&
                TryResolveClickTargetFromCandidate(_cachedClickTarget, pointerPosition, out var cachedClickTarget) &&
                cachedClickTarget != null)
            {
                _lastPointerResolvePath = "CachedClickCandidate";
                _clickCpuResolveCachedCount++;
                return cachedClickTarget;
            }

            if (TryResolveClickTargetFromHovered(pointerPosition, out var hoveredClickTarget) &&
                hoveredClickTarget != null)
            {
                _lastPointerResolvePath = "HoveredClickCandidate";
                _clickCpuResolveHoveredCount++;
                return hoveredClickTarget;
            }

            if (TryResolvePreciseClickTargetWithinHoveredSubtree(pointerPosition, out var hoveredSubtreeTarget) &&
                hoveredSubtreeTarget != null)
            {
                _lastPointerResolvePath = "HoveredSubtreeHitTest";
                _clickCpuResolveHoveredCount++;
                return hoveredSubtreeTarget;
            }
        }

        _lastInputHitTestCount++;
        if (requiresPreciseTarget)
        {
            _clickCpuResolveHitTestCount++;
        }
        _lastPointerResolvePath = "HitTest";
        var hit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition, out var metrics);
        _lastPointerResolveHitTestMetrics = metrics;
        return hit;
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
        RefreshCachedClickTarget(hovered);
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
        else if (_inputState.CapturedPointerElement is ScrollViewer dragScrollViewer)
        {
            dragScrollViewer.HandlePointerMoveFromInput(pointerPosition);
        }
    }

    private void DispatchMouseDown(UIElement? target, Vector2 pointerPosition, MouseButton button)
    {
        if (target == null)
        {
            return;
        }

        RefreshCachedClickTarget(target);

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
        else if (target is ScrollBar scrollBar &&
                 TryFindAncestor<ScrollViewer>(scrollBar, out var owningScrollViewer) &&
                 owningScrollViewer != null &&
                 owningScrollViewer.HandlePointerDownFromInput(pointerPosition))
        {
            CapturePointer(owningScrollViewer);
        }
        else if (target is ScrollViewer scrollViewer &&
                 scrollViewer.HandlePointerDownFromInput(pointerPosition))
        {
            CapturePointer(scrollViewer);
        }
    }

    private void DispatchMouseUp(UIElement? target, Vector2 pointerPosition, MouseButton button)
    {
        var routedTarget = _inputState.CapturedPointerElement ?? target;
        if (routedTarget == null)
        {
            return;
        }

        RefreshCachedClickTarget(routedTarget);

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
        else if (_inputState.CapturedPointerElement is ScrollViewer scrollViewer)
        {
            scrollViewer.HandlePointerUpFromInput();
        }

        ReleasePointer(_inputState.CapturedPointerElement);
    }

    private void DispatchMouseWheel(UIElement? target, Vector2 pointerPosition, int delta)
    {
        var wheelHitTestsBefore = _lastInputHitTestCount;
        var wheelHandleMs = 0d;
        var didPreciseRetarget = false;
        var resolvedTarget = target ?? _inputState.HoveredElement;
        if (resolvedTarget == null)
        {
            TraceWheelRouting($"DispatchMouseWheel ignored: no resolved target, delta={delta}, pointer={FormatWheelPointer(pointerPosition)}");
            return;
        }

        TraceWheelRouting($"DispatchMouseWheel start: delta={delta}, pointer={FormatWheelPointer(pointerPosition)}, incomingTarget={DescribeWheelElement(target)}, hovered={DescribeWheelElement(_inputState.HoveredElement)}, cachedTextBox={DescribeWheelElement(_cachedWheelTextBoxTarget)}, cachedScrollViewer={DescribeWheelElement(_cachedWheelScrollViewerTarget)}");

        EnsureCachedWheelTargetsAreCurrent(pointerPosition);
        if (_cachedWheelTextBoxTarget != null)
        {
            resolvedTarget = _cachedWheelTextBoxTarget;
        }
        else if (_cachedWheelScrollViewerTarget != null)
        {
            resolvedTarget = _cachedWheelScrollViewerTarget;
        }

        var hasCachedWheelTarget = _cachedWheelTextBoxTarget != null || _cachedWheelScrollViewerTarget != null;
        var hasWheelCapableAncestor = TryFindAncestor<TextBox>(resolvedTarget, out _) ||
                                      TryFindAncestor<ScrollViewer>(resolvedTarget, out _);
        var pointerInsideResolvedTarget = PointerLikelyInsideElement(resolvedTarget, pointerPosition);
        var pointerInsideCachedTarget =
            (_cachedWheelTextBoxTarget != null && PointerLikelyInsideElement(_cachedWheelTextBoxTarget, pointerPosition)) ||
            (_cachedWheelScrollViewerTarget != null && PointerLikelyInsideElement(_cachedWheelScrollViewerTarget, pointerPosition));
        var needsPreciseTarget = (!pointerInsideResolvedTarget && !pointerInsideCachedTarget) ||
                                 (!hasCachedWheelTarget && !hasWheelCapableAncestor);
        var pointerMovedSinceLastWheel = !_hasLastWheelPointerPosition ||
                                         Vector2.DistanceSquared(_lastWheelPointerPosition, pointerPosition) > WheelPointerMoveThresholdSquared;
        var cooldownElapsed = _lastWheelPreciseRetargetTimestamp == 0L ||
                              Stopwatch.GetElapsedTime(_lastWheelPreciseRetargetTimestamp).TotalMilliseconds >= WheelPreciseRetargetCooldownMs;
        var cachedViewerAtEdge = _cachedWheelScrollViewerTarget != null &&
                                 !CanScrollViewerInWheelDirection(_cachedWheelScrollViewerTarget, delta);
        var shouldPreciseRetarget = needsPreciseTarget &&
                                    (!hasCachedWheelTarget || pointerMovedSinceLastWheel || cooldownElapsed || cachedViewerAtEdge);
        TraceWheelRouting($"DispatchMouseWheel resolve: resolvedTarget={DescribeWheelElement(resolvedTarget)}, needsPreciseTarget={needsPreciseTarget}, hasCachedWheelTarget={hasCachedWheelTarget}, pointerInsideResolved={pointerInsideResolvedTarget}, pointerInsideCached={pointerInsideCachedTarget}, pointerMovedSinceLastWheel={pointerMovedSinceLastWheel}, cooldownElapsed={cooldownElapsed}, cachedViewerAtEdge={cachedViewerAtEdge}, shouldPreciseRetarget={shouldPreciseRetarget}");

        if (shouldPreciseRetarget)
        {
            if (TryFindWheelCapableTargetAtPointer(pointerPosition, out var fastWheelTarget) &&
                fastWheelTarget != null)
            {
                resolvedTarget = fastWheelTarget;
                RefreshCachedWheelTargets(fastWheelTarget);
                TraceWheelRouting($"DispatchMouseWheel fast-retarget hit: target={DescribeWheelElement(fastWheelTarget)}");
            }
            else
            {
                _lastInputHitTestCount++;
                didPreciseRetarget = true;
                var preciseTarget = VisualTreeHelper.HitTest(_visualRoot, pointerPosition);
                if (preciseTarget != null)
                {
                    resolvedTarget = preciseTarget;
                    RefreshCachedWheelTargets(preciseTarget);
                    TraceWheelRouting($"DispatchMouseWheel precise-hit-test target={DescribeWheelElement(preciseTarget)}");
                }
                else
                {
                    TraceWheelRouting("DispatchMouseWheel precise-hit-test returned null");
                }
            }

            _lastWheelPreciseRetargetTimestamp = Stopwatch.GetTimestamp();
        }

        _lastInputPointerEventCount++;
        if (HasWheelRoutedEventHandlers(resolvedTarget))
        {
            _lastInputRoutedEventCount += 2;
            resolvedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseWheelEvent, new MouseWheelRoutedEventArgs(UIElement.PreviewMouseWheelEvent, pointerPosition, delta));
            resolvedTarget.RaiseRoutedEventInternal(UIElement.MouseWheelEvent, new MouseWheelRoutedEventArgs(UIElement.MouseWheelEvent, pointerPosition, delta));
            TraceWheelRouting($"DispatchMouseWheel routed events raised on {DescribeWheelElement(resolvedTarget)}");
        }

        if (_cachedWheelTextBoxTarget != null &&
            _cachedWheelTextBoxTarget.HandleMouseWheelFromInput(delta))
        {
            TraceWheelRouting($"DispatchMouseWheel handled by cached TextBox {DescribeWheelElement(_cachedWheelTextBoxTarget)}");
            TrackWheelPointerPosition(pointerPosition);
            ObserveScrollCpuWheelDispatch(delta, didPreciseRetarget, _lastInputHitTestCount - wheelHitTestsBefore, wheelHandleMs);
            return;
        }

        if (_cachedWheelScrollViewerTarget != null)
        {
            var cachedViewer = _cachedWheelScrollViewerTarget;
            var beforeHorizontal = cachedViewer.HorizontalOffset;
            var beforeVertical = cachedViewer.VerticalOffset;
            var wheelHandleStart = Stopwatch.GetTimestamp();
            var handled = cachedViewer.HandleMouseWheelFromInput(delta);
            wheelHandleMs = Stopwatch.GetElapsedTime(wheelHandleStart).TotalMilliseconds;
            TraceWheelRouting($"DispatchMouseWheel cached ScrollViewer attempt: target={DescribeWheelElement(cachedViewer)}, handled={handled}, offsets(before=({beforeHorizontal:0.###},{beforeVertical:0.###}), after=({cachedViewer.HorizontalOffset:0.###},{cachedViewer.VerticalOffset:0.###})), handleMs={wheelHandleMs:0.###}");
            TrackWheelPointerPosition(pointerPosition);
            ObserveScrollCpuWheelDispatch(delta, didPreciseRetarget, _lastInputHitTestCount - wheelHitTestsBefore, wheelHandleMs);
            return;
        }

        if (TryFindAncestor<TextBox>(resolvedTarget, out var textBox) &&
            textBox != null &&
            textBox.HandleMouseWheelFromInput(delta))
        {
            _cachedWheelTextBoxTarget = textBox;
            _cachedWheelScrollViewerTarget = null;
            TraceWheelRouting($"DispatchMouseWheel handled by ancestor TextBox {DescribeWheelElement(textBox)}");
            TrackWheelPointerPosition(pointerPosition);
            ObserveScrollCpuWheelDispatch(delta, didPreciseRetarget, _lastInputHitTestCount - wheelHitTestsBefore, wheelHandleMs);
            return;
        }

        if (TryFindAncestor<ScrollViewer>(resolvedTarget, out var scrollViewer) && scrollViewer != null)
        {
            _cachedWheelScrollViewerTarget = scrollViewer;
            _cachedWheelTextBoxTarget = null;
            var beforeHorizontal = scrollViewer.HorizontalOffset;
            var beforeVertical = scrollViewer.VerticalOffset;
            var wheelHandleStart = Stopwatch.GetTimestamp();
            var handled = scrollViewer.HandleMouseWheelFromInput(delta);
            wheelHandleMs = Stopwatch.GetElapsedTime(wheelHandleStart).TotalMilliseconds;
            TraceWheelRouting($"DispatchMouseWheel handled by ancestor ScrollViewer {DescribeWheelElement(scrollViewer)} handled={handled}, offsets(before=({beforeHorizontal:0.###},{beforeVertical:0.###}), after=({scrollViewer.HorizontalOffset:0.###},{scrollViewer.VerticalOffset:0.###})), handleMs={wheelHandleMs:0.###}");
        }
        else
        {
            TraceWheelRouting($"DispatchMouseWheel ended with no wheel-capable ancestor for resolved target {DescribeWheelElement(resolvedTarget)}");
        }

        TrackWheelPointerPosition(pointerPosition);
        ObserveScrollCpuWheelDispatch(delta, didPreciseRetarget, _lastInputHitTestCount - wheelHitTestsBefore, wheelHandleMs);
    }

    private void EnsureCachedWheelTargetsAreCurrent(Vector2 pointerPosition)
    {
        if (_cachedWheelTextBoxTarget != null &&
            !PointerLikelyInsideElement(_cachedWheelTextBoxTarget, pointerPosition))
        {
            TraceWheelRouting($"EnsureCachedWheelTargetsAreCurrent cleared cached TextBox {DescribeWheelElement(_cachedWheelTextBoxTarget)} for pointer {FormatWheelPointer(pointerPosition)}");
            _cachedWheelTextBoxTarget = null;
        }

        if (_cachedWheelScrollViewerTarget != null &&
            !PointerLikelyInsideElement(_cachedWheelScrollViewerTarget, pointerPosition))
        {
            TraceWheelRouting($"EnsureCachedWheelTargetsAreCurrent cleared cached ScrollViewer {DescribeWheelElement(_cachedWheelScrollViewerTarget)} for pointer {FormatWheelPointer(pointerPosition)}");
            _cachedWheelScrollViewerTarget = null;
        }
    }

    private static bool PointerLikelyInsideElement(UIElement element, Vector2 pointerPosition)
    {
        return element.HitTest(pointerPosition);
    }

    private static bool CanScrollViewerInWheelDirection(ScrollViewer viewer, int delta)
    {
        var maxVertical = MathF.Max(0f, viewer.ExtentHeight - viewer.ViewportHeight);
        if (delta < 0)
        {
            return viewer.VerticalOffset < maxVertical - 0.01f;
        }

        if (delta > 0)
        {
            return viewer.VerticalOffset > 0.01f;
        }

        return false;
    }

    private static bool HasWheelRoutedEventHandlers(UIElement target)
    {
        for (var current = target; current != null; current = current.VisualParent)
        {
            if (current.HasRoutedHandlerForEvent(UIElement.PreviewMouseWheelEvent) ||
                current.HasRoutedHandlerForEvent(UIElement.MouseWheelEvent) ||
                EventManager.HasClassHandlers(current.GetType(), UIElement.PreviewMouseWheelEvent) ||
                EventManager.HasClassHandlers(current.GetType(), UIElement.MouseWheelEvent))
            {
                return true;
            }
        }

        return false;
    }

    private void TrackWheelPointerPosition(Vector2 pointerPosition)
    {
        _lastWheelPointerPosition = pointerPosition;
        _hasLastWheelPointerPosition = true;
    }

    private bool TryFindWheelCapableTargetAtPointer(Vector2 pointerPosition, out UIElement? target)
    {
        var bestDepth = -1;
        UIElement? bestTarget = null;
        FindWheelCapableTargetAtPointer(_visualRoot, pointerPosition, depth: 0, ref bestDepth, ref bestTarget);
        target = bestTarget;
        return target != null;
    }

    private static void FindWheelCapableTargetAtPointer(
        UIElement element,
        Vector2 pointerPosition,
        int depth,
        ref int bestDepth,
        ref UIElement? bestTarget)
    {
        if (element is TextBox or ScrollViewer)
        {
            if (element.HitTest(pointerPosition) && depth >= bestDepth)
            {
                bestDepth = depth;
                bestTarget = element;
            }
        }

        foreach (var child in element.GetVisualChildren())
        {
            FindWheelCapableTargetAtPointer(child, pointerPosition, depth + 1, ref bestDepth, ref bestTarget);
        }
    }

    private void RefreshCachedWheelTargets(UIElement? hovered)
    {
        var previousTextBox = _cachedWheelTextBoxTarget;
        var previousScrollViewer = _cachedWheelScrollViewerTarget;
        _cachedWheelTextBoxTarget = null;
        _cachedWheelScrollViewerTarget = null;
        if (hovered == null)
        {
            TraceWheelRouting($"RefreshCachedWheelTargets cleared caches from hovered=null, previousTextBox={DescribeWheelElement(previousTextBox)}, previousScrollViewer={DescribeWheelElement(previousScrollViewer)}");
            return;
        }

        if (TryFindAncestor<TextBox>(hovered, out var textBox) && textBox != null)
        {
            _cachedWheelTextBoxTarget = textBox;
            TraceWheelRouting($"RefreshCachedWheelTargets hovered={DescribeWheelElement(hovered)} -> cached TextBox {DescribeWheelElement(textBox)}, previousTextBox={DescribeWheelElement(previousTextBox)}, previousScrollViewer={DescribeWheelElement(previousScrollViewer)}");
            return;
        }

        if (TryFindAncestor<ScrollViewer>(hovered, out var scrollViewer) && scrollViewer != null)
        {
            _cachedWheelScrollViewerTarget = scrollViewer;
            TraceWheelRouting($"RefreshCachedWheelTargets hovered={DescribeWheelElement(hovered)} -> cached ScrollViewer {DescribeWheelElement(scrollViewer)}, previousTextBox={DescribeWheelElement(previousTextBox)}, previousScrollViewer={DescribeWheelElement(previousScrollViewer)}");
            return;
        }

        TraceWheelRouting($"RefreshCachedWheelTargets hovered={DescribeWheelElement(hovered)} found no wheel-capable ancestor, previousTextBox={DescribeWheelElement(previousTextBox)}, previousScrollViewer={DescribeWheelElement(previousScrollViewer)}");
    }

    private bool TryResolveClickTargetFromHovered(Vector2 pointerPosition, out UIElement? target)
    {
        return TryResolveClickTargetFromCandidate(_inputState.HoveredElement, pointerPosition, out target);
    }

    private bool TryResolvePreciseClickTargetWithinHoveredSubtree(Vector2 pointerPosition, out UIElement? target)
    {
        return TryResolvePreciseClickTargetWithinAnchorSubtree(_inputState.HoveredElement, pointerPosition, out target);
    }

    private bool TryResolvePreciseClickTargetWithinAnchorSubtree(UIElement? anchor, Vector2 pointerPosition, out UIElement? target)
    {
        target = null;
        if (anchor == null || !PointerLikelyInsideElement(anchor, pointerPosition))
        {
            return false;
        }

        _lastInputHitTestCount++;
        _clickCpuResolveHitTestCount++;
        var hit = VisualTreeHelper.HitTest(anchor, pointerPosition, out var metrics);
        _lastPointerResolveHitTestMetrics = metrics;
        if (hit == null)
        {
            return false;
        }

        target = hit;
        return true;
    }

    private static bool TryResolveClickTargetFromCandidate(UIElement? candidate, Vector2 pointerPosition, out UIElement? target)
    {
        target = null;
        if (candidate == null || !PointerLikelyInsideElement(candidate, pointerPosition))
        {
            return false;
        }

        // Avoid pinning click targeting to ScrollViewer via cached-hover reuse:
        // for item controls hosted inside ScrollViewer (ListBox/ListView), this can
        // bypass child hit-testing and break per-item click semantics.
        if (candidate is ScrollViewer)
        {
            return false;
        }

        for (var current = candidate; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (IsClickCapableElement(current))
            {
                target = current;
                return true;
            }
        }

        return false;
    }

    private static bool IsClickCapableElement(UIElement element)
    {
        if (element is Button or TextBox or ScrollViewer or ScrollBar)
        {
            return true;
        }

        return element.HasRoutedHandlerForEvent(UIElement.PreviewMouseDownEvent) ||
               element.HasRoutedHandlerForEvent(UIElement.MouseDownEvent) ||
               element.HasRoutedHandlerForEvent(UIElement.PreviewMouseUpEvent) ||
               element.HasRoutedHandlerForEvent(UIElement.MouseUpEvent) ||
               EventManager.HasClassHandlers(element.GetType(), UIElement.PreviewMouseDownEvent) ||
               EventManager.HasClassHandlers(element.GetType(), UIElement.MouseDownEvent) ||
               EventManager.HasClassHandlers(element.GetType(), UIElement.PreviewMouseUpEvent) ||
               EventManager.HasClassHandlers(element.GetType(), UIElement.MouseUpEvent);
    }

    private void RefreshCachedClickTarget(UIElement? anchor)
    {
        _cachedClickTarget = null;
        if (anchor == null)
        {
            return;
        }

        for (var current = anchor; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (IsClickCapableElement(current))
            {
                _cachedClickTarget = current;
                return;
            }
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

        _ = InputGestureService.Execute(key, modifiers, _inputState.FocusedElement, _visualRoot);
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
