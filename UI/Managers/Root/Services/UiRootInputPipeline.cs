using System;
using System.Collections.Generic;
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

        var captureStart = Stopwatch.GetTimestamp();
        var delta = _inputManager.Capture();
        _lastInputCaptureMs = Stopwatch.GetElapsedTime(captureStart).TotalMilliseconds;
        var dispatchStart = Stopwatch.GetTimestamp();
        ProcessInputDelta(delta);
        _lastInputDispatchMs = Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds;

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
                TryResolveContextMenuMenuItemTarget(delta.Current.PointerPosition, pointerTarget, out var contextMenuHoverItem))
            {
                var hoverHandleStart = Stopwatch.GetTimestamp();
                contextMenuHoverItem.HandlePointerMoveFromInput();
                var hoverHandleMs = Stopwatch.GetElapsedTime(hoverHandleStart).TotalMilliseconds;
                contextMenuMoveHandled = true;
            }

            var hasOpenContextMenu = _lastKnownOpenContextMenu is { IsOpen: true } || TryFindOpenContextMenu(out _);
            var shouldRouteMove = !contextMenuMoveHandled &&
                                  (hasOpenContextMenu ||
                                  !ReferenceEquals(previousHovered, _inputState.HoveredElement) ||
                                  _inputState.CapturedPointerElement != null);
            if (shouldRouteMove)
            {
                var routeStart = Stopwatch.GetTimestamp();
                DispatchPointerMove(pointerTarget, delta.Current.PointerPosition);
                pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
            }
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
            DispatchMouseDown(pointerTarget, delta.Current.PointerPosition, MouseButton.Left);
            clickResolveHitTests = 0;
            pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
        }

        if (delta.LeftReleased)
        {
            var routeStart = Stopwatch.GetTimestamp();
            DispatchMouseUp(pointerTarget, delta.Current.PointerPosition, MouseButton.Left);
            clickResolveHitTests = 0;
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
            DispatchMouseDown(pointerTarget, delta.Current.PointerPosition, MouseButton.Right);
            clickResolveHitTests = 0;
            pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
        }

        if (delta.RightReleased)
        {
            var routeStart = Stopwatch.GetTimestamp();
            DispatchMouseUp(pointerTarget, delta.Current.PointerPosition, MouseButton.Right);
            clickResolveHitTests = 0;
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
        _lastPointerResolvePath = "None";
        _lastPointerResolveHitTestMetrics = null;
        var pointerPosition = delta.Current.PointerPosition;
        var requiresPreciseTarget =
            delta.LeftPressed || delta.LeftReleased ||
            delta.RightPressed || delta.RightReleased;

        if (_inputState.CapturedPointerElement != null)
        {
            if (delta.LeftPressed || delta.LeftReleased)
            {
                _clickCpuResolveCapturedCount++;
            }

            return FinalizePointerResolve("Captured", _inputState.CapturedPointerElement);
        }

        if (delta.LeftPressed &&
            !delta.PointerMoved &&
            _lastClickUpTarget != null &&
            IsElementConnectedToVisualRoot(_lastClickUpTarget) &&
            _hasLastClickUpPointerPosition &&
            Vector2.DistanceSquared(_lastClickUpPointerPosition, pointerPosition) <= ClickPointerReuseThresholdSquared)
        {
            _clickCpuResolveCachedCount++;
            return FinalizePointerResolve("ReuseLastClickUp", _lastClickUpTarget);
        }

        if (delta.LeftReleased &&
            !delta.PointerMoved &&
            _lastClickDownTarget != null &&
            IsElementConnectedToVisualRoot(_lastClickDownTarget) &&
            _hasLastClickDownPointerPosition &&
            Vector2.DistanceSquared(_lastClickDownPointerPosition, pointerPosition) <= ClickPointerReuseThresholdSquared)
        {
            _clickCpuResolveCachedCount++;
            return FinalizePointerResolve("ReuseLastClickDown", _lastClickDownTarget);
        }

        var bypassClickTargetShortcuts = requiresPreciseTarget && ShouldBypassClickTargetShortcuts(pointerPosition);
        if (requiresPreciseTarget &&
            !bypassClickTargetShortcuts &&
            !delta.PointerMoved &&
            TryReuseCachedPointerResolveTarget(pointerPosition, out var cachedPointerTarget) &&
            cachedPointerTarget != null)
        {
            _clickCpuResolveCachedCount++;
            return FinalizePointerResolve("PointerResolveCacheReuse", cachedPointerTarget);
        }

        if (!requiresPreciseTarget)
        {
            if (TryFindOpenContextMenu(out _))
            {
                _lastInputHitTestCount++;
                return FinalizePointerResolve("ContextMenuOpenHitTest", VisualTreeHelper.HitTest(_visualRoot, pointerPosition));
            }

            if (_inputState.HoveredElement != null &&
                (delta.WheelDelta != 0 || ShouldReuseHoveredTargetForPointerMove(_inputState.HoveredElement)) &&
                PointerInsideHoveredTargetForReuse(_inputState.HoveredElement, pointerPosition))
            {
                // Do not refresh click-target reuse cache from hover-reuse paths.
                // Hover can be stale while pointer moves without precise retargeting.
                return FinalizePointerResolve("HoverReuse", _inputState.HoveredElement, updatePointerCache: false);
            }
        }

        if (!requiresPreciseTarget && !delta.PointerMoved && delta.WheelDelta == 0)
        {
            return FinalizePointerResolve("HoverNoInput", _inputState.HoveredElement, updatePointerCache: false);
        }

        if (requiresPreciseTarget && !bypassClickTargetShortcuts)
        {
            if (TryResolveItemsHostContainerTarget(pointerPosition, out var fastContainerTarget, out _, out var skipCachedAnchorPaths) &&
                fastContainerTarget != null)
            {
                _clickCpuResolveCachedCount++;
                return FinalizePointerResolve("ItemsHostContainerFastPath", fastContainerTarget);
            }

            if (TryResolvePreciseClickTargetWithinHoveredSubtree(pointerPosition, out var hoveredSubtreeTarget) &&
                hoveredSubtreeTarget != null)
            {
                _clickCpuResolveHoveredCount++;
                return FinalizePointerResolve("HoveredSubtreeHitTest", hoveredSubtreeTarget);
            }

            if (!skipCachedAnchorPaths &&
                TryResolvePreciseClickTargetWithinAnchorSubtree(_cachedClickTarget, pointerPosition, out var cachedAnchorTarget) &&
                cachedAnchorTarget != null)
            {
                _clickCpuResolveCachedCount++;
                return FinalizePointerResolve("CachedAnchorSubtreeHitTest", cachedAnchorTarget);
            }

            if (!skipCachedAnchorPaths &&
                _cachedClickTarget != null &&
                TryResolveClickTargetFromCandidate(_cachedClickTarget, pointerPosition, out var cachedClickTarget) &&
                cachedClickTarget != null)
            {
                _clickCpuResolveCachedCount++;
                return FinalizePointerResolve("CachedClickCandidate", cachedClickTarget);
            }

            if (TryResolveClickTargetFromHovered(pointerPosition, out var hoveredClickTarget) &&
                hoveredClickTarget != null)
            {
                _clickCpuResolveHoveredCount++;
                return FinalizePointerResolve("HoveredClickCandidate", hoveredClickTarget);
            }

            if (!skipCachedAnchorPaths &&
                TryGetClickHostAnchor(_cachedClickTarget, out var cachedHostAnchor) &&
                TryResolvePreciseClickTargetWithinAnchorSubtree(cachedHostAnchor, pointerPosition, out var cachedHostTarget) &&
                cachedHostTarget != null)
            {
                _clickCpuResolveCachedCount++;
                return FinalizePointerResolve("CachedHostSubtreeHitTest", cachedHostTarget);
            }

            if (HasMultipleTopLevelVisualChildren() &&
                TryResolveTopLevelSubtreeHitTest(pointerPosition, out var topLevelSubtreeTarget, out _) &&
                topLevelSubtreeTarget != null)
            {
                return FinalizePointerResolve("TopLevelSubtreeHitTest", topLevelSubtreeTarget);
            }
        }

        _lastInputHitTestCount++;
        if (requiresPreciseTarget)
        {
            _clickCpuResolveHitTestCount++;
        }

        var finalPath = bypassClickTargetShortcuts ? "OverlayBypassHitTest" : "HitTest";
        var hit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition, out var metrics);
        _lastPointerResolveHitTestMetrics = metrics;
        return FinalizePointerResolve(finalPath, hit);
    }

    private bool ShouldBypassClickTargetShortcuts(Vector2 pointerPosition)
    {
        // Cached click-target shortcuts are unsafe while any overlay is open because
        // submenus can render outside root overlay bounds.
        _ = pointerPosition;
        return TryGetTopOverlayCandidate(out _);
    }

    private bool TryReuseCachedPointerResolveTarget(Vector2 pointerPosition, out UIElement? target)
    {
        target = null;
        if (!_hasCachedPointerResolveTarget || _cachedPointerResolveTarget == null)
        {
            return false;
        }

        if (!IsElementConnectedToVisualRoot(_cachedPointerResolveTarget))
        {
            _hasCachedPointerResolveTarget = false;
            _cachedPointerResolveTarget = null;
            return false;
        }

        if (Vector2.DistanceSquared(_cachedPointerResolvePointerPosition, pointerPosition) > ClickPointerReuseThresholdSquared)
        {
            return false;
        }

        var stateStamp = GetPointerResolveStateStamp();
        if (stateStamp != _cachedPointerResolveStateStamp)
        {
            return false;
        }

        target = _cachedPointerResolveTarget;
        return true;
    }

    private void UpdateCachedPointerResolveTarget(Vector2 pointerPosition, UIElement? target)
    {
        if (target == null || !IsElementConnectedToVisualRoot(target))
        {
            _hasCachedPointerResolveTarget = false;
            _cachedPointerResolveTarget = null;
            return;
        }

        _cachedPointerResolveTarget = target;
        _cachedPointerResolvePointerPosition = pointerPosition;
        _cachedPointerResolveStateStamp = GetPointerResolveStateStamp();
        _hasCachedPointerResolveTarget = true;
    }

    private UIElement? FinalizePointerResolve(string path, UIElement? target, bool updatePointerCache = true)
    {
        _lastPointerResolvePath = path;
        if (updatePointerCache)
        {
            UpdateCachedPointerResolveTarget(_inputState.LastPointerPosition, target);
        }
        return target;
    }

    private int GetPointerResolveStateStamp()
    {
        return HashCode.Combine(
            LayoutPasses,
            MeasureInvalidationCount,
            ArrangeInvalidationCount,
            RenderInvalidationCount,
            _visualRoot.MeasureInvalidationCount,
            _visualRoot.ArrangeInvalidationCount,
            _visualRoot.RenderInvalidationCount);
    }

    private void UpdateHover(UIElement? hovered)
    {
        hovered = PromoteHoverTarget(hovered);
        var previousHovered = _inputState.HoveredElement;
        if (ReferenceEquals(previousHovered, hovered))
        {
            return;
        }

        SetHoverState(previousHovered, isMouseOver: false);

        _inputState.HoveredElement = hovered;
        RefreshCachedWheelTargets(hovered);
        RefreshCachedClickTarget(hovered);
        SetHoverState(hovered, isMouseOver: true);

        var pointerPosition = _inputState.LastPointerPosition;
        if (previousHovered != null)
        {
            _lastInputRoutedEventCount++;
            previousHovered.RaiseRoutedEventInternal(
                UIElement.MouseLeaveEvent,
                new MouseRoutedEventArgs(UIElement.MouseLeaveEvent, pointerPosition, MouseButton.Left, _inputState.CurrentModifiers));
        }

        if (hovered != null)
        {
            _lastInputRoutedEventCount++;
            hovered.RaiseRoutedEventInternal(
                UIElement.MouseEnterEvent,
                new MouseRoutedEventArgs(UIElement.MouseEnterEvent, pointerPosition, MouseButton.Left, _inputState.CurrentModifiers));
        }

    }

    private UIElement? PromoteHoverTarget(UIElement? hovered)
    {
        if (hovered == null)
        {
            return hovered;
        }

        for (var current = hovered; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (IsHoverHostElement(current))
            {
                return current;
            }
        }

        return hovered;
    }

    private static bool IsHoverHostElement(UIElement element)
    {
        return element is ITextInputControl or Button or ListBoxItem or DataGridRow or TabItem or TreeViewItem;
    }

    private static void SetHoverState(UIElement? element, bool isMouseOver)
    {
        switch (element)
        {
            case null:
                return;
            case ITextInputControl textInput:
                textInput.SetMouseOverFromInput(isMouseOver);
                return;
            case Button button:
                button.SetMouseOverFromInput(isMouseOver);
                return;
            case ListBoxItem listBoxItem:
                if (listBoxItem.IsMouseOver != isMouseOver)
                {
                    listBoxItem.IsMouseOver = isMouseOver;
                }

                return;
            case DataGridRow dataGridRow:
                if (dataGridRow.IsMouseOver != isMouseOver)
                {
                    dataGridRow.IsMouseOver = isMouseOver;
                }

                return;
            case TabItem tabItem:
                if (tabItem.IsMouseOver != isMouseOver)
                {
                    tabItem.IsMouseOver = isMouseOver;
                }

                return;
            case TreeViewItem treeViewItem:
                if (treeViewItem.IsMouseOver != isMouseOver)
                {
                    treeViewItem.IsMouseOver = isMouseOver;
                }

                return;
        }
    }

    private void DispatchPointerMove(UIElement? target, Vector2 pointerPosition)
    {
        var routedTarget = _inputState.CapturedPointerElement ?? target;
        if (routedTarget == null)
        {
            return;
        }

        var dispatchStart = Stopwatch.GetTimestamp(); _lastInputPointerEventCount++;
        _lastInputRoutedEventCount += 2; routedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseMoveEvent, new MouseRoutedEventArgs(UIElement.PreviewMouseMoveEvent, pointerPosition, MouseButton.Left, _inputState.CurrentModifiers)); routedTarget.RaiseRoutedEventInternal(UIElement.MouseMoveEvent, new MouseRoutedEventArgs(UIElement.MouseMoveEvent, pointerPosition, MouseButton.Left, _inputState.CurrentModifiers));

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
            if (target is RichTextBox richTextBox)
            {
                richTextBox.UpdateHoveredHyperlinkFromPointer(pointerPosition);
            }

            var resolved = TryResolveContextMenuMenuItemTarget(pointerPosition, target, out var contextMenuMenuItem);
            if (resolved)
            {
                var hoverHandleStart = Stopwatch.GetTimestamp();
                contextMenuMenuItem.HandlePointerMoveFromInput();
                var hoverHandleMs = Stopwatch.GetElapsedTime(hoverHandleStart).TotalMilliseconds;
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

        if (button == MouseButton.Left &&
            target is not Button &&
            TryFindAncestor<Button>(target, out var ancestorButton) &&
            ancestorButton != null)
        {
            target = ancestorButton;
        }

        UpdateHover(target);

        UIElement? textInputTarget = null;
        if (button == MouseButton.Left &&
            TryFindWheelTextInputAncestor(target, out var ancestorTextInput) &&
            ancestorTextInput != null)
        {
            textInputTarget = ancestorTextInput;
        }

        RefreshCachedClickTarget(target);

        _lastInputPointerEventCount++;
        _lastInputRoutedEventCount += 2;
        target.RaiseRoutedEventInternal(UIElement.PreviewMouseDownEvent, new MouseRoutedEventArgs(UIElement.PreviewMouseDownEvent, pointerPosition, button, _inputState.CurrentModifiers));
        target.RaiseRoutedEventInternal(UIElement.MouseDownEvent, new MouseRoutedEventArgs(UIElement.MouseDownEvent, pointerPosition, button, _inputState.CurrentModifiers));
        if (target is not Menu && target is not MenuItem)
        {
            SetFocus(textInputTarget ?? target);
        }

        if (target is MenuItem menuItemTarget)
        {
            _ = menuItemTarget.HandlePointerDownFromInput();
            if (menuItemTarget.OwnerMenu is { } ownerMenu)
            {
                TrySynchronizeMenuFocusRestore(ownerMenu);
            }
            return;
        }

        if (button == MouseButton.Left &&
            TryResolveContextMenuMenuItemTarget(pointerPosition, target, out var contextMenuMenuItem))
        {
            _ = contextMenuMenuItem.HandlePointerDownFromInput(); return;
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
        else if (button == MouseButton.Left &&
                 ((target is Expander directExpander && directExpander.HandlePointerDownFromInput(pointerPosition)) ||
                  (target != null &&
                   TryFindAncestor<Expander>(target, out var owningExpander) &&
                   owningExpander != null &&
                   owningExpander.HandlePointerDownFromInput(pointerPosition))))
        {
            var expanderToCapture = target as Expander;
            if (expanderToCapture == null &&
                target != null &&
                TryFindAncestor<Expander>(target, out var ancestorExpander))
            {
                expanderToCapture = ancestorExpander;
            }

            if (expanderToCapture != null)
            {
                CapturePointer(expanderToCapture);
            }
        }
        else if (button == MouseButton.Left && textInputTarget is ITextInputControl textInput)
        {
            textInput.HandlePointerDownFromInput(pointerPosition, extendSelection: (_inputState.CurrentModifiers & ModifierKeys.Shift) != 0);
            CapturePointer(textInputTarget);
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
        routedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseUpEvent, new MouseRoutedEventArgs(UIElement.PreviewMouseUpEvent, pointerPosition, button, _inputState.CurrentModifiers));
        routedTarget.RaiseRoutedEventInternal(UIElement.MouseUpEvent, new MouseRoutedEventArgs(UIElement.MouseUpEvent, pointerPosition, button, _inputState.CurrentModifiers));

        if (_inputState.CapturedPointerElement is Button pressedButton && button == MouseButton.Left)
        {
            var shouldInvoke = ReferenceEquals(target, pressedButton) ||
                               (target != null &&
                                TryFindAncestor<Button>(target, out var ancestorButton) &&
                                ReferenceEquals(ancestorButton, pressedButton));
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
        else if (_inputState.CapturedPointerElement is Expander expander && button == MouseButton.Left)
        {
            expander.HandlePointerUpFromInput(pointerPosition);
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
        }

        ReleasePointer(_inputState.CapturedPointerElement);

        if (button == MouseButton.Right)
        {
            _ = TryOpenContextMenuFromPointer(target, pointerPosition);
        }
    }

    private void DispatchMouseWheel(UIElement? target, Vector2 pointerPosition, int delta)
    {
        var wheelHitTestsBefore = _lastInputHitTestCount;
        var wheelHandleMs = 0d;
        var resolvedTarget = target ?? _inputState.HoveredElement;
        if (resolvedTarget == null)
        {
            return;
        }

        var dispatchStart = Stopwatch.GetTimestamp(); EnsureCachedWheelTargetsAreCurrent(pointerPosition);
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
        if (shouldPreciseRetarget)
        {
            if (TryFindWheelCapableTargetAtPointer(pointerPosition, out var fastWheelTarget) &&
                fastWheelTarget != null)
            {
                resolvedTarget = fastWheelTarget;
                RefreshCachedWheelTargets(fastWheelTarget);
            }
            else
            {
                _lastInputHitTestCount++;
                var preciseTarget = VisualTreeHelper.HitTest(_visualRoot, pointerPosition);
                if (preciseTarget != null)
                {
                    resolvedTarget = preciseTarget;
                    RefreshCachedWheelTargets(preciseTarget);
                }
                else
                { }
            }

            _lastWheelPreciseRetargetTimestamp = Stopwatch.GetTimestamp();
        }

        _lastInputPointerEventCount++;
        if (HasWheelRoutedEventHandlers(resolvedTarget))
        {
            _lastInputRoutedEventCount += 2;
            resolvedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseWheelEvent, new MouseWheelRoutedEventArgs(UIElement.PreviewMouseWheelEvent, pointerPosition, delta));
            resolvedTarget.RaiseRoutedEventInternal(UIElement.MouseWheelEvent, new MouseWheelRoutedEventArgs(UIElement.MouseWheelEvent, pointerPosition, delta));
        }

        if (TryFindOpenContextMenu(out var openContextMenu) &&
            openContextMenu.HandleMouseWheelFromInput(pointerPosition, delta))
        {
            RefreshHoverAfterWheelContentMutation(pointerPosition);
            TrackWheelPointerPosition(pointerPosition);
            return;
        }

        if (TryHandleTextInputWheel(_cachedWheelTextInputTarget, delta))
        {
            RefreshHoverAfterWheelContentMutation(pointerPosition);
            TrackWheelPointerPosition(pointerPosition);
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
            if (handled)
            {
                RefreshHoverAfterWheelContentMutation(pointerPosition, cachedViewer);
            }
            else
            {
                RefreshHoverAfterWheel(pointerPosition);
            }
            TrackWheelPointerPosition(pointerPosition);
            return;
        }

        if (TryFindWheelTextInputAncestor(resolvedTarget, out var textInput) &&
            textInput != null &&
            TryHandleTextInputWheel(textInput, delta))
        {
            _cachedWheelTextInputTarget = textInput;
            _cachedWheelScrollViewerTarget = null;
            TrackWheelPointerPosition(pointerPosition);
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
            if (handled)
            {
                RefreshHoverAfterWheelContentMutation(pointerPosition, scrollViewer);
            }
        }

        RefreshHoverAfterWheel(pointerPosition);
        TrackWheelPointerPosition(pointerPosition);
    }

    private void RefreshHoverAfterWheel(Vector2 pointerPosition)
    {
        _hasCachedPointerResolveTarget = false;
        _cachedPointerResolveTarget = null;

        var existingHovered = _inputState.HoveredElement;
        if (existingHovered != null && PointerInsideHoveredTargetForReuse(existingHovered, pointerPosition))
        {
            return;
        }

        var hoverTarget = VisualTreeHelper.HitTest(_visualRoot, pointerPosition);
        UpdateHover(hoverTarget);
    }

    private void RefreshHoverAfterWheelContentMutation(Vector2 pointerPosition, UIElement? mutationRoot = null)
    {
        _hasCachedPointerResolveTarget = false;
        _cachedPointerResolveTarget = null;

        UIElement? hoverTarget = null;
        if (mutationRoot != null && IsElementConnectedToVisualRoot(mutationRoot))
        {
            hoverTarget = VisualTreeHelper.HitTest(mutationRoot, pointerPosition);
        }

        if (hoverTarget == null)
        {
            hoverTarget = VisualTreeHelper.HitTest(_visualRoot, pointerPosition);
        }

        UpdateHover(hoverTarget);
    }

    private void EnsureCachedWheelTargetsAreCurrent(Vector2 pointerPosition)
    {
        if (_cachedWheelTextInputTarget != null &&
            !PointerLikelyInsideElement(_cachedWheelTextInputTarget, pointerPosition))
        {
            _cachedWheelTextInputTarget = null;
        }

        if (_cachedWheelScrollViewerTarget != null &&
            !PointerLikelyInsideElement(_cachedWheelScrollViewerTarget, pointerPosition))
        {
            _cachedWheelScrollViewerTarget = null;
        }
    }

    private static bool PointerLikelyInsideElement(UIElement element, Vector2 pointerPosition)
    {
        if (RequiresPrecisePointerContainmentCheck(element))
        {
            return element.HitTest(pointerPosition);
        }

        if (IsPointInsideElementSlot(element, pointerPosition.X, pointerPosition.Y))
        {
            return true;
        }

        // Fallback for non-framework visuals with custom hit geometry.
        return element is not FrameworkElement && element.HitTest(pointerPosition);
    }

    private bool PointerInsideHoveredTargetForReuse(UIElement hovered, Vector2 pointerPosition)
    {
        if (!IsElementConnectedToVisualRoot(hovered))
        {
            return false;
        }

        return hovered.HitTest(pointerPosition);
    }

    private static bool RequiresPrecisePointerContainmentCheck(UIElement element)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current.TryGetLocalRenderTransformSnapshot(out _) ||
                current.TryGetLocalClipSnapshot(out _))
            {
                return true;
            }
        }

        return false;
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
            return;
        }

        if (TryFindWheelTextInputAncestor(hovered, out var textInput) && textInput != null)
        {
            _cachedWheelTextInputTarget = textInput; return;
        }

        if (TryFindAncestor<ScrollViewer>(hovered, out var scrollViewer) && scrollViewer != null)
        {
            _cachedWheelScrollViewerTarget = scrollViewer; return;
        }
    }

    private bool TryResolveClickTargetFromHovered(Vector2 pointerPosition, out UIElement? target)
    {
        return TryResolveClickTargetFromCandidate(_inputState.HoveredElement, pointerPosition, out target);
    }

    private bool TryResolveTopLevelSubtreeHitTest(Vector2 pointerPosition, out UIElement? target, out string detail)
    {
        target = null;
        detail = "no-candidate";
        var rootChildren = new List<UIElement>(8);
        foreach (var child in _visualRoot.GetVisualChildren())
        {
            rootChildren.Add(child);
        }

        if (rootChildren.Count == 0)
        {
            detail = "root-children=0";
            return false;
        }

        for (var i = rootChildren.Count - 1; i >= 0; i--)
        {
            var candidate = rootChildren[i];
            if (!IsPointInsideElementSlot(candidate, pointerPosition.X, pointerPosition.Y))
            {
                continue;
            }

            _lastInputHitTestCount++;
            _clickCpuResolveHitTestCount++;
            var hit = VisualTreeHelper.HitTest(candidate, pointerPosition);

            if (hit == null)
            {
                detail = $"childIndex={i}; child={candidate.GetType().Name}; result=null";
                continue;
            }

            target = hit;
            detail = $"childIndex={i}; child={candidate.GetType().Name}; result={hit.GetType().Name}";
            return true;
        }

        detail = $"root-children={rootChildren.Count}; matchedSlots=0";
        return false;
    }

    private bool TryResolvePreciseClickTargetWithinHoveredSubtree(Vector2 pointerPosition, out UIElement? target)
    {
        return TryResolvePreciseClickTargetWithinAnchorSubtree(_inputState.HoveredElement, pointerPosition, out target, hoveredStrictMode: true);
    }

    private bool TryResolvePreciseClickTargetWithinAnchorSubtree(
        UIElement? anchor,
        Vector2 pointerPosition,
        out UIElement? target,
        bool hoveredStrictMode = false)
    {
        target = null;
        if (anchor == null ||
            !IsElementConnectedToVisualRoot(anchor) ||
            IsBroadClickAnchor(anchor) ||
            (hoveredStrictMode && !IsNarrowHoveredAnchor(anchor)) ||
            !PointerLikelyInsideElement(anchor, pointerPosition))
        {
            return false;
        }

        _lastInputHitTestCount++;
        _clickCpuResolveHitTestCount++;
        var hit = VisualTreeHelper.HitTest(anchor, pointerPosition);

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

        // TreeViewItem layout slots include descendant rows when expanded, so
        // candidate shortcutting can incorrectly pin clicks to the ancestor item.
        // Let precise hit-testing determine the concrete row target.
        if (candidate is TreeViewItem)
        {
            return false;
        }

        for (var current = candidate; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (IsKnownClickCapableElement(current))
            {
                target = current;
                return true;
            }
        }

        return false;
    }

    private bool HasMultipleTopLevelVisualChildren()
    {
        var count = 0;
        foreach (var _ in _visualRoot.GetVisualChildren())
        {
            count++;
            if (count > 1)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsBroadClickAnchor(UIElement anchor)
    {
        if (ReferenceEquals(anchor, _visualRoot))
        {
            return true;
        }

        if (ReferenceEquals(anchor.VisualParent, _visualRoot) &&
            anchor is not Popup &&
            anchor is not ContextMenu)
        {
            return true;
        }

        if (anchor is not FrameworkElement frameworkElement)
        {
            return false;
        }

        var slot = frameworkElement.LayoutSlot;
        var anchorArea = MathF.Max(0f, slot.Width) * MathF.Max(0f, slot.Height);
        if (anchorArea <= 0f)
        {
            return false;
        }

        var rootArea = 0f;
        if (_hasLastLayoutViewport)
        {
            rootArea = MathF.Max(0f, _lastLayoutViewport.Width) * MathF.Max(0f, _lastLayoutViewport.Height);
        }
        else if (_visualRoot is FrameworkElement rootFramework)
        {
            var rootSlot = rootFramework.LayoutSlot;
            rootArea = MathF.Max(0f, rootSlot.Width) * MathF.Max(0f, rootSlot.Height);
        }

        if (rootArea <= 0f)
        {
            return false;
        }

        var areaRatio = anchorArea / rootArea;
        return areaRatio >= 0.20f;
    }

    private bool ShouldReuseHoveredTargetForPointerMove(UIElement hovered)
    {
        if (hovered.RenderTransform != null ||
            hovered.HasRoutedHandlerForEvent(UIElement.MouseEnterEvent) ||
            hovered.HasRoutedHandlerForEvent(UIElement.MouseLeaveEvent))
        {
            return false;
        }

        if (hovered is ScrollViewer or ScrollBar)
        {
            return false;
        }

        if (hovered is ITextInputControl or
            ListBox or ListView or DataGrid or
            MenuItem or ComboBoxItem or TabItem)
        {
            return true;
        }

        if (hovered is not FrameworkElement frameworkElement)
        {
            return false;
        }

        var anchorArea = MathF.Max(0f, frameworkElement.LayoutSlot.Width) * MathF.Max(0f, frameworkElement.LayoutSlot.Height);
        if (anchorArea <= 0f)
        {
            return false;
        }

        var rootArea = 0f;
        if (_hasLastLayoutViewport)
        {
            rootArea = MathF.Max(0f, _lastLayoutViewport.Width) * MathF.Max(0f, _lastLayoutViewport.Height);
        }
        else if (_visualRoot is FrameworkElement rootFramework)
        {
            var rootSlot = rootFramework.LayoutSlot;
            rootArea = MathF.Max(0f, rootSlot.Width) * MathF.Max(0f, rootSlot.Height);
        }

        if (rootArea <= 0f)
        {
            return false;
        }

        var areaRatio = anchorArea / rootArea;
        return hovered is Label && areaRatio < 0.02f;
    }

    private bool IsNarrowHoveredAnchor(UIElement anchor)
    {
        if (anchor is ListBoxItem or ListViewItem or DataGridRow or MenuItem or ComboBoxItem or TabItem or TreeViewItem)
        {
            return true;
        }

        if (anchor is ScrollViewer or Panel or ItemsPresenter)
        {
            return false;
        }

        if (anchor is not FrameworkElement frameworkElement)
        {
            return false;
        }

        var slot = frameworkElement.LayoutSlot;
        var anchorArea = MathF.Max(0f, slot.Width) * MathF.Max(0f, slot.Height);
        if (anchorArea <= 0f)
        {
            return false;
        }

        var rootArea = 0f;
        if (_hasLastLayoutViewport)
        {
            rootArea = MathF.Max(0f, _lastLayoutViewport.Width) * MathF.Max(0f, _lastLayoutViewport.Height);
        }
        else if (_visualRoot is FrameworkElement rootFramework)
        {
            var rootSlot = rootFramework.LayoutSlot;
            rootArea = MathF.Max(0f, rootSlot.Width) * MathF.Max(0f, rootSlot.Height);
        }

        if (rootArea <= 0f)
        {
            return false;
        }

        var areaRatio = anchorArea / rootArea;
        return areaRatio <= 0.08f;
    }

    private static bool IsKnownClickCapableElement(UIElement element)
    {
        return element is Button or ITextInputControl or ScrollViewer or ScrollBar or
            ListBoxItem or ListViewItem or DataGridRow or MenuItem or ComboBoxItem or TabItem or TreeViewItem;
    }

    private bool IsElementConnectedToVisualRoot(UIElement element)
    {
        return ReferenceEquals(element.GetVisualRoot(), _visualRoot);
    }

    private bool TryResolveItemsHostContainerTarget(
        Vector2 pointerPosition,
        out UIElement? target,
        out string detail,
        out bool skipCachedAnchorPaths)
    {
        target = null;
        detail = "none";
        skipCachedAnchorPaths = false;
        var cachedAnchor = _cachedClickTarget;
        var hoveredAnchor = _inputState.HoveredElement;
        var attemptedCached = false;
        var attemptedHovered = false;
        var cachedDetail = "skipped(anchor=null)";
        var hoveredDetail = "skipped(anchor=null)";

        if (cachedAnchor != null &&
            IsElementConnectedToVisualRoot(cachedAnchor) &&
            TryResolveItemsHostContainerTargetFromAnchor(cachedAnchor, pointerPosition, _visualRoot, out target, out cachedDetail))
        {
            detail = $"anchor=cached; {cachedDetail}";
            return true;
        }
        attemptedCached = cachedAnchor != null;
        cachedDetail = attemptedCached ? cachedDetail : "skipped(anchor=null)";

        if (hoveredAnchor != null &&
            IsElementConnectedToVisualRoot(hoveredAnchor) &&
            !ReferenceEquals(cachedAnchor, hoveredAnchor) &&
            TryResolveItemsHostContainerTargetFromAnchor(hoveredAnchor, pointerPosition, _visualRoot, out target, out hoveredDetail))
        {
            detail = $"anchor=hovered; {hoveredDetail}";
            return true;
        }
        attemptedHovered = hoveredAnchor != null && !ReferenceEquals(cachedAnchor, hoveredAnchor);
        hoveredDetail = attemptedHovered
            ? hoveredDetail
            : (hoveredAnchor == null ? "skipped(anchor=null)" : "skipped(anchor=same-as-cached)");

        skipCachedAnchorPaths = attemptedCached &&
                                (cachedDetail.Contains("hostBoundsHit=false", StringComparison.Ordinal) ||
                                 cachedDetail.Contains("host=null", StringComparison.Ordinal) ||
                                 cachedDetail.Contains("container=null", StringComparison.Ordinal));
        detail = $"cachedMiss={cachedDetail}; hoveredMiss={hoveredDetail}";
        return false;
    }

    private static bool TryResolveItemsHostContainerTargetFromAnchor(
        UIElement? anchor,
        Vector2 pointerPosition,
        UIElement visualRoot,
        out UIElement? target,
        out string detail)
    {
        var phaseStart = Stopwatch.GetTimestamp();
        target = null;
        detail = "unknown";
        if (anchor == null)
        {
            detail = "anchor=null";
            return false;
        }

        if (!ReferenceEquals(anchor.GetVisualRoot(), visualRoot))
        {
            detail = "anchor=detached";
            return false;
        }

        var container = FindNearestItemsContainer(anchor);
        var nearestMs = Stopwatch.GetElapsedTime(phaseStart).TotalMilliseconds;
        if (container == null)
        {
            detail = $"container=null nearest={nearestMs:0.###}ms";
            return false;
        }

        if (!ReferenceEquals(container.GetVisualRoot(), visualRoot))
        {
            detail = $"container={container.GetType().Name} detached nearest={nearestMs:0.###}ms";
            return false;
        }

        if (!IsPointerTargetChainInteractive(container))
        {
            detail = $"container={container.GetType().Name} chainInactive nearest={nearestMs:0.###}ms";
            return false;
        }

        var hostStart = Stopwatch.GetTimestamp();
        var host = container.VisualParent as Panel;
        if (host == null)
        {
            detail = $"container={container.GetType().Name} host=null nearest={nearestMs:0.###}ms";
            return false;
        }
        var hostMs = Stopwatch.GetElapsedTime(hostStart).TotalMilliseconds;

        var probeStart = Stopwatch.GetTimestamp();
        var probeX = pointerPosition.X;
        var probeY = pointerPosition.Y;
        if (TryFindAncestor<ScrollViewer>(host, out var ownerScrollViewer) && ownerScrollViewer != null)
        {
            probeX += ownerScrollViewer.HorizontalOffset;
            probeY += ownerScrollViewer.VerticalOffset;
        }
        var probeMs = Stopwatch.GetElapsedTime(probeStart).TotalMilliseconds;

        var hostBoundsStart = Stopwatch.GetTimestamp();
        if (!IsPointInsideElementSlot(host, probeX, probeY))
        {
            var hostBoundsMs = Stopwatch.GetElapsedTime(hostBoundsStart).TotalMilliseconds;
            detail = $"containerHit=false hostBoundsHit=false nearest={nearestMs:0.###}ms host={hostMs:0.###}ms probe={probeMs:0.###}ms hostBounds={hostBoundsMs:0.###}ms";
            return false;
        }
        var hostBoundsHitMs = Stopwatch.GetElapsedTime(hostBoundsStart).TotalMilliseconds;

        var directContainerStart = Stopwatch.GetTimestamp();
        if (container != null && IsPointInsideElementSlot(container, probeX, probeY))
        {
            target = container;
            var directMs = Stopwatch.GetElapsedTime(directContainerStart).TotalMilliseconds;
            detail = $"containerHit=true hostBoundsHit=true nearest={nearestMs:0.###}ms host={hostMs:0.###}ms probe={probeMs:0.###}ms hostBounds={hostBoundsHitMs:0.###}ms direct={directMs:0.###}ms";
            return true;
        }
        var directMissMs = Stopwatch.GetElapsedTime(directContainerStart).TotalMilliseconds;
        detail = $"containerHit=false hostBoundsHit=true nearest={nearestMs:0.###}ms host={hostMs:0.###}ms probe={probeMs:0.###}ms hostBounds={hostBoundsHitMs:0.###}ms direct={directMissMs:0.###}ms";
        return false;
    }

    private static bool IsPointerTargetChainInteractive(UIElement element)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (!current.IsVisible || !current.IsEnabled || !current.IsHitTestVisible)
            {
                return false;
            }

            if (current is Popup popup && !popup.IsOpen)
            {
                return false;
            }

            if (current is ContextMenu contextMenu && !contextMenu.IsOpen)
            {
                return false;
            }
        }

        return true;
    }

    private static UIElement? FindNearestItemsContainer(UIElement? element)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (IsItemsContainerElement(current))
            {
                return current;
            }
        }

        return null;
    }

    private static bool IsItemsContainerElement(UIElement element)
    {
        return element is ListBoxItem or ListViewItem or DataGridRow;
    }

    private static bool TryResolveContainerByEstimatedIndex(IReadOnlyList<UIElement> children, float probeX, float probeY, out UIElement? target)
    {
        target = null;
        if (children.Count == 0)
        {
            return false;
        }

        if (!TryFindContainerRange(children, out var firstContainerIndex, out var lastContainerIndex))
        {
            return false;
        }

        if (children[firstContainerIndex] is not FrameworkElement firstContainer ||
            children[lastContainerIndex] is not FrameworkElement lastContainer)
        {
            return false;
        }

        var containerCount = (lastContainerIndex - firstContainerIndex) + 1;
        if (containerCount <= 0)
        {
            return false;
        }

        var top = firstContainer.LayoutSlot.Y;
        var bottom = lastContainer.LayoutSlot.Y + lastContainer.LayoutSlot.Height;
        var span = MathF.Max(1f, bottom - top);
        var averageHeight = span / containerCount;
        var estimated = firstContainerIndex + (int)((probeY - top) / averageHeight);
        estimated = Math.Clamp(estimated, firstContainerIndex, lastContainerIndex);

        if (IsItemsContainerElement(children[estimated]) && IsPointInsideElementSlot(children[estimated], probeX, probeY))
        {
            target = children[estimated];
            return true;
        }

        const int neighborRadius = 6;
        for (var offset = 1; offset <= neighborRadius; offset++)
        {
            var lower = estimated - offset;
            if (lower >= firstContainerIndex &&
                IsItemsContainerElement(children[lower]) &&
                IsPointInsideElementSlot(children[lower], probeX, probeY))
            {
                target = children[lower];
                return true;
            }

            var upper = estimated + offset;
            if (upper <= lastContainerIndex &&
                IsItemsContainerElement(children[upper]) &&
                IsPointInsideElementSlot(children[upper], probeX, probeY))
            {
                target = children[upper];
                return true;
            }
        }

        return false;
    }

    private static bool TryFindContainerRange(IReadOnlyList<UIElement> children, out int firstIndex, out int lastIndex)
    {
        firstIndex = -1;
        for (var i = 0; i < children.Count; i++)
        {
            if (!IsItemsContainerElement(children[i]))
            {
                continue;
            }

            firstIndex = i;
            break;
        }

        if (firstIndex < 0)
        {
            lastIndex = -1;
            return false;
        }

        lastIndex = -1;
        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (!IsItemsContainerElement(children[i]))
            {
                continue;
            }

            lastIndex = i;
            break;
        }

        return lastIndex >= firstIndex;
    }

    private static bool IsPointInsideElementSlot(UIElement element, float x, float y)
    {
        if (!element.IsVisible || !element.IsEnabled || !element.IsHitTestVisible || element is not FrameworkElement frameworkElement)
        {
            return false;
        }

        var slot = frameworkElement.LayoutSlot;
        return x >= slot.X &&
               x <= slot.X + slot.Width &&
               y >= slot.Y &&
               y <= slot.Y + slot.Height;
    }

    private static bool TryGetClickHostAnchor(UIElement? anchor, out UIElement? hostAnchor)
    {
        hostAnchor = null;
        if (anchor == null)
        {
            return false;
        }

        for (var current = anchor.VisualParent ?? anchor.LogicalParent; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is ScrollViewer)
            {
                continue;
            }

            if (current is Panel)
            {
                hostAnchor = current;
                return true;
            }
        }

        return false;
    }

    private static bool IsClickCapableElement(UIElement element)
    {
        if (element is Button or ITextInputControl or ScrollViewer or ScrollBar or
            ListBoxItem or ListViewItem or DataGridRow or ComboBoxItem or TabItem or TreeViewItem)
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
        var dispatchStart = Stopwatch.GetTimestamp(); try
        {
            _lastInputKeyEventCount++;
            _lastInputRoutedEventCount += 2; var previewArgs = new KeyRoutedEventArgs(UIElement.PreviewKeyDownEvent, key, modifiers);
            target.RaiseRoutedEventInternal(UIElement.PreviewKeyDownEvent, previewArgs);
            if (previewArgs.Handled) return;

            var keyArgs = new KeyRoutedEventArgs(UIElement.KeyDownEvent, key, modifiers);
            target.RaiseRoutedEventInternal(UIElement.KeyDownEvent, keyArgs);
            if (keyArgs.Handled) return;

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
        { }
    }

    private void DispatchKeyUp(Keys key, ModifierKeys modifiers)
    {
        var target = _inputState.FocusedElement ?? _visualRoot;
        var dispatchStart = Stopwatch.GetTimestamp(); _lastInputKeyEventCount++;
        _lastInputRoutedEventCount += 2; target.RaiseRoutedEventInternal(UIElement.PreviewKeyUpEvent, new KeyRoutedEventArgs(UIElement.PreviewKeyUpEvent, key, modifiers)); target.RaiseRoutedEventInternal(UIElement.KeyUpEvent, new KeyRoutedEventArgs(UIElement.KeyUpEvent, key, modifiers));
    }

    private void DispatchTextInput(char character)
    {
        var focused = _inputState.FocusedElement;
        if (focused == null)
        {
            return;
        }

        var dispatchStart = Stopwatch.GetTimestamp(); _lastInputTextEventCount++;
        _lastInputRoutedEventCount += 2; focused.RaiseRoutedEventInternal(UIElement.PreviewTextInputEvent, new TextInputRoutedEventArgs(UIElement.PreviewTextInputEvent, character)); focused.RaiseRoutedEventInternal(UIElement.TextInputEvent, new TextInputRoutedEventArgs(UIElement.TextInputEvent, character));

        if (focused is ITextInputControl textInput)
        {
            _ = textInput.HandleTextInputFromInput(character);
        }
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
        if (target == null)
        {
            return false;
        }

        var contextMenu = FindContextMenuForElement(target);
        if (contextMenu == null)
        {
            return false;
        }

        var host = FindHostPanelForElement(target);
        if (host == null)
        {
            return false;
        }

        CloseAllOpenContextMenus();
        contextMenu.OpenAtPointer(host, pointerPosition, target);
        _lastKnownOpenContextMenu = contextMenu;
        if (contextMenu.TryHitTestMenuItem(pointerPosition, out var hoveredItem))
        {
            contextMenu.HandlePointerMoveFromInput(hoveredItem);
        }

        return true;
    }

    private bool TryOpenContextMenuForElement(UIElement? target)
    {
        if (target == null)
        {
            return false;
        }

        var contextMenu = FindContextMenuForElement(target);
        if (contextMenu == null)
        {
            return false;
        }

        var host = FindHostPanelForElement(target);
        if (host == null)
        {
            return false;
        }

        var slot = target.LayoutSlot;
        CloseAllOpenContextMenus();
        contextMenu.OpenAt(host, slot.X, slot.Y + slot.Height, target);
        _lastKnownOpenContextMenu = contextMenu;
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
        if (target is ContextMenu contextMenu &&
            contextMenu.IsOpen)
        {
            if (contextMenu.TryHitTestMenuItem(pointerPosition, out menuItem, out _))
            {
                return true;
            }
        }

        if (target is MenuItem targetMenuItem &&
            targetMenuItem.OwnerContextMenu is { IsOpen: true } ownerContextMenu)
        {
            if (ownerContextMenu.TryHitTestMenuItem(pointerPosition, out menuItem, out _))
            {
                return true;
            }
        }

        if (_lastKnownOpenContextMenu is { IsOpen: true } knownOpenContextMenu)
        {
            if (knownOpenContextMenu.TryHitTestMenuItem(pointerPosition, out menuItem, out _))
            {
                return true;
            }
        }
        else
        {
            _lastKnownOpenContextMenu = null;
        }

        var hasOpenContextMenu = TryFindOpenContextMenu(out var openContextMenu);
        if (hasOpenContextMenu)
        {
            if (openContextMenu.TryHitTestMenuItem(pointerPosition, out menuItem, out _))
            {
                return true;
            }
        }

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


