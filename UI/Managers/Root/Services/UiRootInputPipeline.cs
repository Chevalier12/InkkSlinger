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
            EmitPointerResolveDiagnosticsForClick("LeftDown", pointerTarget, clickHitTests);
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
            EmitPointerResolveDiagnosticsForClick("LeftUp", pointerTarget, clickHitTests);
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
            EmitPointerResolveDiagnosticsForClick("RightDown", pointerTarget, clickHitTests);
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
            EmitPointerResolveDiagnosticsForClick("RightUp", pointerTarget, clickHitTests);
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
        var pointerPosition = delta.Current.PointerPosition;
        var requiresPreciseTarget =
            delta.LeftPressed || delta.LeftReleased ||
            delta.RightPressed || delta.RightReleased;
        BeginPointerResolveTrace(pointerPosition, requiresPreciseTarget);

        var capturedCheckStart = Stopwatch.GetTimestamp();
        if (_inputState.CapturedPointerElement != null)
        {
            TracePointerResolveStep("CapturedPointer", capturedCheckStart, success: true, _inputState.CapturedPointerElement.GetType().Name);
            if (delta.LeftPressed || delta.LeftReleased)
            {
                _clickCpuResolveCapturedCount++;
            }
            return CompletePointerResolve("Captured", _inputState.CapturedPointerElement);
        }
        TracePointerResolveStep("CapturedPointer", capturedCheckStart, success: false);

        var reuseUpStart = Stopwatch.GetTimestamp();
        if (delta.LeftPressed &&
            !delta.PointerMoved &&
            _lastClickUpTarget != null &&
            IsElementConnectedToVisualRoot(_lastClickUpTarget) &&
            _hasLastClickUpPointerPosition &&
            Vector2.DistanceSquared(_lastClickUpPointerPosition, delta.Current.PointerPosition) <= ClickPointerReuseThresholdSquared)
        {
            TracePointerResolveStep("ReuseLastClickUp", reuseUpStart, success: true, _lastClickUpTarget.GetType().Name);
            _clickCpuResolveCachedCount++;
            return CompletePointerResolve("ReuseLastClickUp", _lastClickUpTarget);
        }
        TracePointerResolveStep("ReuseLastClickUp", reuseUpStart, success: false);

        var reuseDownStart = Stopwatch.GetTimestamp();
        if (delta.LeftReleased &&
            !delta.PointerMoved &&
            _lastClickDownTarget != null &&
            IsElementConnectedToVisualRoot(_lastClickDownTarget) &&
            _hasLastClickDownPointerPosition &&
            Vector2.DistanceSquared(_lastClickDownPointerPosition, delta.Current.PointerPosition) <= ClickPointerReuseThresholdSquared)
        {
            TracePointerResolveStep("ReuseLastClickDown", reuseDownStart, success: true, _lastClickDownTarget.GetType().Name);
            _clickCpuResolveCachedCount++;
            return CompletePointerResolve("ReuseLastClickDown", _lastClickDownTarget);
        }
        TracePointerResolveStep("ReuseLastClickDown", reuseDownStart, success: false);

        var bypassCheckStart = Stopwatch.GetTimestamp();
        var bypassClickTargetShortcuts = requiresPreciseTarget && ShouldBypassClickTargetShortcuts(pointerPosition);
        TracePointerResolveStep("BypassClickShortcutsCheck", bypassCheckStart, bypassClickTargetShortcuts);
        SetPointerResolveBypassFlag(bypassClickTargetShortcuts);
        if (requiresPreciseTarget &&
            !bypassClickTargetShortcuts &&
            !delta.PointerMoved &&
            TryReuseCachedPointerResolveTarget(pointerPosition, out var cachedPointerTarget) &&
            cachedPointerTarget != null)
        {
            TracePointerResolveStep("PointerResolveCacheReuse", Stopwatch.GetTimestamp(), success: true, cachedPointerTarget.GetType().Name);
            _clickCpuResolveCachedCount++;
            return CompletePointerResolve("PointerResolveCacheReuse", cachedPointerTarget);
        }

        if (!requiresPreciseTarget)
        {
            var contextMenuOpenHitTestStart = Stopwatch.GetTimestamp();
            if (TryFindOpenContextMenu(out _))
            {
                _lastInputHitTestCount++;
                if (ListBoxSelectCPUDiagnostics.RequiresDetailedHitTestMetrics)
                {
                    var overlayHit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition, out var overlayMetrics);
                    _lastPointerResolveHitTestMetrics = overlayMetrics;
                    TracePointerResolveStep("ContextMenuOpenHitTest", contextMenuOpenHitTestStart, success: overlayHit != null, overlayHit?.GetType().Name);
                    return CompletePointerResolve("ContextMenuOpenHitTest", overlayHit);
                }
                var contextMenuHit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition);
                TracePointerResolveStep("ContextMenuOpenHitTest", contextMenuOpenHitTestStart, success: contextMenuHit != null, contextMenuHit?.GetType().Name);
                return CompletePointerResolve("ContextMenuOpenHitTest", contextMenuHit);
            }
            TracePointerResolveStep("ContextMenuOpenHitTest", contextMenuOpenHitTestStart, success: false);

            // CPU-first path: keep current hover target during high-frequency move/wheel
            // and only re-resolve precisely on button transitions.
            var hoverReuseStart = Stopwatch.GetTimestamp();
            if (_inputState.HoveredElement != null)
            {
                TracePointerResolveStep("HoverReuse", hoverReuseStart, success: true, _inputState.HoveredElement.GetType().Name);
                return CompletePointerResolve("HoverReuse", _inputState.HoveredElement);
            }
            TracePointerResolveStep("HoverReuse", hoverReuseStart, success: false);

            var hoverBypassStart = Stopwatch.GetTimestamp();
            if ((ForceBypassMoveHitTest || string.Equals(
                    Environment.GetEnvironmentVariable("INKKSLINGER_BYPASS_MOVE_HITTEST"),
                    "1",
                    StringComparison.Ordinal)) &&
                _inputState.HoveredElement != null)
            {
                TracePointerResolveStep("HoverBypass", hoverBypassStart, success: true, _inputState.HoveredElement.GetType().Name);
                return CompletePointerResolve("HoverBypass", _inputState.HoveredElement);
            }
            TracePointerResolveStep("HoverBypass", hoverBypassStart, success: false);
        }

        var hoverNoInputStart = Stopwatch.GetTimestamp();
        if (!requiresPreciseTarget &&
            !delta.PointerMoved &&
            delta.WheelDelta == 0)
        {
            TracePointerResolveStep("HoverNoInput", hoverNoInputStart, success: _inputState.HoveredElement != null, _inputState.HoveredElement?.GetType().Name);
            return CompletePointerResolve("HoverNoInput", _inputState.HoveredElement);
        }
        TracePointerResolveStep("HoverNoInput", hoverNoInputStart, success: false);

        if (requiresPreciseTarget && !bypassClickTargetShortcuts)
        {
            var itemsHostStart = Stopwatch.GetTimestamp();
            if (TryResolveItemsHostContainerTarget(pointerPosition, out var fastContainerTarget, out var itemsHostDetail, out var skipCachedAnchorPaths) &&
                fastContainerTarget != null)
            {
                TracePointerResolveStep("ItemsHostContainerFastPath", itemsHostStart, success: true, $"{fastContainerTarget.GetType().Name}; {itemsHostDetail}");
                _clickCpuResolveCachedCount++;
                return CompletePointerResolve("ItemsHostContainerFastPath", fastContainerTarget);
            }
            TracePointerResolveStep("ItemsHostContainerFastPath", itemsHostStart, success: false, itemsHostDetail);

            var hoveredSubtreeStart = Stopwatch.GetTimestamp();
            if (TryResolvePreciseClickTargetWithinHoveredSubtree(pointerPosition, out var hoveredSubtreeTarget) &&
                hoveredSubtreeTarget != null)
            {
                TracePointerResolveStep("HoveredSubtreeHitTest", hoveredSubtreeStart, success: true, hoveredSubtreeTarget.GetType().Name);
                _clickCpuResolveHoveredCount++;
                return CompletePointerResolve("HoveredSubtreeHitTest", hoveredSubtreeTarget);
            }
            TracePointerResolveStep("HoveredSubtreeHitTest", hoveredSubtreeStart, success: false);

            var cachedAnchorSubtreeStart = Stopwatch.GetTimestamp();
            if (!skipCachedAnchorPaths &&
                TryResolvePreciseClickTargetWithinAnchorSubtree(_cachedClickTarget, pointerPosition, out var cachedAnchorTarget) &&
                cachedAnchorTarget != null)
            {
                TracePointerResolveStep("CachedAnchorSubtreeHitTest", cachedAnchorSubtreeStart, success: true, cachedAnchorTarget.GetType().Name);
                _clickCpuResolveCachedCount++;
                return CompletePointerResolve("CachedAnchorSubtreeHitTest", cachedAnchorTarget);
            }
            TracePointerResolveStep(
                "CachedAnchorSubtreeHitTest",
                cachedAnchorSubtreeStart,
                success: false,
                skipCachedAnchorPaths ? "skipped(items-host-hard-miss)" : null);

            var cachedClickCandidateStart = Stopwatch.GetTimestamp();
            if (!skipCachedAnchorPaths &&
                _cachedClickTarget != null &&
                TryResolveClickTargetFromCandidate(_cachedClickTarget, pointerPosition, out var cachedClickTarget) &&
                cachedClickTarget != null)
            {
                TracePointerResolveStep("CachedClickCandidate", cachedClickCandidateStart, success: true, cachedClickTarget.GetType().Name);
                _clickCpuResolveCachedCount++;
                return CompletePointerResolve("CachedClickCandidate", cachedClickTarget);
            }
            TracePointerResolveStep(
                "CachedClickCandidate",
                cachedClickCandidateStart,
                success: false,
                skipCachedAnchorPaths ? "skipped(items-host-hard-miss)" : null);

            var hoveredClickCandidateStart = Stopwatch.GetTimestamp();
            if (TryResolveClickTargetFromHovered(pointerPosition, out var hoveredClickTarget) &&
                hoveredClickTarget != null)
            {
                TracePointerResolveStep("HoveredClickCandidate", hoveredClickCandidateStart, success: true, hoveredClickTarget.GetType().Name);
                _clickCpuResolveHoveredCount++;
                return CompletePointerResolve("HoveredClickCandidate", hoveredClickTarget);
            }
            TracePointerResolveStep("HoveredClickCandidate", hoveredClickCandidateStart, success: false);

            var cachedHostSubtreeStart = Stopwatch.GetTimestamp();
            if (!skipCachedAnchorPaths &&
                TryGetClickHostAnchor(_cachedClickTarget, out var cachedHostAnchor) &&
                TryResolvePreciseClickTargetWithinAnchorSubtree(cachedHostAnchor, pointerPosition, out var cachedHostTarget) &&
                cachedHostTarget != null)
            {
                TracePointerResolveStep("CachedHostSubtreeHitTest", cachedHostSubtreeStart, success: true, cachedHostTarget.GetType().Name);
                _clickCpuResolveCachedCount++;
                return CompletePointerResolve("CachedHostSubtreeHitTest", cachedHostTarget);
            }
            TracePointerResolveStep(
                "CachedHostSubtreeHitTest",
                cachedHostSubtreeStart,
                success: false,
                skipCachedAnchorPaths ? "skipped(items-host-hard-miss)" : null);

            var topLevelSubtreeStart = Stopwatch.GetTimestamp();
            var hasMultipleTopLevelChildren = HasMultipleTopLevelVisualChildren();
            if (hasMultipleTopLevelChildren)
            {
                var topLevelResolved = TryResolveTopLevelSubtreeHitTest(pointerPosition, out var topLevelSubtreeTarget, out var topLevelDetail);
                if (topLevelResolved && topLevelSubtreeTarget != null)
                {
                    TracePointerResolveStep("TopLevelSubtreeHitTest", topLevelSubtreeStart, success: true, $"{topLevelSubtreeTarget.GetType().Name}; {topLevelDetail}");
                    return CompletePointerResolve("TopLevelSubtreeHitTest", topLevelSubtreeTarget);
                }

                TracePointerResolveStep("TopLevelSubtreeHitTest", topLevelSubtreeStart, success: false, topLevelDetail);
            }
            else
            {
                TracePointerResolveStep("TopLevelSubtreeHitTest", topLevelSubtreeStart, success: false, "skipped(root-child-count<=1)");
            }
        }

        var fullHitTestStart = Stopwatch.GetTimestamp();
        _lastInputHitTestCount++;
        if (requiresPreciseTarget)
        {
            _clickCpuResolveHitTestCount++;
        }
        var finalPath = bypassClickTargetShortcuts ? "OverlayBypassHitTest" : "HitTest";
        if (ListBoxSelectCPUDiagnostics.RequiresDetailedHitTestMetrics)
        {
            var hit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition, out var metrics);
            _lastPointerResolveHitTestMetrics = metrics;
            TracePointerResolveStep(finalPath, fullHitTestStart, success: hit != null, hit?.GetType().Name);
            return CompletePointerResolve(finalPath, hit);
        }

        var fallbackHit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition);
        TracePointerResolveStep(finalPath, fullHitTestStart, success: fallbackHit != null, fallbackHit?.GetType().Name);
        return CompletePointerResolve(finalPath, fallbackHit);
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

        if (TryFindOpenContextMenu(out var openContextMenu) &&
            openContextMenu.HandleMouseWheelFromInput(pointerPosition, delta))
        {
            TrackWheelPointerPosition(pointerPosition);
            ObserveScrollCpuWheelDispatch(delta, didPreciseRetarget, _lastInputHitTestCount - wheelHitTestsBefore, wheelHandleMs);
            ObserveControlHotspotDispatch(openContextMenu, Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds);
            return;
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
        if (IsPointInsideElementSlot(element, pointerPosition.X, pointerPosition.Y))
        {
            return true;
        }

        // Fallback for non-framework visuals with custom hit geometry.
        return element is not FrameworkElement && element.HitTest(pointerPosition);
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
            UIElement? hit;
            if (ListBoxSelectCPUDiagnostics.RequiresDetailedHitTestMetrics)
            {
                hit = VisualTreeHelper.HitTest(candidate, pointerPosition, out var metrics);
                _lastPointerResolveHitTestMetrics = metrics;
            }
            else
            {
                hit = VisualTreeHelper.HitTest(candidate, pointerPosition);
            }

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
        UIElement? hit;
        if (ListBoxSelectCPUDiagnostics.RequiresDetailedHitTestMetrics)
        {
            hit = VisualTreeHelper.HitTest(anchor, pointerPosition, out var metrics);
            _lastPointerResolveHitTestMetrics = metrics;
        }
        else
        {
            hit = VisualTreeHelper.HitTest(anchor, pointerPosition);
        }

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

    private bool IsNarrowHoveredAnchor(UIElement anchor)
    {
        if (anchor is ListBoxItem or ListViewItem or DataGridRow or MenuItem or ComboBoxItem or TabItem)
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
            ListBoxItem or ListViewItem or DataGridRow or MenuItem or ComboBoxItem or TabItem;
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
        if (element is Button or ITextInputControl or ScrollViewer or ScrollBar or ListBoxItem or ListViewItem or DataGridRow)
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

