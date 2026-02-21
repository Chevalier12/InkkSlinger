using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    private readonly record struct ContextMenuOpenPerfSnapshot(
        int MenuMeasureInvalidationCount,
        int MenuArrangeInvalidationCount,
        int MenuRenderInvalidationCount,
        int RootMeasureInvalidationCount,
        int RootArrangeInvalidationCount,
        int RootRenderInvalidationCount);

    private const double WheelPreciseRetargetCooldownMs = 90d;
    private const float WheelPointerMoveThresholdSquared = 4f;
    private const float ClickPointerReuseThresholdSquared = 9f;
    private string _lastPointerResolvePath = "None";
    private HitTestMetrics? _lastPointerResolveHitTestMetrics;
    private ContextMenu? _lastKnownOpenContextMenu;

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
        _inputState.LastPointerPosition = delta.Current.PointerPosition;
        var pointerStart = Stopwatch.GetTimestamp();
        _inputState.CurrentModifiers = GetModifiers(delta.Current.Keyboard);
        var clickResolveHitTests = 0;
        var pointerResolveStart = Stopwatch.GetTimestamp();
        var clickHitTestsBeforeResolve = _lastInputHitTestCount;
        var pointerTarget = ResolvePointerTarget(delta);
        if (delta.LeftPressed || delta.LeftReleased || delta.RightPressed || delta.RightReleased)
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
            var contextMenuMoveHandled = false;
            if (_inputState.CapturedPointerElement == null &&
                TryResolveContextMenuMenuItemTarget(delta.Current.PointerPosition, pointerTarget, out var contextMenuHoverItem, out var contextMenuResolveStats))
            {
                ContextMenuHoverDiagnostics.ObservePointerHover(
                    contextMenuHoverItem,
                    delta.Current.PointerPosition,
                    pointerTarget,
                    contextMenuResolveStats.Path,
                    "BeforeMove");
                var hoverHandleStart = Stopwatch.GetTimestamp();
                contextMenuHoverItem.HandlePointerMoveFromInput();
                var hoverHandleMs = Stopwatch.GetElapsedTime(hoverHandleStart).TotalMilliseconds;
                ContextMenuHoverDiagnostics.ObservePointerHover(
                    contextMenuHoverItem,
                    delta.Current.PointerPosition,
                    pointerTarget,
                    contextMenuResolveStats.Path,
                    "AfterMove");
                ContextMenuCpuDiagnostics.ObserveHoverDispatch(contextMenuResolveStats, hoverHandleMs);
                contextMenuMoveHandled = true;
            }

            var hasOpenContextMenu = TryFindOpenContextMenu(out _) || HasAnyOpenContextMenu(_visualRoot);
            var shouldRouteMove = !contextMenuMoveHandled &&
                                  (hasOpenContextMenu ||
                                  !ReferenceEquals(previousHovered, _inputState.HoveredElement) ||
                                  _inputState.CapturedPointerElement != null ||
                                  string.Equals(Environment.GetEnvironmentVariable("INKKSLINGER_ALWAYS_ROUTE_MOUSEMOVE"), "1", StringComparison.Ordinal));
            if (shouldRouteMove)
            {
                var routeStart = Stopwatch.GetTimestamp();
                DispatchPointerMove(pointerTarget, delta.Current.PointerPosition);
                pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
            }

            ObserveFrameLatencyMoveEvent();
            ObserveMoveCpuPointerDispatch(_lastInputHitTestCount - moveHitTestsBefore);

            if (UseSoftwareCursor)
            {
                _mustDrawNextFrame = true;
            }
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
            ObserveFrameLatencyClickEvent();
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
            ObserveFrameLatencyClickEvent();
            ObserveClickCpuPointerDispatch(isDown: false, clickHitTests, clickHandleMs);
            _lastClickUpTarget = pointerTarget;
            _lastClickUpPointerPosition = delta.Current.PointerPosition;
            _hasLastClickUpPointerPosition = true;
            _lastClickDownTarget = null;
            _hasLastClickDownPointerPosition = false;
            pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
        }

        if (delta.RightPressed)
        {
            var routeStart = Stopwatch.GetTimestamp();
            var clickHitTestsBefore = _lastInputHitTestCount;
            var clickDispatchStart = Stopwatch.GetTimestamp();
            DispatchMouseDown(pointerTarget, delta.Current.PointerPosition, MouseButton.Right);
            var clickHandleMs = Stopwatch.GetElapsedTime(clickDispatchStart).TotalMilliseconds;
            var clickHitTests = Math.Max(0, _lastInputHitTestCount - clickHitTestsBefore) + clickResolveHitTests;
            clickResolveHitTests = 0;
            ObserveFrameLatencyClickEvent();
            ObserveClickCpuPointerDispatch(isDown: true, clickHitTests, clickHandleMs);
            pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
        }

        if (delta.RightReleased)
        {
            var routeStart = Stopwatch.GetTimestamp();
            var clickHitTestsBefore = _lastInputHitTestCount;
            var clickDispatchStart = Stopwatch.GetTimestamp();
            DispatchMouseUp(pointerTarget, delta.Current.PointerPosition, MouseButton.Right);
            var clickHandleMs = Stopwatch.GetElapsedTime(clickDispatchStart).TotalMilliseconds;
            var clickHitTests = Math.Max(0, _lastInputHitTestCount - clickHitTestsBefore) + clickResolveHitTests;
            clickResolveHitTests = 0;
            ObserveFrameLatencyClickEvent();
            ObserveClickCpuPointerDispatch(isDown: false, clickHitTests, clickHandleMs);
            pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
        }

        if (delta.WheelDelta != 0)
        {
            ObserveFrameLatencyScrollEvent();
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
            IsElementConnectedToVisualRoot(_lastClickUpTarget) &&
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
            IsElementConnectedToVisualRoot(_lastClickDownTarget) &&
            _hasLastClickDownPointerPosition &&
            Vector2.DistanceSquared(_lastClickDownPointerPosition, delta.Current.PointerPosition) <= ClickPointerReuseThresholdSquared)
        {
            _lastPointerResolvePath = "ReuseLastClickDown";
            _clickCpuResolveCachedCount++;
            return _lastClickDownTarget;
        }

        var requiresPreciseTarget =
            delta.LeftPressed || delta.LeftReleased ||
            delta.RightPressed || delta.RightReleased;
        var pointerPosition = delta.Current.PointerPosition;
        var bypassClickTargetShortcuts = requiresPreciseTarget && ShouldBypassClickTargetShortcuts(pointerPosition);
        if (!requiresPreciseTarget)
        {
            if (TryFindOpenContextMenu(out _))
            {
                _lastInputHitTestCount++;
                _lastPointerResolvePath = "ContextMenuOpenHitTest";
                var overlayHit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition, out var overlayMetrics);
                _lastPointerResolveHitTestMetrics = overlayMetrics;
                return overlayHit;
            }

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

        if (requiresPreciseTarget && !bypassClickTargetShortcuts)
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
        _lastPointerResolvePath = bypassClickTargetShortcuts ? "OverlayBypassHitTest" : "HitTest";
        var hit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition, out var metrics);
        _lastPointerResolveHitTestMetrics = metrics;
        return hit;
    }

    private bool ShouldBypassClickTargetShortcuts(Vector2 pointerPosition)
    {
        // Cached click-target shortcuts are unsafe while any overlay is open because
        // submenus can render outside root overlay bounds.
        _ = pointerPosition;
        return TryGetTopOverlayCandidate(out _);
    }

    private void UpdateHover(UIElement? hovered)
    {
        if (ReferenceEquals(_inputState.HoveredElement, hovered))
        {
            return;
        }

        if (_inputState.HoveredElement is ITextInputControl oldTextInput)
        {
            oldTextInput.SetMouseOverFromInput(false);
        }

        if (_inputState.HoveredElement is Button oldButton)
        {
            oldButton.SetMouseOverFromInput(false);
        }

        _inputState.HoveredElement = hovered;
        RefreshCachedWheelTargets(hovered);
        RefreshCachedClickTarget(hovered);
        if (hovered is ITextInputControl newTextInput)
        {
            newTextInput.SetMouseOverFromInput(true);
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

        var dispatchStart = Stopwatch.GetTimestamp();
        ObserveAllocationGcInteraction("PointerMove");
        _lastInputPointerEventCount++;
        _lastInputRoutedEventCount += 2;
        ObserveInputRouteComplexity("Pointer", routedTarget, UIElement.PreviewMouseMoveEvent);
        routedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseMoveEvent, new MouseRoutedEventArgs(UIElement.PreviewMouseMoveEvent, pointerPosition, MouseButton.Left));
        ObserveInputRouteComplexity("Pointer", routedTarget, UIElement.MouseMoveEvent);
        routedTarget.RaiseRoutedEventInternal(UIElement.MouseMoveEvent, new MouseRoutedEventArgs(UIElement.MouseMoveEvent, pointerPosition, MouseButton.Left));

        if (_inputState.CapturedPointerElement is ITextInputControl dragTextInput)
        {
            dragTextInput.HandlePointerMoveFromInput(pointerPosition);
        }
        else if (_inputState.CapturedPointerElement is ScrollViewer dragScrollViewer)
        {
            dragScrollViewer.HandlePointerMoveFromInput(pointerPosition);
        }
        else if (_inputState.CapturedPointerElement is Slider dragSlider)
        {
            dragSlider.HandlePointerMoveFromInput(pointerPosition);
        }
        else if (_inputState.CapturedPointerElement is Popup dragPopup)
        {
            dragPopup.HandlePointerMoveFromInput(pointerPosition);
        }
        else if (_inputState.CapturedPointerElement == null)
        {
            var resolved = TryResolveContextMenuMenuItemTarget(pointerPosition, target, out var contextMenuMenuItem, out var resolveStats);
            if (resolved)
            {
                ContextMenuHoverDiagnostics.ObservePointerHover(
                    contextMenuMenuItem,
                    pointerPosition,
                    target,
                    _lastPointerResolvePath,
                    "BeforeMove");
                var hoverHandleStart = Stopwatch.GetTimestamp();
                contextMenuMenuItem.HandlePointerMoveFromInput();
                var hoverHandleMs = Stopwatch.GetElapsedTime(hoverHandleStart).TotalMilliseconds;
                ContextMenuHoverDiagnostics.ObservePointerHover(
                    contextMenuMenuItem,
                    pointerPosition,
                    target,
                    _lastPointerResolvePath,
                    "AfterMove");
                ContextMenuCpuDiagnostics.ObserveHoverDispatch(
                    resolveStats,
                    hoverHandleMs);
                return;
            }

            if (target is MenuItem menuItem)
            {
                menuItem.HandlePointerMoveFromInput();
                if (menuItem.OwnerMenu is { } ownerMenu)
                {
                    TrySynchronizeMenuFocusRestore(ownerMenu);
                }

                return;
            }
        }

        ObserveControlHotspotDispatch(routedTarget, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
    }

    private void DispatchMouseDown(UIElement? target, Vector2 pointerPosition, MouseButton button)
    {
        if (TryFindMenuInMenuMode(out var activeMenu) &&
            (target == null || !activeMenu.ContainsElement(target)))
        {
            activeMenu.CloseAllSubmenus(restoreFocus: true);
            TrySynchronizeMenuFocusRestore(activeMenu);
        }

        if (button == MouseButton.Left &&
            target is ComboBox preDismissComboBox &&
            preDismissComboBox.IsDropDownOpen &&
            preDismissComboBox.HandlePointerDownFromInput(pointerPosition))
        {
            SetFocus(preDismissComboBox);
            return;
        }

        _ = TryDismissOverlayOnOutsidePointerDown(pointerPosition, target);

        if (target == null)
        {
            return;
        }

        var dispatchStart = Stopwatch.GetTimestamp();
        ObserveAllocationGcInteraction("MouseDown");
        RefreshCachedClickTarget(target);

        _lastInputPointerEventCount++;
        _lastInputRoutedEventCount += 2;
        ObserveInputRouteComplexity("Pointer", target, UIElement.PreviewMouseDownEvent);
        target.RaiseRoutedEventInternal(UIElement.PreviewMouseDownEvent, new MouseRoutedEventArgs(UIElement.PreviewMouseDownEvent, pointerPosition, button));
        ObserveInputRouteComplexity("Pointer", target, UIElement.MouseDownEvent);
        target.RaiseRoutedEventInternal(UIElement.MouseDownEvent, new MouseRoutedEventArgs(UIElement.MouseDownEvent, pointerPosition, button));
        if (target is not Menu && target is not MenuItem)
        {
            SetFocus(target);
        }

        if (target is MenuItem menuItemTarget)
        {
            _ = menuItemTarget.HandlePointerDownFromInput();
            if (menuItemTarget.OwnerMenu is { } ownerMenu)
            {
                TrySynchronizeMenuFocusRestore(ownerMenu);
            }

            ObserveControlHotspotDispatch(target, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
            return;
        }

        if (button == MouseButton.Left &&
            TryResolveContextMenuMenuItemTarget(pointerPosition, target, out var contextMenuMenuItem))
        {
            _ = contextMenuMenuItem.HandlePointerDownFromInput();
            ObserveControlHotspotDispatch(contextMenuMenuItem, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
            return;
        }

        if (button == MouseButton.Left && target is Button pressedButton)
        {
            pressedButton.SetPressedFromInput(true);
            CapturePointer(target);
        }
        else if (button == MouseButton.Left && target is ComboBox comboBox &&
                 comboBox.HandlePointerDownFromInput(pointerPosition))
        {
        }
        else if (button == MouseButton.Left && target is Popup popup &&
                 popup.HandlePointerDownFromInput(pointerPosition))
        {
            CapturePointer(target);
        }
        else if (button == MouseButton.Left && target is Slider slider &&
                 slider.HandlePointerDownFromInput(pointerPosition))
        {
            CapturePointer(target);
        }
        else if (button == MouseButton.Left && target is ITextInputControl textInput)
        {
            textInput.HandlePointerDownFromInput(pointerPosition, extendSelection: (_inputState.CurrentModifiers & ModifierKeys.Shift) != 0);
            CapturePointer(target);
        }
        else if (button == MouseButton.Left && target is ScrollBar scrollBar &&
                 TryFindAncestor<ScrollViewer>(scrollBar, out var owningScrollViewer) &&
                 owningScrollViewer != null &&
                 owningScrollViewer.HandlePointerDownFromInput(pointerPosition))
        {
            CapturePointer(owningScrollViewer);
        }
        else if (button == MouseButton.Left && target is ScrollViewer scrollViewer &&
                 scrollViewer.HandlePointerDownFromInput(pointerPosition))
        {
            CapturePointer(scrollViewer);
        }

        ObserveControlHotspotDispatch(target, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
    }

    private void DispatchMouseUp(UIElement? target, Vector2 pointerPosition, MouseButton button)
    {
        var routedTarget = _inputState.CapturedPointerElement ?? target;
        if (routedTarget == null)
        {
            return;
        }

        var dispatchStart = Stopwatch.GetTimestamp();
        ObserveAllocationGcInteraction("MouseUp");
        RefreshCachedClickTarget(routedTarget);

        _lastInputPointerEventCount++;
        _lastInputRoutedEventCount += 2;
        ObserveInputRouteComplexity("Pointer", routedTarget, UIElement.PreviewMouseUpEvent);
        routedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseUpEvent, new MouseRoutedEventArgs(UIElement.PreviewMouseUpEvent, pointerPosition, button));
        ObserveInputRouteComplexity("Pointer", routedTarget, UIElement.MouseUpEvent);
        routedTarget.RaiseRoutedEventInternal(UIElement.MouseUpEvent, new MouseRoutedEventArgs(UIElement.MouseUpEvent, pointerPosition, button));

        if (_inputState.CapturedPointerElement is Button pressedButton && button == MouseButton.Left)
        {
            var shouldInvoke = ReferenceEquals(target, pressedButton);
            pressedButton.SetPressedFromInput(false);
            if (shouldInvoke)
            {
                pressedButton.InvokeFromInput();
            }
        }
        else if (_inputState.CapturedPointerElement is ITextInputControl textInput && button == MouseButton.Left)
        {
            textInput.HandlePointerUpFromInput();
        }
        else if (_inputState.CapturedPointerElement is ScrollViewer scrollViewer && button == MouseButton.Left)
        {
            scrollViewer.HandlePointerUpFromInput();
        }
        else if (_inputState.CapturedPointerElement is Slider slider && button == MouseButton.Left)
        {
            slider.HandlePointerUpFromInput();
        }
        else if (_inputState.CapturedPointerElement is Popup popup && button == MouseButton.Left)
        {
            popup.HandlePointerUpFromInput(pointerPosition);
        }
        else if (_inputState.CapturedPointerElement == null && target is MenuItem menuItemTarget)
        {
            _ = menuItemTarget.HandlePointerUpFromInput();
            if (menuItemTarget.OwnerMenu is { } ownerMenu)
            {
                TrySynchronizeMenuFocusRestore(ownerMenu);
            }
        }
        else if (button == MouseButton.Left &&
                 _inputState.CapturedPointerElement == null &&
                 TryResolveContextMenuMenuItemTarget(pointerPosition, target, out var contextMenuMenuItem))
        {
            var invokeStart = Stopwatch.GetTimestamp();
            var handled = contextMenuMenuItem.HandlePointerUpFromInput();
            ContextMenuCpuDiagnostics.ObserveInvoke(
                Stopwatch.GetElapsedTime(invokeStart).TotalMilliseconds,
                handled);
        }

        ReleasePointer(_inputState.CapturedPointerElement);

        if (button == MouseButton.Right)
        {
            _ = TryOpenContextMenuFromPointer(target, pointerPosition);
        }

        ObserveControlHotspotDispatch(routedTarget, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
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

        var dispatchStart = Stopwatch.GetTimestamp();
        ObserveAllocationGcInteraction("MouseWheel");
        TraceWheelRouting($"DispatchMouseWheel start: delta={delta}, pointer={FormatWheelPointer(pointerPosition)}, incomingTarget={DescribeWheelElement(target)}, hovered={DescribeWheelElement(_inputState.HoveredElement)}, cachedTextBox={DescribeWheelElement(_cachedWheelTextInputTarget)}, cachedScrollViewer={DescribeWheelElement(_cachedWheelScrollViewerTarget)}");

        EnsureCachedWheelTargetsAreCurrent(pointerPosition);
        if (_cachedWheelTextInputTarget != null)
        {
            resolvedTarget = _cachedWheelTextInputTarget;
        }
        else if (_cachedWheelScrollViewerTarget != null)
        {
            resolvedTarget = _cachedWheelScrollViewerTarget;
        }

        var hasCachedWheelTarget = _cachedWheelTextInputTarget != null || _cachedWheelScrollViewerTarget != null;
        var hasWheelCapableAncestor = TryFindWheelTextInputAncestor(resolvedTarget, out _) ||
                                      TryFindAncestor<ScrollViewer>(resolvedTarget, out _);
        var pointerInsideResolvedTarget = PointerLikelyInsideElement(resolvedTarget, pointerPosition);
        var pointerInsideCachedTarget =
            (_cachedWheelTextInputTarget != null && PointerLikelyInsideElement(_cachedWheelTextInputTarget, pointerPosition)) ||
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
            ObserveInputRouteComplexity("Wheel", resolvedTarget, UIElement.PreviewMouseWheelEvent);
            resolvedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseWheelEvent, new MouseWheelRoutedEventArgs(UIElement.PreviewMouseWheelEvent, pointerPosition, delta));
            ObserveInputRouteComplexity("Wheel", resolvedTarget, UIElement.MouseWheelEvent);
            resolvedTarget.RaiseRoutedEventInternal(UIElement.MouseWheelEvent, new MouseWheelRoutedEventArgs(UIElement.MouseWheelEvent, pointerPosition, delta));
            TraceWheelRouting($"DispatchMouseWheel routed events raised on {DescribeWheelElement(resolvedTarget)}");
        }

        if (TryHandleTextInputWheel(_cachedWheelTextInputTarget, delta))
        {
            TraceWheelRouting($"DispatchMouseWheel handled by cached text input {DescribeWheelElement(_cachedWheelTextInputTarget)}");
            TrackWheelPointerPosition(pointerPosition);
            ObserveScrollCpuWheelDispatch(delta, didPreciseRetarget, _lastInputHitTestCount - wheelHitTestsBefore, wheelHandleMs);
            ObserveControlHotspotDispatch(_cachedWheelTextInputTarget, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
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
            ObserveControlHotspotDispatch(cachedViewer, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
            return;
        }

        if (TryFindWheelTextInputAncestor(resolvedTarget, out var textInput) &&
            textInput != null &&
            TryHandleTextInputWheel(textInput, delta))
        {
            _cachedWheelTextInputTarget = textInput;
            _cachedWheelScrollViewerTarget = null;
            TraceWheelRouting($"DispatchMouseWheel handled by ancestor text input {DescribeWheelElement(textInput)}");
            TrackWheelPointerPosition(pointerPosition);
            ObserveScrollCpuWheelDispatch(delta, didPreciseRetarget, _lastInputHitTestCount - wheelHitTestsBefore, wheelHandleMs);
            ObserveControlHotspotDispatch(textInput, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
            return;
        }

        if (TryFindAncestor<ScrollViewer>(resolvedTarget, out var scrollViewer) && scrollViewer != null)
        {
            _cachedWheelScrollViewerTarget = scrollViewer;
            _cachedWheelTextInputTarget = null;
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
        ObserveControlHotspotDispatch(resolvedTarget, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
    }

    private void EnsureCachedWheelTargetsAreCurrent(Vector2 pointerPosition)
    {
        if (_cachedWheelTextInputTarget != null &&
            !PointerLikelyInsideElement(_cachedWheelTextInputTarget, pointerPosition))
        {
            TraceWheelRouting($"EnsureCachedWheelTargetsAreCurrent cleared cached text input {DescribeWheelElement(_cachedWheelTextInputTarget)} for pointer {FormatWheelPointer(pointerPosition)}");
            _cachedWheelTextInputTarget = null;
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
        if (element is ITextInputControl or ScrollViewer)
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
        var previousTextInput = _cachedWheelTextInputTarget;
        var previousScrollViewer = _cachedWheelScrollViewerTarget;
        _cachedWheelTextInputTarget = null;
        _cachedWheelScrollViewerTarget = null;
        if (hovered == null)
        {
            TraceWheelRouting($"RefreshCachedWheelTargets cleared caches from hovered=null, previousTextInput={DescribeWheelElement(previousTextInput)}, previousScrollViewer={DescribeWheelElement(previousScrollViewer)}");
            return;
        }

        if (TryFindWheelTextInputAncestor(hovered, out var textInput) && textInput != null)
        {
            _cachedWheelTextInputTarget = textInput;
            TraceWheelRouting($"RefreshCachedWheelTargets hovered={DescribeWheelElement(hovered)} -> cached text input {DescribeWheelElement(textInput)}, previousTextInput={DescribeWheelElement(previousTextInput)}, previousScrollViewer={DescribeWheelElement(previousScrollViewer)}");
            return;
        }

        if (TryFindAncestor<ScrollViewer>(hovered, out var scrollViewer) && scrollViewer != null)
        {
            _cachedWheelScrollViewerTarget = scrollViewer;
            TraceWheelRouting($"RefreshCachedWheelTargets hovered={DescribeWheelElement(hovered)} -> cached ScrollViewer {DescribeWheelElement(scrollViewer)}, previousTextInput={DescribeWheelElement(previousTextInput)}, previousScrollViewer={DescribeWheelElement(previousScrollViewer)}");
            return;
        }

        TraceWheelRouting($"RefreshCachedWheelTargets hovered={DescribeWheelElement(hovered)} found no wheel-capable ancestor, previousTextInput={DescribeWheelElement(previousTextInput)}, previousScrollViewer={DescribeWheelElement(previousScrollViewer)}");
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
        if (anchor == null ||
            !IsElementConnectedToVisualRoot(anchor) ||
            !PointerLikelyInsideElement(anchor, pointerPosition))
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

    private bool TryResolveClickTargetFromCandidate(UIElement? candidate, Vector2 pointerPosition, out UIElement? target)
    {
        target = null;
        if (candidate == null ||
            !IsElementConnectedToVisualRoot(candidate) ||
            !PointerLikelyInsideElement(candidate, pointerPosition))
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

    private bool IsElementConnectedToVisualRoot(UIElement element)
    {
        return ReferenceEquals(element.GetVisualRoot(), _visualRoot);
    }

    private static bool IsClickCapableElement(UIElement element)
    {
        if (element is Button or ITextInputControl or ScrollViewer or ScrollBar)
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
        var dispatchStart = Stopwatch.GetTimestamp();
        ObserveAllocationGcInteraction("KeyDown");
        try
        {
            _lastInputKeyEventCount++;
            _lastInputRoutedEventCount += 2;
            ObserveInputRouteComplexity("Key", target, UIElement.PreviewKeyDownEvent);
            target.RaiseRoutedEventInternal(UIElement.PreviewKeyDownEvent, new KeyRoutedEventArgs(UIElement.PreviewKeyDownEvent, key, modifiers));
            ObserveInputRouteComplexity("Key", target, UIElement.KeyDownEvent);
            target.RaiseRoutedEventInternal(UIElement.KeyDownEvent, new KeyRoutedEventArgs(UIElement.KeyDownEvent, key, modifiers));

            if (key == Keys.F10 && modifiers == ModifierKeys.None)
            {
                var menu = FindFirstVisualOfType<Menu>(_visualRoot);
                if (menu != null && menu.TryActivateMenuBarFromKeyboard(_inputState.FocusedElement))
                {
                    TrySynchronizeMenuFocusRestore(menu);
                    return;
                }
            }

            if ((modifiers & ModifierKeys.Alt) != 0 && key is >= Keys.A and <= Keys.Z)
            {
                var accessKey = (char)('A' + (int)(key - Keys.A));
                if (TryHandleMenuAccessKey(accessKey))
                {
                    return;
                }
            }

            if (TryFindMenuInMenuMode(out var activeMenu) &&
                activeMenu.TryHandleKeyDownFromInput(key, modifiers))
            {
                TrySynchronizeMenuFocusRestore(activeMenu);
                return;
            }

            if ((key == Keys.Apps || (key == Keys.F10 && modifiers == ModifierKeys.Shift)) &&
                TryOpenContextMenuForElement(_inputState.FocusedElement))
            {
                return;
            }

            if (TryFindOpenContextMenu(out var openContextMenu) &&
                openContextMenu.TryHandleKeyDownFromInput(key, modifiers))
            {
                TrySynchronizeContextMenuFocusRestore(openContextMenu);
                return;
            }

            if (key == Keys.Escape &&
                modifiers == ModifierKeys.None &&
                TryDismissTopOverlayOnEscape())
            {
                return;
            }

            if (_inputState.FocusedElement is ITextInputControl focusedTextInput &&
                focusedTextInput.HandleKeyDownFromInput(key, modifiers))
            {
                return;
            }

            if (_inputState.FocusedElement is Button button && (key == Keys.Enter || key == Keys.Space))
            {
                button.InvokeFromInput();
                return;
            }

            if (_inputState.FocusedElement is Menu focusedMenu && focusedMenu.TryHandleKeyDownFromInput(key, modifiers))
            {
                TrySynchronizeMenuFocusRestore(focusedMenu);
                return;
            }

            _ = InputGestureService.Execute(key, modifiers, _inputState.FocusedElement, _visualRoot);
        }
        finally
        {
            ObserveControlHotspotDispatch(target, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
        }
    }

    private void DispatchKeyUp(Keys key, ModifierKeys modifiers)
    {
        var target = _inputState.FocusedElement ?? _visualRoot;
        var dispatchStart = Stopwatch.GetTimestamp();
        ObserveAllocationGcInteraction("KeyUp");
        _lastInputKeyEventCount++;
        _lastInputRoutedEventCount += 2;
        ObserveInputRouteComplexity("Key", target, UIElement.PreviewKeyUpEvent);
        target.RaiseRoutedEventInternal(UIElement.PreviewKeyUpEvent, new KeyRoutedEventArgs(UIElement.PreviewKeyUpEvent, key, modifiers));
        ObserveInputRouteComplexity("Key", target, UIElement.KeyUpEvent);
        target.RaiseRoutedEventInternal(UIElement.KeyUpEvent, new KeyRoutedEventArgs(UIElement.KeyUpEvent, key, modifiers));
        ObserveControlHotspotDispatch(target, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
    }

    private void DispatchTextInput(char character)
    {
        var focused = _inputState.FocusedElement;
        if (focused == null)
        {
            return;
        }

        var dispatchStart = Stopwatch.GetTimestamp();
        ObserveAllocationGcInteraction("TextInput");
        _lastInputTextEventCount++;
        _lastInputRoutedEventCount += 2;
        ObserveInputRouteComplexity("Key", focused, UIElement.PreviewTextInputEvent);
        focused.RaiseRoutedEventInternal(UIElement.PreviewTextInputEvent, new TextInputRoutedEventArgs(UIElement.PreviewTextInputEvent, character));
        ObserveInputRouteComplexity("Key", focused, UIElement.TextInputEvent);
        focused.RaiseRoutedEventInternal(UIElement.TextInputEvent, new TextInputRoutedEventArgs(UIElement.TextInputEvent, character));

        if (focused is ITextInputControl textInput)
        {
            _ = textInput.HandleTextInputFromInput(character);
        }

        ObserveControlHotspotDispatch(focused, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
    }

    private bool TryDismissOverlayOnOutsidePointerDown(Vector2 pointerPosition, UIElement? pointerTarget)
    {
        if (!TryGetTopOverlayCandidate(out var candidate) ||
            !candidate.CloseOnOutsidePointerDown)
        {
            return false;
        }

        if ((pointerTarget != null && IsElementDescendantOf(pointerTarget, candidate.Element)) ||
            candidate.Element.HitTest(pointerPosition))
        {
            return false;
        }

        CloseOverlay(candidate.Element);
        return true;
    }

    private bool TryDismissTopOverlayOnEscape()
    {
        if (!TryGetTopOverlayCandidate(out var candidate) ||
            !candidate.CloseOnEscape)
        {
            return false;
        }

        CloseOverlay(candidate.Element);
        return true;
    }

    private bool TryGetTopOverlayCandidate(out OverlayCandidate candidate)
    {
        var hasCandidate = false;
        var bestDepth = int.MinValue;
        var bestZIndex = int.MinValue;
        var bestOrder = int.MinValue;
        var traversalOrder = 0;
        var currentCandidate = default(OverlayCandidate);
        CollectOverlayCandidate(
            _visualRoot,
            depth: 0,
            ref traversalOrder,
            ref hasCandidate,
            ref bestDepth,
            ref bestZIndex,
            ref bestOrder,
            ref currentCandidate);
        candidate = currentCandidate;
        return hasCandidate;
    }

    private static bool IsElementDescendantOf(UIElement element, UIElement ancestor)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAnyOpenContextMenu(UIElement root)
    {
        if (root is ContextMenu contextMenu && contextMenu.IsOpen)
        {
            return true;
        }

        foreach (var child in root.GetVisualChildren())
        {
            if (HasAnyOpenContextMenu(child))
            {
                return true;
            }
        }

        return false;
    }

    private static void CloseOverlay(UIElement overlay)
    {
        switch (overlay)
        {
            case Popup popup:
                popup.Close();
                break;
            case ContextMenu contextMenu:
                contextMenu.Close();
                break;
        }
    }

    private static void CollectOverlayCandidate(
        UIElement element,
        int depth,
        ref int traversalOrder,
        ref bool hasCandidate,
        ref int bestDepth,
        ref int bestZIndex,
        ref int bestOrder,
        ref OverlayCandidate bestCandidate)
    {
        var currentOrder = traversalOrder++;
        var zIndex = Panel.GetZIndex(element);
        var isOverlay = false;
        var closeOnOutsidePointerDown = false;
        var closeOnEscape = false;
        switch (element)
        {
            case Popup popup when popup.IsOpen:
                isOverlay = true;
                closeOnOutsidePointerDown = popup.DismissOnOutsideClick;
                closeOnEscape = popup.CanClose;
                break;
            case ContextMenu contextMenu when contextMenu.IsOpen:
                isOverlay = true;
                closeOnOutsidePointerDown = !contextMenu.StaysOpen;
                closeOnEscape = true;
                break;
        }

        if (isOverlay && IsBetterOverlayCandidate(depth, zIndex, currentOrder, hasCandidate, bestDepth, bestZIndex, bestOrder))
        {
            hasCandidate = true;
            bestDepth = depth;
            bestZIndex = zIndex;
            bestOrder = currentOrder;
            bestCandidate = new OverlayCandidate(element, closeOnOutsidePointerDown, closeOnEscape);
        }

        foreach (var child in element.GetVisualChildren())
        {
            CollectOverlayCandidate(
                child,
                depth + 1,
                ref traversalOrder,
                ref hasCandidate,
                ref bestDepth,
                ref bestZIndex,
                ref bestOrder,
                ref bestCandidate);
        }
    }

    private static bool IsBetterOverlayCandidate(
        int depth,
        int zIndex,
        int order,
        bool hasCandidate,
        int bestDepth,
        int bestZIndex,
        int bestOrder)
    {
        if (!hasCandidate)
        {
            return true;
        }

        if (zIndex != bestZIndex)
        {
            return zIndex > bestZIndex;
        }

        if (depth != bestDepth)
        {
            return depth > bestDepth;
        }

        return order > bestOrder;
    }

    private readonly record struct OverlayCandidate(
        UIElement Element,
        bool CloseOnOutsidePointerDown,
        bool CloseOnEscape);

    private void SetFocus(UIElement? element)
    {
        if (ReferenceEquals(_inputState.FocusedElement, element))
        {
            return;
        }

        var old = _inputState.FocusedElement;
        _inputState.FocusedElement = element;
        FocusManager.SetFocus(element);

        if (old is ITextInputControl oldTextInput)
        {
            oldTextInput.SetFocusedFromInput(false);
            old.RaiseRoutedEventInternal(UIElement.LostFocusEvent, new FocusChangedRoutedEventArgs(UIElement.LostFocusEvent, old, element));
            _lastInputRoutedEventCount++;
        }

        if (element is ITextInputControl newTextInput)
        {
            newTextInput.SetFocusedFromInput(true);
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
        if (menu == null || !menu.TryHandleAccessKeyFromInput(accessKey, _inputState.FocusedElement))
        {
            return false;
        }

        TrySynchronizeMenuFocusRestore(menu);
        return true;
    }

    private bool TryFindMenuInMenuMode(out Menu menu)
    {
        var candidate = FindFirstVisualOfType<Menu>(_visualRoot);
        if (candidate != null && candidate.IsMenuMode)
        {
            menu = candidate;
            return true;
        }

        menu = null!;
        return false;
    }

    private void TrySynchronizeMenuFocusRestore(Menu menu)
    {
        if (menu.TryConsumePendingFocusRestore(out var focusTarget) && focusTarget != null)
        {
            SetFocus(focusTarget);
        }
    }

    private void TrySynchronizeContextMenuFocusRestore(ContextMenu contextMenu)
    {
        if (contextMenu.TryConsumePendingFocusRestore(out var focusTarget) && focusTarget != null)
        {
            SetFocus(focusTarget);
        }
    }

    private bool TryOpenContextMenuFromPointer(UIElement? target, Vector2 pointerPosition)
    {
        var openStart = Stopwatch.GetTimestamp();
        if (target == null)
        {
            ContextMenuCpuDiagnostics.ObserveOpenAttempt(
                Stopwatch.GetElapsedTime(openStart).TotalMilliseconds,
                opened: false,
                source: "Pointer");
            return false;
        }

        var contextMenu = FindContextMenuForElement(target);
        if (contextMenu == null)
        {
            ContextMenuCpuDiagnostics.ObserveOpenAttempt(
                Stopwatch.GetElapsedTime(openStart).TotalMilliseconds,
                opened: false,
                source: "Pointer");
            return false;
        }

        var host = FindHostPanelForElement(target);
        if (host == null)
        {
            ContextMenuCpuDiagnostics.ObserveOpenAttempt(
                Stopwatch.GetElapsedTime(openStart).TotalMilliseconds,
                opened: false,
                source: "Pointer",
                menu: contextMenu);
            return false;
        }

        var beforeOpenSnapshot = CaptureContextMenuOpenPerfSnapshot(contextMenu);
        CloseAllOpenContextMenus();
        contextMenu.OpenAtPointer(host, pointerPosition, target);
        _lastKnownOpenContextMenu = contextMenu;
        if (contextMenu.TryHitTestMenuItem(pointerPosition, out var hoveredItem))
        {
            ContextMenuHoverDiagnostics.ObservePointerHover(
                hoveredItem,
                pointerPosition,
                target,
                "OpenAtPointerSync",
                "BeforeMove");
            contextMenu.HandlePointerMoveFromInput(hoveredItem);
            ContextMenuHoverDiagnostics.ObservePointerHover(
                hoveredItem,
                pointerPosition,
                target,
                "OpenAtPointerSync",
                "AfterMove");
        }

        var openBreakdown = BuildContextMenuOpenBreakdown(beforeOpenSnapshot, CaptureContextMenuOpenPerfSnapshot(contextMenu));
        ContextMenuCpuDiagnostics.ObserveOpenAttempt(
            Stopwatch.GetElapsedTime(openStart).TotalMilliseconds,
            opened: true,
            source: "Pointer",
            menu: contextMenu,
            breakdown: openBreakdown);
        return true;
    }

    private bool TryOpenContextMenuForElement(UIElement? target)
    {
        var openStart = Stopwatch.GetTimestamp();
        if (target == null)
        {
            ContextMenuCpuDiagnostics.ObserveOpenAttempt(
                Stopwatch.GetElapsedTime(openStart).TotalMilliseconds,
                opened: false,
                source: "Keyboard");
            return false;
        }

        var contextMenu = FindContextMenuForElement(target);
        if (contextMenu == null)
        {
            ContextMenuCpuDiagnostics.ObserveOpenAttempt(
                Stopwatch.GetElapsedTime(openStart).TotalMilliseconds,
                opened: false,
                source: "Keyboard");
            return false;
        }

        var host = FindHostPanelForElement(target);
        if (host == null)
        {
            ContextMenuCpuDiagnostics.ObserveOpenAttempt(
                Stopwatch.GetElapsedTime(openStart).TotalMilliseconds,
                opened: false,
                source: "Keyboard",
                menu: contextMenu);
            return false;
        }

        var slot = target.LayoutSlot;
        var beforeOpenSnapshot = CaptureContextMenuOpenPerfSnapshot(contextMenu);
        CloseAllOpenContextMenus();
        contextMenu.OpenAt(host, slot.X, slot.Y + slot.Height, target);
        _lastKnownOpenContextMenu = contextMenu;
        var openBreakdown = BuildContextMenuOpenBreakdown(beforeOpenSnapshot, CaptureContextMenuOpenPerfSnapshot(contextMenu));
        ContextMenuCpuDiagnostics.ObserveOpenAttempt(
            Stopwatch.GetElapsedTime(openStart).TotalMilliseconds,
            opened: true,
            source: "Keyboard",
            menu: contextMenu,
            breakdown: openBreakdown);
        return true;
    }

    private ContextMenuOpenPerfSnapshot CaptureContextMenuOpenPerfSnapshot(ContextMenu menu)
    {
        return new ContextMenuOpenPerfSnapshot(
            MenuMeasureInvalidationCount: menu.MeasureInvalidationCount,
            MenuArrangeInvalidationCount: menu.ArrangeInvalidationCount,
            MenuRenderInvalidationCount: menu.RenderInvalidationCount,
            RootMeasureInvalidationCount: _visualRoot.MeasureInvalidationCount,
            RootArrangeInvalidationCount: _visualRoot.ArrangeInvalidationCount,
            RootRenderInvalidationCount: _visualRoot.RenderInvalidationCount);
    }

    private static ContextMenuOpenBreakdown BuildContextMenuOpenBreakdown(ContextMenuOpenPerfSnapshot beforeOpen, ContextMenuOpenPerfSnapshot afterOpen)
    {
        return new ContextMenuOpenBreakdown(
            MenuMeasureDelta: Math.Max(0, afterOpen.MenuMeasureInvalidationCount - beforeOpen.MenuMeasureInvalidationCount),
            MenuArrangeDelta: Math.Max(0, afterOpen.MenuArrangeInvalidationCount - beforeOpen.MenuArrangeInvalidationCount),
            MenuRenderDelta: Math.Max(0, afterOpen.MenuRenderInvalidationCount - beforeOpen.MenuRenderInvalidationCount),
            RootMeasureDelta: Math.Max(0, afterOpen.RootMeasureInvalidationCount - beforeOpen.RootMeasureInvalidationCount),
            RootArrangeDelta: Math.Max(0, afterOpen.RootArrangeInvalidationCount - beforeOpen.RootArrangeInvalidationCount),
            RootRenderDelta: Math.Max(0, afterOpen.RootRenderInvalidationCount - beforeOpen.RootRenderInvalidationCount));
    }

    private bool TryFindOpenContextMenu(out ContextMenu contextMenu)
    {
        if (TryGetTopOverlayCandidate(out var candidate) &&
            candidate.Element is ContextMenu openContextMenu &&
            openContextMenu.IsOpen)
        {
            contextMenu = openContextMenu;
            return true;
        }

        contextMenu = null!;
        return false;
    }

    private void CloseAllOpenContextMenus()
    {
        CloseOpenContextMenusRecursive(_visualRoot);
        _lastKnownOpenContextMenu = null;
    }

    private static void CloseOpenContextMenusRecursive(UIElement element)
    {
        if (element is ContextMenu contextMenu && contextMenu.IsOpen)
        {
            contextMenu.Close();
        }

        foreach (var child in element.GetVisualChildren())
        {
            CloseOpenContextMenusRecursive(child);
        }
    }

    private static ContextMenu? FindContextMenuForElement(UIElement start)
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ContextMenu.GetContextMenu(current) is { } menu)
            {
                return menu;
            }
        }

        return null;
    }

    private static Panel? FindHostPanelForElement(UIElement start)
    {
        Panel? selected = null;
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Panel panel)
            {
                selected = panel;
            }
        }

        return selected;
    }

    private bool TryResolveContextMenuMenuItemTarget(Vector2 pointerPosition, UIElement? target, out MenuItem menuItem)
    {
        return TryResolveContextMenuMenuItemTarget(pointerPosition, target, out menuItem, out _);
    }

    private bool TryResolveContextMenuMenuItemTarget(
        Vector2 pointerPosition,
        UIElement? target,
        out MenuItem menuItem,
        out ContextMenuResolveStats resolveStats)
    {
        resolveStats = new ContextMenuResolveStats(
            Resolved: false,
            Path: "None",
            OverlayLookupMs: 0d,
            MenuHitTestMs: 0d,
            NodesVisited: 0,
            OpenBranchesVisited: 0,
            RootItemsVisited: 0,
            RowChecks: 0,
            BoundsChecks: 0,
            MaxDepth: 0,
            UninitializedBoundsFallbackCount: 0,
            InternalMenuHitTestMs: 0d);

        if (target is ContextMenu contextMenu &&
            contextMenu.IsOpen)
        {
            var menuHitTestStart = Stopwatch.GetTimestamp();
            var resolved = contextMenu.TryHitTestMenuItem(
                pointerPosition,
                out menuItem,
                out var hitTestStats);
            var menuHitTestMs = Stopwatch.GetElapsedTime(menuHitTestStart).TotalMilliseconds;
            resolveStats = new ContextMenuResolveStats(
                Resolved: resolved,
                Path: "TargetContextMenu",
                OverlayLookupMs: 0d,
                MenuHitTestMs: menuHitTestMs,
                NodesVisited: hitTestStats.NodesVisited,
                OpenBranchesVisited: hitTestStats.OpenBranchesVisited,
                RootItemsVisited: hitTestStats.RootItemsVisited,
                RowChecks: hitTestStats.RowChecks,
                BoundsChecks: hitTestStats.BoundsChecks,
                MaxDepth: hitTestStats.MaxDepth,
                UninitializedBoundsFallbackCount: hitTestStats.UninitializedBoundsFallbackCount,
                InternalMenuHitTestMs: hitTestStats.InternalElapsedMs);
            if (resolved)
            {
                return true;
            }
        }

        if (target is MenuItem targetMenuItem &&
            targetMenuItem.OwnerContextMenu is { IsOpen: true } ownerContextMenu)
        {
            var menuHitTestStart = Stopwatch.GetTimestamp();
            var resolved = ownerContextMenu.TryHitTestMenuItem(
                pointerPosition,
                out menuItem,
                out var hitTestStats);
            var menuHitTestMs = Stopwatch.GetElapsedTime(menuHitTestStart).TotalMilliseconds;
            resolveStats = new ContextMenuResolveStats(
                Resolved: resolved,
                Path: "OwnerContextMenu",
                OverlayLookupMs: 0d,
                MenuHitTestMs: menuHitTestMs,
                NodesVisited: hitTestStats.NodesVisited,
                OpenBranchesVisited: hitTestStats.OpenBranchesVisited,
                RootItemsVisited: hitTestStats.RootItemsVisited,
                RowChecks: hitTestStats.RowChecks,
                BoundsChecks: hitTestStats.BoundsChecks,
                MaxDepth: hitTestStats.MaxDepth,
                UninitializedBoundsFallbackCount: hitTestStats.UninitializedBoundsFallbackCount,
                InternalMenuHitTestMs: hitTestStats.InternalElapsedMs);
            if (resolved)
            {
                return true;
            }
        }

        if (_lastKnownOpenContextMenu is { IsOpen: true } knownOpenContextMenu)
        {
            var menuHitTestStart = Stopwatch.GetTimestamp();
            var resolved = knownOpenContextMenu.TryHitTestMenuItem(
                pointerPosition,
                out menuItem,
                out var hitTestStats);
            var menuHitTestMs = Stopwatch.GetElapsedTime(menuHitTestStart).TotalMilliseconds;
            resolveStats = new ContextMenuResolveStats(
                Resolved: resolved,
                Path: "KnownOpenContextMenu",
                OverlayLookupMs: 0d,
                MenuHitTestMs: menuHitTestMs,
                NodesVisited: hitTestStats.NodesVisited,
                OpenBranchesVisited: hitTestStats.OpenBranchesVisited,
                RootItemsVisited: hitTestStats.RootItemsVisited,
                RowChecks: hitTestStats.RowChecks,
                BoundsChecks: hitTestStats.BoundsChecks,
                MaxDepth: hitTestStats.MaxDepth,
                UninitializedBoundsFallbackCount: hitTestStats.UninitializedBoundsFallbackCount,
                InternalMenuHitTestMs: hitTestStats.InternalElapsedMs);
            if (resolved)
            {
                return true;
            }
        }
        else
        {
            _lastKnownOpenContextMenu = null;
        }

        var overlayLookupStart = Stopwatch.GetTimestamp();
        var hasOpenContextMenu = TryFindOpenContextMenu(out var openContextMenu);
        var overlayLookupMs = Stopwatch.GetElapsedTime(overlayLookupStart).TotalMilliseconds;
        if (hasOpenContextMenu)
        {
            var menuHitTestStart = Stopwatch.GetTimestamp();
            var resolved = openContextMenu.TryHitTestMenuItem(
                pointerPosition,
                out menuItem,
                out var hitTestStats);
            var menuHitTestMs = Stopwatch.GetElapsedTime(menuHitTestStart).TotalMilliseconds;
            resolveStats = new ContextMenuResolveStats(
                Resolved: resolved,
                Path: "OverlayLookup",
                OverlayLookupMs: overlayLookupMs,
                MenuHitTestMs: menuHitTestMs,
                NodesVisited: hitTestStats.NodesVisited,
                OpenBranchesVisited: hitTestStats.OpenBranchesVisited,
                RootItemsVisited: hitTestStats.RootItemsVisited,
                RowChecks: hitTestStats.RowChecks,
                BoundsChecks: hitTestStats.BoundsChecks,
                MaxDepth: hitTestStats.MaxDepth,
                UninitializedBoundsFallbackCount: hitTestStats.UninitializedBoundsFallbackCount,
                InternalMenuHitTestMs: hitTestStats.InternalElapsedMs);
            if (resolved)
            {
                return true;
            }
        }

        resolveStats = new ContextMenuResolveStats(
            Resolved: false,
            Path: hasOpenContextMenu ? "OverlayLookupMiss" : "NoOpenMenu",
            OverlayLookupMs: overlayLookupMs,
            MenuHitTestMs: 0d,
            NodesVisited: 0,
            OpenBranchesVisited: 0,
            RootItemsVisited: 0,
            RowChecks: 0,
            BoundsChecks: 0,
            MaxDepth: 0,
            UninitializedBoundsFallbackCount: 0,
            InternalMenuHitTestMs: 0d);
        menuItem = null!;
        return false;
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

    private static bool TryHandleTextInputWheel(UIElement? element, int delta)
    {
        return element switch
        {
            ITextInputControl textInput => textInput.HandleMouseWheelFromInput(delta),
            _ => false
        };
    }

    private static bool TryFindWheelTextInputAncestor(UIElement start, out UIElement? textInput)
    {
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is ITextInputControl)
            {
                textInput = current;
                return true;
            }
        }

        textInput = null;
        return false;
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

