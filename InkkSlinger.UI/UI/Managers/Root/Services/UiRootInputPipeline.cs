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
    private const float SweptPointerPressSampleSpacing = 1f;
    private bool _shouldRevalidateHoverAfterLayoutMutation = true;
    private string _lastPointerResolvePath = "None";
    private HitTestMetrics? _lastPointerResolveHitTestMetrics;
    private ContextMenu? _lastKnownOpenContextMenu;
    private int _overlayCandidateVersion;
    private OverlayCandidate _cachedTopOverlayCandidate;
    private int _cachedTopOverlayCandidateVersion = -1;
    private bool _cachedTopOverlayCandidateHasValue;
    private bool _hasCachedTopOverlayCandidate;
    private readonly HashSet<Menu> _keyboardMenuScopeDedupSet = new(ReferenceEqualityComparer.Instance);
    private ContextMenu? _cachedTopContextMenu;
    private int _cachedTopContextMenuVersion = -1;
    private bool _cachedTopContextMenuHasValue;
    private bool _hasCachedTopContextMenu;
    private bool _suppressNextLeftReleaseAfterOverlayDismiss;
    private bool _suppressNextRightReleaseAfterOverlayDismiss;
    private bool _suppressCurrentLeftPressGesture;
    private bool _suppressCurrentRightPressGesture;
    private UIElement? _toolTipHoverTarget;
    private ToolTip? _activeToolTip;
    private UIElement? _activeToolTipTarget;
    private double _toolTipHoverElapsedMs;
    private double _activeToolTipElapsedMs;
    private double _timeSinceToolTipClosedMs = double.PositiveInfinity;

    private void RunInputAndEventsPhase(GameTime gameTime)
    {
        ResetInputPhaseTelemetry(includeVisualUpdate: true);
        _lastVisualUpdateMs = 0d;

        var captureStart = Stopwatch.GetTimestamp();
        var delta = _inputManager.Capture();
        _lastInputCaptureMs = Stopwatch.GetElapsedTime(captureStart).TotalMilliseconds;
        var dispatchStart = Stopwatch.GetTimestamp();
        ProcessInputDelta(delta);
        if (ShouldTickToolTipLifecycle())
        {
            var tooltipStart = Stopwatch.GetTimestamp();
            TickToolTipLifecycle(gameTime.ElapsedGameTime.TotalMilliseconds);
            _lastInputToolTipLifecycleMs = Stopwatch.GetElapsedTime(tooltipStart).TotalMilliseconds;
        }
        _lastInputDispatchMs = Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds;

        var updateStart = Stopwatch.GetTimestamp();
        RunFrameUpdateParticipants(gameTime);
        _lastVisualUpdateMs = Stopwatch.GetElapsedTime(updateStart).TotalMilliseconds;
    }

    internal void RunInputDeltaForTests(InputDelta delta)
    {
        RunInputDeltaForTests(delta, 0d);
    }

    internal void RunInputDeltaForTests(InputDelta delta, double elapsedMs)
    {
        ResetInputPhaseTelemetry(includeVisualUpdate: false);
        var dispatchStart = Stopwatch.GetTimestamp();
        ProcessInputDelta(delta);
        if (ShouldTickToolTipLifecycle())
        {
            var tooltipStart = Stopwatch.GetTimestamp();
            TickToolTipLifecycle(Math.Max(0d, elapsedMs));
            _lastInputToolTipLifecycleMs = Stopwatch.GetElapsedTime(tooltipStart).TotalMilliseconds;
        }
        _lastInputDispatchMs = Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds;
    }

    private void ResetInputPhaseTelemetry(bool includeVisualUpdate)
    {
        _inputTelemetry = default;
        if (includeVisualUpdate)
        {
            _lastVisualUpdateMs = 0d;
        }
    }

    private void ProcessInputDelta(InputDelta delta)
    {
        _inputState.LastPointerPosition = delta.Current.PointerPosition;
        _lastPointerResolvePath = "None";
        _lastPointerResolveHitTestMetrics = null;
        var pointerStart = Stopwatch.GetTimestamp();
        _inputState.CurrentModifiers = GetModifiers(delta.Current.Keyboard);
        _shouldRevalidateHoverAfterLayoutMutation = !delta.PointerMoved;
        if (delta.IsEmpty)
        {
            _lastPointerResolvePath = "NoInputBypass";
            _lastInputPointerDispatchMs = Stopwatch.GetElapsedTime(pointerStart).TotalMilliseconds;
            return;
        }

        var clickResolveHitTests = 0;
        var pointerResolveStart = Stopwatch.GetTimestamp();
        var clickHitTestsBeforeResolve = _lastInputHitTestCount;
        var pointerTarget = ResolvePointerTarget(delta);
        var pressedPointerPosition = delta.Current.PointerPosition;
        if (TryResolveSweptPointerPressTarget(delta, pointerTarget, out var sweptPointerTarget, out var sweptPointerPosition) &&
            sweptPointerTarget != null)
        {
            pointerTarget = FinalizePointerResolve("SweptPressPathHitTest", sweptPointerTarget);
            pressedPointerPosition = sweptPointerPosition;
        }
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
            if (TryResolveSweptPointerHoverTarget(delta, pointerTarget, out var sweptHoverTarget, out var sweptHoverPointerPosition) &&
                sweptHoverTarget != null)
            {
                var sweptHoverStart = Stopwatch.GetTimestamp();
                UpdateHover(sweptHoverTarget, sweptHoverPointerPosition);
                hoverTicks += Stopwatch.GetTimestamp() - sweptHoverStart;
            }

            var hoverStart = Stopwatch.GetTimestamp();
            UpdateHover(pointerTarget, delta.Current.PointerPosition);
            hoverTicks += Stopwatch.GetTimestamp() - hoverStart;
            var contextMenuMoveHandled = false;
            if (_inputState.CapturedPointerElement == null &&
                TryResolveContextMenuMenuItemTarget(delta.Current.PointerPosition, pointerTarget, out var contextMenuHoverItem))
            {
                var hoverHandleStart = Stopwatch.GetTimestamp();
                contextMenuHoverItem.HandlePointerMoveFromInput();
                _lastInputPointerMoveHandlerMs += Stopwatch.GetElapsedTime(hoverHandleStart).TotalMilliseconds;
                contextMenuMoveHandled = true;
            }

            var outerContextMenuProbeStart = Stopwatch.GetTimestamp();
            var hasOpenContextMenu = _lastKnownOpenContextMenu is { IsOpen: true } || TryFindOpenContextMenu(out _);
            _lastInputPointerRouteOuterContextMenuProbeMs += Stopwatch.GetElapsedTime(outerContextMenuProbeStart).TotalMilliseconds;
            var outerGateStart = Stopwatch.GetTimestamp();
            var shouldRouteMove = !contextMenuMoveHandled &&
                                  (hasOpenContextMenu ||
                                  !ReferenceEquals(previousHovered, _inputState.HoveredElement) ||
                                  _inputState.CapturedPointerElement != null);
            _lastInputPointerRouteOuterGateMs += Stopwatch.GetElapsedTime(outerGateStart).TotalMilliseconds;
            if (shouldRouteMove)
            {
                var routeStart = Stopwatch.GetTimestamp();
                DispatchPointerMove(pointerTarget, delta.Current.PointerPosition);
                _lastInputPointerRouteOuterDispatchCallMs += Stopwatch.GetElapsedTime(routeStart).TotalMilliseconds;
                _lastInputPointerRouteDispatchCount++;
                pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
            }
            if (UseSoftwareCursor)
            {
                _mustDrawNextFrame = true;
            }
        }

        if (delta.LeftPressed)
        {
            DismissToolTipForPointerInteraction();
            _suppressCurrentLeftPressGesture = false;
            _lastClickDownTarget = pointerTarget;
            _lastClickDownPointerPosition = pressedPointerPosition;
            _hasLastClickDownPointerPosition = true;
            var routeStart = Stopwatch.GetTimestamp();
            DispatchMouseDown(pointerTarget, pressedPointerPosition, MouseButton.Left);
            if (!_suppressCurrentLeftPressGesture)
            {
                _ = InputGestureService.Execute(
                    MouseButton.Left,
                    _inputState.CurrentModifiers,
                    _inputState.FocusedElement,
                    _visualRoot);
            }
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
            DismissToolTipForPointerInteraction();
            _suppressCurrentRightPressGesture = false;
            var routeStart = Stopwatch.GetTimestamp();
            DispatchMouseDown(pointerTarget, pressedPointerPosition, MouseButton.Right);
            if (!_suppressCurrentRightPressGesture)
            {
                _ = InputGestureService.Execute(
                    MouseButton.Right,
                    _inputState.CurrentModifiers,
                    _inputState.FocusedElement,
                    _visualRoot);
            }
            clickResolveHitTests = 0;
            pointerRouteTicks += Stopwatch.GetTimestamp() - routeStart;
        }

        if (delta.MiddlePressed)
        {
            DismissToolTipForPointerInteraction();
            _ = InputGestureService.Execute(
                MouseButton.Middle,
                _inputState.CurrentModifiers,
                _inputState.FocusedElement,
                _visualRoot);
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

        if (ShouldInvalidateCommandRequeryForInput(delta))
        {
            var commandRequeryStart = Stopwatch.GetTimestamp();
            CommandManager.InvalidateRequerySuggested();
            _lastInputCommandRequeryMs = Stopwatch.GetElapsedTime(commandRequeryStart).TotalMilliseconds;
        }

    }

    private static bool ShouldInvalidateCommandRequeryForInput(InputDelta delta)
    {
        if (delta.LeftPressed || delta.LeftReleased ||
            delta.RightPressed || delta.RightReleased ||
            delta.MiddlePressed || delta.MiddleReleased)
        {
            return true;
        }

        if (delta.PressedKeys.Count > 0 || delta.ReleasedKeys.Count > 0 || delta.TextInput.Count > 0)
        {
            return true;
        }

        return false;
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
            if (TryResolveClickTargetFromCandidate(_lastClickUpTarget, pointerPosition, out var reusedClickUpTarget) &&
                reusedClickUpTarget != null)
            {
                return FinalizePointerResolve("ReuseLastClickUp", reusedClickUpTarget);
            }

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
            if (TryResolveClickTargetFromCandidate(_lastClickDownTarget, pointerPosition, out var reusedClickDownTarget) &&
                reusedClickDownTarget != null)
            {
                return FinalizePointerResolve("ReuseLastClickDown", reusedClickDownTarget);
            }

            return FinalizePointerResolve("ReuseLastClickDown", _lastClickDownTarget);
        }

        var bypassClickTargetShortcuts = requiresPreciseTarget && ShouldBypassClickTargetShortcuts(pointerPosition);
        if (requiresPreciseTarget &&
            TryResolveSubspaceViewport2DPreciseClickTarget(pointerPosition, out var subspaceViewport2DTarget) &&
            subspaceViewport2DTarget != null)
        {
            return FinalizePointerResolve("SubspaceViewport2DHitTest", subspaceViewport2DTarget);
        }

        if (requiresPreciseTarget &&
            !bypassClickTargetShortcuts &&
            !delta.PointerMoved &&
            TryReuseCachedPointerResolveTarget(pointerPosition, out var cachedPointerTarget) &&
            cachedPointerTarget != null)
        {
            _clickCpuResolveCachedCount++;
            if (TryResolveClickTargetFromCandidate(cachedPointerTarget, pointerPosition, out var resolvedCachedPointerTarget) &&
                resolvedCachedPointerTarget != null)
            {
                return FinalizePointerResolve("PointerResolveCacheReuse", resolvedCachedPointerTarget);
            }

            return FinalizePointerResolve("PointerResolveCacheReuse", cachedPointerTarget);
        }

        if (!requiresPreciseTarget)
        {
            var contextMenuCheckStart = Stopwatch.GetTimestamp();
            var hasOpenContextMenu = TryFindOpenContextMenu(out _);
            _lastInputPointerResolveContextMenuCheckMs += Stopwatch.GetElapsedTime(contextMenuCheckStart).TotalMilliseconds;
            if (hasOpenContextMenu)
            {
                _lastInputHitTestCount++;
                var contextMenuHitTestStart = Stopwatch.GetTimestamp();
                var contextMenuHit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition);
                _lastInputPointerResolveFinalHitTestMs += Stopwatch.GetElapsedTime(contextMenuHitTestStart).TotalMilliseconds;
                return FinalizePointerResolve("ContextMenuOpenHitTest", contextMenuHit);
            }

            var hoverReuseCheckStart = Stopwatch.GetTimestamp();
            var canReuseHoveredTarget =
                _inputState.HoveredElement != null &&
                (delta.WheelDelta != 0 || ShouldReuseHoveredTargetForPointerMove(_inputState.HoveredElement)) &&
                PointerInsideHoveredTargetForReuse(_inputState.HoveredElement, pointerPosition);
            _lastInputPointerResolveHoverReuseCheckMs += Stopwatch.GetElapsedTime(hoverReuseCheckStart).TotalMilliseconds;
            if (canReuseHoveredTarget)
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

        if (!requiresPreciseTarget &&
            delta.PointerMoved &&
            TryResolvePointerMoveWithinHoveredHostSubtree(pointerPosition, out var hoveredHostTarget) &&
            hoveredHostTarget != null)
        {
            return FinalizePointerResolve("HoveredHostSubtreeHitTest", hoveredHostTarget, updatePointerCache: false);
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
        var finalHitTestStart = Stopwatch.GetTimestamp();
        var hit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition, out var metrics);
        _lastInputPointerResolveFinalHitTestMs += Stopwatch.GetElapsedTime(finalHitTestStart).TotalMilliseconds;
        _lastPointerResolveHitTestMetrics = metrics;
        hit = ResolveSubspaceViewport2DClickCandidate(hit, pointerPosition);

        if (requiresPreciseTarget)
        {
            if (TryResolveClickTargetFromCandidate(hit, pointerPosition, out var resolvedClickTarget) &&
                resolvedClickTarget != null)
            {
                return FinalizePointerResolve(finalPath, resolvedClickTarget);
            }

            if (hit != null && IsClickCapableElement(hit))
            {
                return FinalizePointerResolve(finalPath, hit);
            }
        }

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

    private bool TryResolveSweptPointerPressTarget(
        InputDelta delta,
        UIElement? currentTarget,
        out UIElement? target,
        out Vector2 pointerPosition)
    {
        target = null;
        pointerPosition = delta.Current.PointerPosition;
        if (!delta.PointerMoved ||
            !(delta.LeftPressed || delta.RightPressed) ||
            _inputState.CapturedPointerElement != null)
        {
            return false;
        }

        var currentPointerPosition = delta.Current.PointerPosition;
        if (TryResolveClickTargetFromCandidate(currentTarget, currentPointerPosition, out var resolvedCurrentTarget) &&
            resolvedCurrentTarget != null)
        {
            return false;
        }

        var previousPointerPosition = delta.Previous.PointerPosition;
        var deltaVector = currentPointerPosition - previousPointerPosition;
        var maxAxisDistance = MathF.Max(MathF.Abs(deltaVector.X), MathF.Abs(deltaVector.Y));
        var sampleCount = (int)MathF.Ceiling(maxAxisDistance / SweptPointerPressSampleSpacing);
        if (sampleCount <= 1)
        {
            return false;
        }

        for (var sampleIndex = sampleCount - 1; sampleIndex >= 1; sampleIndex--)
        {
            var progress = sampleIndex / (float)sampleCount;
            var samplePosition = Vector2.Lerp(previousPointerPosition, currentPointerPosition, progress);
            if (!TryResolveSweptClickTargetAtPoint(samplePosition, out target) || target == null)
            {
                continue;
            }

            pointerPosition = samplePosition;
            return true;
        }

        return false;
    }

    private bool TryResolveSweptPointerHoverTarget(
        InputDelta delta,
        UIElement? currentTarget,
        out UIElement? target,
        out Vector2 pointerPosition)
    {
        target = null;
        pointerPosition = delta.Current.PointerPosition;
        if (!delta.PointerMoved || _inputState.CapturedPointerElement != null)
        {
            return false;
        }

        var currentPointerPosition = delta.Current.PointerPosition;
        var currentHoverTarget = ResolveHoverTargetCandidate(currentTarget, currentPointerPosition);
        if (currentHoverTarget != null && IsHoverHostElement(currentHoverTarget))
        {
            return false;
        }

        var previousPointerPosition = delta.Previous.PointerPosition;
        var deltaVector = currentPointerPosition - previousPointerPosition;
        var maxAxisDistance = MathF.Max(MathF.Abs(deltaVector.X), MathF.Abs(deltaVector.Y));
        var sampleCount = (int)MathF.Ceiling(maxAxisDistance / SweptPointerPressSampleSpacing);
        if (sampleCount <= 1)
        {
            return false;
        }

        for (var sampleIndex = 1; sampleIndex < sampleCount; sampleIndex++)
        {
            var progress = sampleIndex / (float)sampleCount;
            var samplePosition = Vector2.Lerp(previousPointerPosition, currentPointerPosition, progress);
            if (!TryResolveSweptHoverTargetAtPoint(samplePosition, out target) || target == null)
            {
                continue;
            }

            pointerPosition = samplePosition;
            return true;
        }

        return false;
    }

    private bool TryResolveSubspaceViewport2DPreciseClickTarget(Vector2 pointerPosition, out UIElement? target)
    {
        target = null;
        EnsureVisualIndexCurrent();

        var indexedVisuals = _visualIndex.Nodes;
        for (var index = indexedVisuals.Count - 1; index >= 0; index--)
        {
            if (indexedVisuals[index].Visual is not RenderSurface renderSurface ||
                !renderSurface.HitTest(pointerPosition) ||
                !renderSurface.TryHitTestSubspaceViewport2Ds(pointerPosition, out var subspaceViewport2DHit) ||
                subspaceViewport2DHit == null)
            {
                continue;
            }

            if (TryResolveClickTargetFromCandidate(subspaceViewport2DHit, pointerPosition, out var resolvedSubspaceViewport2DTarget) &&
                resolvedSubspaceViewport2DTarget != null)
            {
                target = resolvedSubspaceViewport2DTarget;
                return true;
            }

            target = subspaceViewport2DHit;
            return true;
        }

        return false;
    }

    private bool TryResolveSweptClickTargetAtPoint(Vector2 pointerPosition, out UIElement? target)
    {
        target = null;
        _lastInputHitTestCount++;
        _clickCpuResolveHitTestCount++;
        var hitTestStart = Stopwatch.GetTimestamp();
        var hit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition, out var metrics);
        _lastInputPointerResolveFinalHitTestMs += Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
        _lastPointerResolveHitTestMetrics = metrics;
        if (hit == null)
        {
            return false;
        }

        return TryResolveClickTargetFromCandidate(hit, pointerPosition, out target) && target != null;
    }

    private bool TryResolveSweptHoverTargetAtPoint(Vector2 pointerPosition, out UIElement? target)
    {
        target = null;
        _lastInputHitTestCount++;
        var hitTestStart = Stopwatch.GetTimestamp();
        var hit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition);
        _lastInputPointerResolveFinalHitTestMs += Stopwatch.GetElapsedTime(hitTestStart).TotalMilliseconds;
        target = ResolveHoverTargetCandidate(hit, pointerPosition);
        return target != null && IsHoverHostElement(target);
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

        if (CanvasThumbInvestigationLog.ShouldTrace(
                _visualRoot,
                _inputState.LastPointerPosition,
                target,
                _inputState.HoveredElement,
                _inputState.CapturedPointerElement))
        {
            CanvasThumbInvestigationLog.Write(
                "Resolve",
                $"path={path} pointer={CanvasThumbInvestigationLog.DescribePointer(_inputState.LastPointerPosition)} target={CanvasThumbInvestigationLog.DescribeElement(target)} hovered={CanvasThumbInvestigationLog.DescribeElement(_inputState.HoveredElement)} captured={CanvasThumbInvestigationLog.DescribeElement(_inputState.CapturedPointerElement)} metrics={CanvasThumbInvestigationLog.DescribeHitTestMetrics(_lastPointerResolveHitTestMetrics)}");
        }

        return target;
    }

    private int GetPointerResolveStateStamp()
    {
        return _pointerResolveStateVersion;
    }

    private void UpdateHover(UIElement? hovered)
    {
        UpdateHover(hovered, _inputState.LastPointerPosition);
    }

    private void UpdateHover(UIElement? hovered, Vector2 pointerPosition)
    {
        hovered = ResolveHoverTargetCandidate(hovered, pointerPosition);
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

        if (CanvasThumbInvestigationLog.ShouldTrace(
            _visualRoot,
            pointerPosition,
            previousHovered,
            hovered,
            _inputState.CapturedPointerElement))
        {
            CanvasThumbInvestigationLog.Write(
            "Hover",
            $"pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} previous={CanvasThumbInvestigationLog.DescribeElement(previousHovered)} current={CanvasThumbInvestigationLog.DescribeElement(hovered)} cachedClick={CanvasThumbInvestigationLog.DescribeElement(_cachedClickTarget)} captured={CanvasThumbInvestigationLog.DescribeElement(_inputState.CapturedPointerElement)}");
        }

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

        UpdateToolTipHoverTarget(hovered);
    }

    private UIElement? ResolveHostedHoverTargetWithinTextInputSubtree(UIElement? hovered)
    {
        return ResolveHostedHoverTargetWithinTextInputSubtree(hovered, _inputState.LastPointerPosition);
    }

    private UIElement? ResolveHostedHoverTargetWithinTextInputSubtree(UIElement? hovered, Vector2 pointerPosition)
    {
        if (hovered == null)
        {
            return null;
        }

        if (!TryResolveHoverTargetWithinTextInputSubtree(hovered, pointerPosition, out var hostedHoverTarget) ||
            hostedHoverTarget == null)
        {
            return hovered;
        }

        return hostedHoverTarget;
    }

    private UIElement? ResolveHoverTargetCandidate(UIElement? candidate, Vector2 pointerPosition)
    {
        candidate = ResolveSubspaceViewport2DClickCandidate(candidate, pointerPosition);
        candidate = ResolveHostedHoverTargetWithinTextInputSubtree(candidate, pointerPosition);
        return PromoteHoverTarget(candidate);
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
        return element is ITextInputControl or Button or Thumb or GridSplitter or ListBoxItem or DataGridRow or TabItem or TreeViewItem;
    }

    private static void SetHoverState(UIElement? element, bool isMouseOver)
    {
        switch (element)
        {
            case null:
                return;
            case ITextInputControl textInput:
            {
                textInput.SetMouseOverFromInput(isMouseOver);
                return;
            }
            case Button button:
            {
                button.SetMouseOverFromInput(isMouseOver);
                return;
            }
            case Thumb thumb:
            {
                thumb.SetMouseOverFromInput(isMouseOver);
                return;
            }
            case GridSplitter gridSplitter:
            {
                gridSplitter.SetMouseOverFromInput(isMouseOver);
                return;
            }
            case ListBoxItem listBoxItem:
            {
                if (listBoxItem.IsMouseOver != isMouseOver)
                {
                    listBoxItem.IsMouseOver = isMouseOver;
                }
                return;
            }
            case DataGridRow dataGridRow:
            {
                if (dataGridRow.IsMouseOver != isMouseOver)
                {
                    dataGridRow.IsMouseOver = isMouseOver;
                }
                return;
            }
            case TabItem tabItem:
            {
                if (tabItem.IsMouseOver != isMouseOver)
                {
                    tabItem.IsMouseOver = isMouseOver;
                }
                return;
            }
            case TreeViewItem treeViewItem:
            {
                if (treeViewItem.IsMouseOver != isMouseOver)
                {
                    treeViewItem.IsMouseOver = isMouseOver;
                }
                return;
            }
        }
    }

    private void DismissToolTipForPointerInteraction()
    {
        _toolTipHoverTarget = null;
        _toolTipHoverElapsedMs = 0d;
        CloseActiveToolTip();
    }

    private void UpdateToolTipHoverTarget(UIElement? hovered)
    {
        if (TryResolveToolTipSourceFromHover(hovered, out var resolvedTarget))
        {
            if (ReferenceEquals(_toolTipHoverTarget, resolvedTarget))
            {
                return;
            }

            _toolTipHoverTarget = resolvedTarget;
            _toolTipHoverElapsedMs = 0d;
            if (_activeToolTipTarget != null &&
                !ReferenceEquals(_activeToolTipTarget, resolvedTarget))
            {
                CloseActiveToolTip();
            }

            return;
        }

        _toolTipHoverTarget = null;
        _toolTipHoverElapsedMs = 0d;
        CloseActiveToolTip();
    }

    private bool ShouldTickToolTipLifecycle()
    {
        return _activeToolTip != null ||
               _toolTipHoverTarget != null ||
               _timeSinceToolTipClosedMs != double.PositiveInfinity;
    }

    private void TickToolTipLifecycle(double elapsedMs)
    {
        if (_timeSinceToolTipClosedMs != double.PositiveInfinity)
        {
            _timeSinceToolTipClosedMs += elapsedMs;
        }

        if (_activeToolTip != null)
        {
            if (!_activeToolTip.IsOpen)
            {
                CloseActiveToolTip();
                return;
            }

            _activeToolTipElapsedMs += elapsedMs;
            if (_activeToolTipTarget == null ||
                !IsElementConnectedToVisualRoot(_activeToolTipTarget) ||
                !_activeToolTipTarget.IsEnabled ||
                !ReferenceEquals(_toolTipHoverTarget, _activeToolTipTarget))
            {
                CloseActiveToolTip();
                return;
            }

            var showDuration = ToolTipService.GetShowDuration(_activeToolTipTarget);
            if (_activeToolTipElapsedMs >= showDuration)
            {
                _toolTipHoverTarget = null;
                _toolTipHoverElapsedMs = 0d;
                CloseActiveToolTip();
                return;
            }
        }

        if (_toolTipHoverTarget == null ||
            _activeToolTip != null ||
            !IsElementConnectedToVisualRoot(_toolTipHoverTarget) ||
            !_toolTipHoverTarget.IsEnabled)
        {
            return;
        }

        var initialDelay = ToolTipService.GetInitialShowDelay(_toolTipHoverTarget);
        var betweenDelay = ToolTipService.GetBetweenShowDelay(_toolTipHoverTarget);
        var requiredDelay = _timeSinceToolTipClosedMs <= betweenDelay ? 0 : initialDelay;
        _toolTipHoverElapsedMs += elapsedMs;
        if (_toolTipHoverElapsedMs < requiredDelay)
        {
            return;
        }

        if (!TryBuildToolTipForTarget(_toolTipHoverTarget, out var tooltip) ||
            tooltip == null)
        {
            return;
        }

        var host = FindHostPanelForElement(_toolTipHoverTarget);
        if (host == null)
        {
            return;
        }

        tooltip.ShowFor(host, _toolTipHoverTarget);
        _activeToolTip = tooltip;
        _activeToolTipTarget = _toolTipHoverTarget;
        _activeToolTipElapsedMs = 0d;
    }

    private static bool TryResolveToolTipSourceFromHover(UIElement? hovered, out UIElement target)
    {
        for (var current = hovered; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (!ToolTipService.GetIsEnabled(current))
            {
                continue;
            }

            if (ToolTipService.GetToolTip(current) != null)
            {
                target = current;
                return true;
            }
        }

        target = null!;
        return false;
    }

    private static bool TryBuildToolTipForTarget(UIElement target, out ToolTip tooltip)
    {
        var toolTipValue = ToolTipService.GetToolTip(target);
        switch (toolTipValue)
        {
            case null:
                tooltip = null!;
                return false;
            case ToolTip providedToolTip:
                tooltip = providedToolTip;
                return true;
            case UIElement element:
                throw new InvalidOperationException(
                    $"ToolTipService.ToolTip does not support UIElement values ('{element.GetType().Name}') " +
                    "because reparenting live visuals is unsafe. Use a ToolTip instance instead.");
            default:
                tooltip = new ToolTip
                {
                    Content = new Label
                    {
                        Content = toolTipValue.ToString() ?? string.Empty
                    }
                };
                return true;
        }
    }

    private void CloseActiveToolTip()
    {
        if (_activeToolTip == null)
        {
            return;
        }

        _activeToolTip.Close();
        _activeToolTip = null;
        _activeToolTipTarget = null;
        _activeToolTipElapsedMs = 0d;
        _timeSinceToolTipClosedMs = 0d;
    }

    private void DispatchPointerMove(UIElement? target, Vector2 pointerPosition)
    {
        var routedTarget = _inputState.CapturedPointerElement ?? target;
        if (routedTarget == null)
        {
            return;
        }

        if (CanvasThumbInvestigationLog.ShouldTrace(
                _visualRoot,
                pointerPosition,
                target,
                routedTarget,
                _inputState.HoveredElement,
                _inputState.CapturedPointerElement))
        {
            CanvasThumbInvestigationLog.Write(
                "PointerMove",
                $"pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} target={CanvasThumbInvestigationLog.DescribeElement(target)} routed={CanvasThumbInvestigationLog.DescribeElement(routedTarget)} hovered={CanvasThumbInvestigationLog.DescribeElement(_inputState.HoveredElement)} captured={CanvasThumbInvestigationLog.DescribeElement(_inputState.CapturedPointerElement)} path={_lastPointerResolvePath}");
        }

        var dispatchStart = Stopwatch.GetTimestamp();
        _lastInputPointerEventCount++;
        var routedEventsStart = Stopwatch.GetTimestamp();
        _lastInputRoutedEventCount += 2;
        var previewStart = Stopwatch.GetTimestamp();
        routedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseMoveEvent, new MouseRoutedEventArgs(UIElement.PreviewMouseMoveEvent, pointerPosition, MouseButton.Left, _inputState.CurrentModifiers));
        _lastInputPointerMovePreviewEventMs += Stopwatch.GetElapsedTime(previewStart).TotalMilliseconds;
        var bubbleStart = Stopwatch.GetTimestamp();
        routedTarget.RaiseRoutedEventInternal(UIElement.MouseMoveEvent, new MouseRoutedEventArgs(UIElement.MouseMoveEvent, pointerPosition, MouseButton.Left, _inputState.CurrentModifiers));
        _lastInputPointerMoveBubbleEventMs += Stopwatch.GetElapsedTime(bubbleStart).TotalMilliseconds;
        _lastInputPointerMoveRoutedEventsMs += Stopwatch.GetElapsedTime(routedEventsStart).TotalMilliseconds;

        if (_inputState.CapturedPointerElement is DataGrid dragDataGrid)
        {
            var handlerStart = Stopwatch.GetTimestamp();
            dragDataGrid.HandlePointerMoveFromInput(pointerPosition);
            var elapsed = Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds;
            _lastInputPointerMoveHandlerMs += elapsed;
            _lastInputPointerMoveCapturedDataGridHandlerMs += elapsed;
        }
        else if (_inputState.CapturedPointerElement is ITextInputControl dragTextInput)
        {
            var handlerStart = Stopwatch.GetTimestamp();
            dragTextInput.HandlePointerMoveFromInput(pointerPosition);
            var elapsed = Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds;
            _lastInputPointerMoveHandlerMs += elapsed;
            _lastInputPointerMoveCapturedTextInputHandlerMs += elapsed;
        }
        else if (_inputState.CapturedPointerElement is Thumb dragThumb)
        {
            var handlerStart = Stopwatch.GetTimestamp();
            dragThumb.HandlePointerMoveFromInput(pointerPosition);
            var elapsed = Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds;
            _lastInputPointerMoveHandlerMs += elapsed;
        }
        else if (_inputState.CapturedPointerElement is GridSplitter dragGridSplitter)
        {
            var handlerStart = Stopwatch.GetTimestamp();
            dragGridSplitter.HandlePointerMoveFromInput(pointerPosition);
            var elapsed = Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds;
            _lastInputPointerMoveHandlerMs += elapsed;
        }
        else if (_inputState.CapturedPointerElement is ScrollViewer dragScrollViewer)
        {
            var handlerStart = Stopwatch.GetTimestamp();
            dragScrollViewer.HandlePointerMoveFromInput(pointerPosition);
            var elapsed = Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds;
            _lastInputPointerMoveHandlerMs += elapsed;
            _lastInputPointerMoveCapturedScrollViewerHandlerMs += elapsed;
        }
        else if (_inputState.CapturedPointerElement is Slider dragSlider)
        {
            var handlerStart = Stopwatch.GetTimestamp();
            dragSlider.HandlePointerMoveFromInput(pointerPosition);
            var elapsed = Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds;
            _lastInputPointerMoveHandlerMs += elapsed;
            _lastInputPointerMoveCapturedSliderHandlerMs += elapsed;
        }
        else if (_inputState.CapturedPointerElement is Popup dragPopup)
        {
            var handlerStart = Stopwatch.GetTimestamp();
            dragPopup.HandlePointerMoveFromInput(pointerPosition);
            var elapsed = Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds;
            _lastInputPointerMoveHandlerMs += elapsed;
            _lastInputPointerMoveCapturedPopupHandlerMs += elapsed;
        }
        else if (_inputState.CapturedPointerElement == null)
        {
            if (target is IHyperlinkHoverHost hyperlinkHoverHost)
            {
                var handlerStart = Stopwatch.GetTimestamp();
                hyperlinkHoverHost.UpdateHoveredHyperlinkFromPointer(pointerPosition);
                var elapsed = Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds;
                _lastInputPointerMoveHandlerMs += elapsed;
                _lastInputPointerMoveHyperlinkHandlerMs += elapsed;
            }

            var resolved = TryResolveContextMenuMenuItemTarget(pointerPosition, target, out var contextMenuMenuItem);
            if (resolved)
            {
                var hoverHandleStart = Stopwatch.GetTimestamp();
                contextMenuMenuItem.HandlePointerMoveFromInput();
                var elapsed = Stopwatch.GetElapsedTime(hoverHandleStart).TotalMilliseconds;
                _lastInputPointerMoveHandlerMs += elapsed;
                _lastInputPointerMoveContextMenuItemHandlerMs += elapsed;
                _lastInputPointerMoveDispatchMs += Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds;
                return;
            }

            if (target is MenuItem menuItem)
            {
                var handlerStart = Stopwatch.GetTimestamp();
                menuItem.HandlePointerMoveFromInput();
                var handlerElapsed = Stopwatch.GetElapsedTime(handlerStart).TotalMilliseconds;
                _lastInputPointerMoveHandlerMs += handlerElapsed;
                _lastInputPointerMoveMenuItemHandlerMs += handlerElapsed;
                if (menuItem.OwnerMenu is { } ownerMenu)
                {
                    var focusRestoreStart = Stopwatch.GetTimestamp();
                    TrySynchronizeMenuFocusRestore(ownerMenu);
                    _lastInputPointerMoveMenuFocusRestoreMs += Stopwatch.GetElapsedTime(focusRestoreStart).TotalMilliseconds;
                }

                _lastInputPointerMoveDispatchMs += Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds;
                return;
            }
        }

        _lastInputPointerMoveDispatchMs += Stopwatch.GetElapsedTime(dispatchStart).TotalMilliseconds;
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

        var dismissResult = TryDismissOverlayOnOutsidePointerDown(pointerPosition, target);
        if (dismissResult.Consumed)
        {
            if (button == MouseButton.Left)
            {
                _suppressCurrentLeftPressGesture = true;
                _suppressNextLeftReleaseAfterOverlayDismiss = true;
            }
            else if (button == MouseButton.Right)
            {
                _suppressCurrentRightPressGesture = true;
                _suppressNextRightReleaseAfterOverlayDismiss = true;
            }

            return;
        }

        if (target == null)
        {
            return;
        }

        if (button == MouseButton.Left &&
            CanvasThumbInvestigationLog.ShouldTrace(
                _visualRoot,
                pointerPosition,
                target,
                _inputState.HoveredElement,
                _inputState.CapturedPointerElement))
        {
            CanvasThumbInvestigationLog.Write(
                "MouseDown",
                $"pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} target={CanvasThumbInvestigationLog.DescribeElement(target)} hovered={CanvasThumbInvestigationLog.DescribeElement(_inputState.HoveredElement)} capturedBefore={CanvasThumbInvestigationLog.DescribeElement(_inputState.CapturedPointerElement)} path={_lastPointerResolvePath}");
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
        var dataGridFocusTarget = target != null &&
                                  TryFindAncestor<DataGrid>(target, out var focusedDataGrid) &&
                                  focusedDataGrid != null &&
                                  (target is not ITextInputControl || focusedDataGrid.ShouldRetainFocusForInputTarget(target))
            ? focusedDataGrid
            : null;
        var expanderTarget = target is Expander directExpander
            ? directExpander
            : (target != null && TryFindAncestor<Expander>(target, out var ancestorExpander)
                ? ancestorExpander
                : null);

        if (target is not Menu && target is not MenuItem)
        {
            SetFocus(dataGridFocusTarget ?? textInputTarget ?? target);
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

        if (button == MouseButton.Left &&
            target != null &&
            dataGridFocusTarget != null &&
            dataGridFocusTarget.HandlePointerDownFromInput(target, pointerPosition, _inputState.CurrentModifiers, out var captureDataGrid))
        {
            if (captureDataGrid)
            {
                CapturePointer(dataGridFocusTarget);
            }
        }
        else if (button == MouseButton.Left && target is Button pressedButton)
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
        else if (button == MouseButton.Left && target is Thumb thumb &&
                 thumb.HandlePointerDownFromInput(pointerPosition))
        {
            CapturePointer(target);
        }
        else if (button == MouseButton.Left && target is GridSplitter gridSplitter &&
                 gridSplitter.HandlePointerDownFromInput(pointerPosition))
        {
            CapturePointer(target);
        }
        else if (button == MouseButton.Left && target is Slider slider &&
                 slider.HandlePointerDownFromInput(pointerPosition))
        {
            CapturePointer(target);
        }
        else if (button == MouseButton.Left &&
                 expanderTarget != null &&
                 expanderTarget.HandlePointerDownFromInput(pointerPosition))
        {
            CapturePointer(expanderTarget);
        }
        else if (button == MouseButton.Left && textInputTarget is ITextInputControl textInput)
        {
            textInput.HandlePointerDownFromInput(pointerPosition, extendSelection: (_inputState.CurrentModifiers & ModifierKeys.Shift) != 0);
            CapturePointer(textInputTarget);
        }
        else if (button == MouseButton.Left && target is ScrollViewer scrollViewer &&
                 scrollViewer.HandlePointerDownFromInput(pointerPosition))
        {
            CapturePointer(scrollViewer);
        }
    }

    private void DispatchMouseUp(UIElement? target, Vector2 pointerPosition, MouseButton button)
    {
        if (button == MouseButton.Left &&
            _suppressNextLeftReleaseAfterOverlayDismiss)
        {
            _suppressNextLeftReleaseAfterOverlayDismiss = false;
            return;
        }

        if (button == MouseButton.Right &&
            _suppressNextRightReleaseAfterOverlayDismiss)
        {
            _suppressNextRightReleaseAfterOverlayDismiss = false;
            return;
        }

        var routedTarget = _inputState.CapturedPointerElement ?? target;
        if (routedTarget == null)
        {
            return;
        }

        if (button == MouseButton.Left &&
            CanvasThumbInvestigationLog.ShouldTrace(
                _visualRoot,
                pointerPosition,
                target,
                routedTarget,
                _inputState.HoveredElement,
                _inputState.CapturedPointerElement))
        {
            CanvasThumbInvestigationLog.Write(
                "MouseUp",
                $"pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} target={CanvasThumbInvestigationLog.DescribeElement(target)} routed={CanvasThumbInvestigationLog.DescribeElement(routedTarget)} hovered={CanvasThumbInvestigationLog.DescribeElement(_inputState.HoveredElement)} capturedBefore={CanvasThumbInvestigationLog.DescribeElement(_inputState.CapturedPointerElement)} path={_lastPointerResolvePath}");
        }

        RefreshCachedClickTarget(routedTarget);

        _lastInputPointerEventCount++;
        _lastInputRoutedEventCount += 2;
        routedTarget.RaiseRoutedEventInternal(UIElement.PreviewMouseUpEvent, new MouseRoutedEventArgs(UIElement.PreviewMouseUpEvent, pointerPosition, button, _inputState.CurrentModifiers));
        routedTarget.RaiseRoutedEventInternal(UIElement.MouseUpEvent, new MouseRoutedEventArgs(UIElement.MouseUpEvent, pointerPosition, button, _inputState.CurrentModifiers));
        if (_inputState.CapturedPointerElement is DataGrid capturedDataGrid && button == MouseButton.Left)
        {
            _ = capturedDataGrid.HandlePointerUpFromInput(pointerPosition);
        }
        else if (_inputState.CapturedPointerElement is Button pressedButton && button == MouseButton.Left)
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
        else if (_inputState.CapturedPointerElement is Thumb thumb && button == MouseButton.Left)
        {
            thumb.HandlePointerUpFromInput();
        }
        else if (_inputState.CapturedPointerElement is GridSplitter gridSplitter && button == MouseButton.Left)
        {
            gridSplitter.HandlePointerUpFromInput();
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
            if (!popup.IsOpen)
            {
                TrySynchronizePopupFocusRestore(popup);
            }
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
        var hasWheelCapableAncestor = TryFindWheelAncestors(resolvedTarget, out _, out _);
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

        if (TryFindWheelAncestors(resolvedTarget, out var textInput, out var scrollViewer))
        {
            if (textInput != null &&
                TryHandleTextInputWheel(textInput, delta))
            {
                _cachedWheelTextInputTarget = textInput;
                _cachedWheelScrollViewerTarget = null;
                TrackWheelPointerPosition(pointerPosition);
                return;
            }

            if (scrollViewer != null)
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

    private void RefreshPointerTargetsAfterLayoutMutation()
    {
        var cachedWheelScrollViewerTarget = _cachedWheelScrollViewerTarget;
        _hasCachedPointerResolveTarget = false;
        _cachedPointerResolveTarget = null;
        _cachedClickTarget = null;
        _lastClickDownTarget = null;
        _hasLastClickDownPointerPosition = false;
        _lastClickUpTarget = null;
        _hasLastClickUpPointerPosition = false;
        _cachedWheelTextInputTarget = null;
        _cachedWheelScrollViewerTarget = null;
        BumpPointerResolveStateVersion();
        if (_shouldRevalidateHoverAfterLayoutMutation &&
            (_inputState.HoveredElement != null || _inputState.CapturedPointerElement != null))
        {
            RevalidateHoverAfterLayoutMutation(cachedWheelScrollViewerTarget);
        }
    }

    private void RevalidateHoverAfterLayoutMutation(ScrollViewer? cachedWheelScrollViewerTarget)
    {
        var pointerPosition = _inputState.LastPointerPosition;

        if (_inputState.CapturedPointerElement is UIElement capturedElement)
        {
            UpdateHover(
                IsElementConnectedToVisualRoot(capturedElement)
                    ? capturedElement
                    : null,
                pointerPosition);
            return;
        }

        if (ShouldPreserveWheelDrivenHoverDuringLayoutMutation(pointerPosition, cachedWheelScrollViewerTarget))
        {
            return;
        }

        var hoverTarget = VisualTreeHelper.HitTest(_visualRoot, pointerPosition);
        UpdateHover(hoverTarget, pointerPosition);
    }

    private void RefreshHoverAfterWheelContentMutation(Vector2 pointerPosition, UIElement? mutationRoot = null)
    {
        _hasCachedPointerResolveTarget = false;
        _cachedPointerResolveTarget = null;

        if (ShouldPreserveHoverHostDuringWheelContentMutation(pointerPosition, mutationRoot))
        {
            return;
        }

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

    private bool ShouldPreserveHoverHostDuringWheelContentMutation(Vector2 pointerPosition, UIElement? mutationRoot)
    {
        if (mutationRoot is not ScrollViewer scrollViewer ||
            !ReferenceEquals(_cachedWheelScrollViewerTarget, scrollViewer))
        {
            return false;
        }

        return ShouldPreserveWheelDrivenHoverDuringLayoutMutation(pointerPosition, scrollViewer);
    }

    private bool ShouldPreserveWheelDrivenHoverDuringLayoutMutation(Vector2 pointerPosition, ScrollViewer? cachedWheelScrollViewerTarget)
    {
        if (cachedWheelScrollViewerTarget == null)
        {
            return false;
        }

        var hovered = _inputState.HoveredElement;
        if (hovered == null ||
            !IsElementConnectedToVisualRoot(hovered) ||
            !ShouldDeferWheelDrivenHoverRefresh(hovered))
        {
            return false;
        }

        if (_hasLastWheelPointerPosition &&
            Vector2.DistanceSquared(_lastWheelPointerPosition, pointerPosition) > WheelPointerMoveThresholdSquared)
        {
            return false;
        }

        if (!TryFindAncestor<ScrollViewer>(hovered, out var hoveredScrollViewer) ||
            !ReferenceEquals(hoveredScrollViewer, cachedWheelScrollViewerTarget))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldDeferWheelDrivenHoverRefresh(UIElement element)
    {
        return element is Button or ListBoxItem or DataGridRow or TabItem or TreeViewItem;
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

    private bool PointerLikelyInsideElement(UIElement element, Vector2 pointerPosition)
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

    private bool RequiresPrecisePointerContainmentCheck(UIElement element)
    {
        foreach (var current in GetInputAncestorChain(element))
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
        var hit = VisualTreeHelper.HitTest(_visualRoot, pointerPosition);
        if (hit != null &&
            TryFindWheelAncestors(hit, out var textInput, out var scrollViewer))
        {
            target = textInput ?? scrollViewer;
            return target != null;
        }

        target = null;
        return false;
    }

    private void RefreshCachedWheelTargets(UIElement? hovered)
    {
        var resetStart = Stopwatch.GetTimestamp();
        var previousTextInput = _cachedWheelTextInputTarget;
        var previousScrollViewer = _cachedWheelScrollViewerTarget;
        _cachedWheelTextInputTarget = null;
        _cachedWheelScrollViewerTarget = null;
        if (hovered == null)
        {
            return;
        }

        if (TryFindWheelAncestors(hovered, out var textInput, out var scrollViewer))
        {
            if (textInput != null)
            {
                _cachedWheelTextInputTarget = textInput;
                return;
            }

            if (scrollViewer != null)
            {
                _cachedWheelScrollViewerTarget = scrollViewer;
                return;
            }
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
        EnsureVisualIndexCurrent();
        var rootChildren = _visualIndex.TopLevelVisuals;

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

            if (!IsElementConnectedToVisualRoot(hit))
            {
                detail = $"childIndex={i}; child={candidate.GetType().Name}; result={hit.GetType().Name}; connected=false";
                continue;
            }

            if (TryResolveClickTargetFromCandidate(hit, pointerPosition, out var clickTarget) &&
                clickTarget != null)
            {
                hit = clickTarget;
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
        target = null;
        if (!TryResolvePreciseClickTargetWithinAnchorSubtree(_inputState.HoveredElement, pointerPosition, out var hoveredSubtreeTarget, hoveredStrictMode: true) ||
            hoveredSubtreeTarget == null)
        {
            return false;
        }

        if (TryResolveClickTargetFromCandidate(hoveredSubtreeTarget, pointerPosition, out var resolvedClickTarget) &&
            resolvedClickTarget != null)
        {
            target = resolvedClickTarget;
            return true;
        }

        if (!IsClickCapableElement(hoveredSubtreeTarget))
        {
            return false;
        }

        target = hoveredSubtreeTarget;
        return true;
    }

    private bool TryResolvePointerMoveWithinHoveredHostSubtree(Vector2 pointerPosition, out UIElement? target)
    {
        target = null;
        var hovered = _inputState.HoveredElement;
        UIElement? hostAnchor = null;
        var shouldTrace = CanvasThumbInvestigationLog.ShouldTrace(
            _visualRoot,
            pointerPosition,
            hovered,
            _inputState.CapturedPointerElement);
        if (hovered == null || !IsElementConnectedToVisualRoot(hovered))
        {
            if (shouldTrace)
            {
                CanvasThumbInvestigationLog.Write(
                    "HoveredHostSubtree",
                    $"eligible=false reason=hovered-null-or-detached pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} hovered={CanvasThumbInvestigationLog.DescribeElement(hovered)}");
            }
            return false;
        }

        var pointerInsideHost = false;
        if (!TryGetHoverHostAnchor(hovered, out hostAnchor) ||
            hostAnchor == null ||
            !IsElementConnectedToVisualRoot(hostAnchor) ||
            !(pointerInsideHost = PointerLikelyInsideElement(hostAnchor, pointerPosition)))
        {
            if (shouldTrace)
            {
                CanvasThumbInvestigationLog.Write(
                    "HoveredHostSubtree",
                    $"eligible=false reason=host-anchor-gate pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} hovered={CanvasThumbInvestigationLog.DescribeElement(hovered)} host={CanvasThumbInvestigationLog.DescribeElement(hostAnchor)} pointerInsideHost={pointerInsideHost}");
            }
            return false;
        }

        _lastInputHitTestCount++;
        var hostHitTestStart = Stopwatch.GetTimestamp();
        var hit = VisualTreeHelper.HitTest(hostAnchor, pointerPosition, out var metrics);
        _lastInputPointerResolveFinalHitTestMs += Stopwatch.GetElapsedTime(hostHitTestStart).TotalMilliseconds;
        _lastPointerResolveHitTestMetrics = metrics;
        if (shouldTrace)
        {
            CanvasThumbInvestigationLog.Write(
                "HoveredHostSubtree",
                $"eligible=true pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} hovered={CanvasThumbInvestigationLog.DescribeElement(hovered)} host={CanvasThumbInvestigationLog.DescribeElement(hostAnchor)} hit={CanvasThumbInvestigationLog.DescribeElement(hit)} metrics={CanvasThumbInvestigationLog.DescribeHitTestMetrics(metrics)}");
        }

        if (hit != null && PromoteHoverTarget(hit) == hit)
        {
            return false;
        }

        target = hit;
        return hit != null;
    }

    private bool TryResolvePreciseClickTargetWithinAnchorSubtree(
        UIElement? anchor,
        Vector2 pointerPosition,
        out UIElement? target,
        bool hoveredStrictMode = false)
    {
        target = null;
        var shouldTrace = CanvasThumbInvestigationLog.ShouldTrace(
            _visualRoot,
            pointerPosition,
            anchor,
            _inputState.CapturedPointerElement);
        var anchorConnected = anchor != null && IsElementConnectedToVisualRoot(anchor);
        var broadAnchor = anchor != null && IsBroadClickAnchor(anchor);
        var narrowHoveredAnchor = !hoveredStrictMode || (anchor != null && IsNarrowHoveredAnchor(anchor));
        var pointerInsideAnchor = anchor != null && PointerLikelyInsideElement(anchor, pointerPosition);
        if (anchor == null ||
            !anchorConnected ||
            broadAnchor ||
            !narrowHoveredAnchor ||
            !pointerInsideAnchor)
        {
            if (shouldTrace)
            {
                CanvasThumbInvestigationLog.Write(
                    "PreciseClickSubtree",
                    $"eligible=false pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} anchor={CanvasThumbInvestigationLog.DescribeElement(anchor)} anchorConnected={anchorConnected} broadAnchor={broadAnchor} hoveredStrictMode={hoveredStrictMode} narrowHoveredAnchor={narrowHoveredAnchor} pointerInsideAnchor={pointerInsideAnchor}");
            }
            return false;
        }

        _lastInputHitTestCount++;
        _clickCpuResolveHitTestCount++;
        var hit = VisualTreeHelper.HitTest(anchor, pointerPosition);
        if (shouldTrace)
        {
            CanvasThumbInvestigationLog.Write(
                "PreciseClickSubtree",
                $"eligible=true pointer={CanvasThumbInvestigationLog.DescribePointer(pointerPosition)} anchor={CanvasThumbInvestigationLog.DescribeElement(anchor)} hoveredStrictMode={hoveredStrictMode} hit={CanvasThumbInvestigationLog.DescribeElement(hit)}");
        }

        if (hit == null)
        {
            return false;
        }

        if (TryResolveClickTargetFromCandidate(hit, pointerPosition, out var resolvedTarget) &&
            resolvedTarget != null)
        {
            target = resolvedTarget;
            return true;
        }

        if (IsClickCapableElement(hit))
        {
            target = hit;
            return true;
        }

        return false;
    }

    private bool TryResolveClickTargetWithinTextInputSubtree(
        UIElement candidate,
        Vector2 pointerPosition,
        out UIElement? target)
    {
        target = null;
        if (!TryFindWheelTextInputAncestor(candidate, out var textInputHost) || textInputHost == null)
        {
            return false;
        }

        _lastInputHitTestCount++;
        _clickCpuResolveHitTestCount++;
        var hit = VisualTreeHelper.HitTest(textInputHost, pointerPosition);
        if (hit == null || ReferenceEquals(hit, textInputHost))
        {
            return false;
        }

        for (var current = hit; current != null && !ReferenceEquals(current, textInputHost); current = current.VisualParent ?? current.LogicalParent)
        {
            if (IsKnownClickCapableElement(current))
            {
                target = current;
                return true;
            }
        }

        return false;
    }

    private bool TryResolveHoverTargetWithinTextInputSubtree(
        UIElement candidate,
        Vector2 pointerPosition,
        out UIElement? target)
    {
        target = null;
        if (!TryFindWheelTextInputAncestor(candidate, out var textInputHost) || textInputHost == null)
        {
            return false;
        }

        _lastInputHitTestCount++;
        var hit = VisualTreeHelper.HitTest(textInputHost, pointerPosition);
        if (hit == null || ReferenceEquals(hit, textInputHost))
        {
            return false;
        }

        for (var current = hit; current != null && !ReferenceEquals(current, textInputHost); current = current.VisualParent ?? current.LogicalParent)
        {
            if (IsHoverHostElement(current))
            {
                target = current;
                return true;
            }
        }

        return false;
    }

    private bool TryResolveClickTargetFromCandidate(UIElement? candidate, Vector2 pointerPosition, out UIElement? target)
    {
        candidate = ResolveSubspaceViewport2DClickCandidate(candidate, pointerPosition);
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

        if (TryResolveClickTargetWithinTextInputSubtree(candidate, pointerPosition, out var descendantTarget) &&
            descendantTarget != null)
        {
            target = descendantTarget;
            return true;
        }

        foreach (var current in GetInputAncestorChain(candidate))
        {
            if (current is ScrollViewer && !ReferenceEquals(current, candidate))
            {
                continue;
            }

            if (IsKnownClickCapableElement(current))
            {
                target = current;
                return true;
            }
        }

        return false;
    }

    private UIElement? ResolveSubspaceViewport2DClickCandidate(UIElement? candidate, Vector2 pointerPosition)
    {
        if (candidate == null)
        {
            return null;
        }

        if (candidate is RenderSurface renderSurface &&
            renderSurface.TryHitTestSubspaceViewport2Ds(pointerPosition, out var directSubspaceViewport2DTarget) &&
            directSubspaceViewport2DTarget != null)
        {
            return directSubspaceViewport2DTarget;
        }

        if (TryFindAncestor<RenderSurface>(candidate, out var ancestorRenderSurface) &&
            ancestorRenderSurface != null &&
            ancestorRenderSurface.TryHitTestSubspaceViewport2Ds(pointerPosition, out var subspaceViewport2DTarget) &&
            subspaceViewport2DTarget != null)
        {
            return subspaceViewport2DTarget;
        }

        return candidate;
    }

    private bool HasMultipleTopLevelVisualChildren()
    {
        EnsureVisualIndexCurrent();
        return _visualIndex.TopLevelVisuals.Count > 1;
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
            MenuItem or ComboBoxItem)
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
        return element is Button or ITextInputControl or Thumb or ScrollViewer or ScrollBar or
            ListBoxItem or ListViewItem or DataGridRow or MenuItem or ComboBoxItem or TabItem or TreeViewItem;
    }

    private bool IsElementConnectedToVisualRoot(UIElement element)
    {
        if (_inputConnectionCache.TryGetValue(element, out var cachedState))
        {
            return cachedState.IsConnected;
        }

        var isConnected = IsElementConnectedToVisualRootCore(element);
        _inputConnectionCache[element] = new CachedInputConnectionState(isConnected);
        return isConnected;
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
            TryResolveItemsHostContainerTargetFromAnchor(cachedAnchor, pointerPosition, out target, out cachedDetail))
        {
            detail = $"anchor=cached; {cachedDetail}";
            return true;
        }
        attemptedCached = cachedAnchor != null;
        cachedDetail = attemptedCached ? cachedDetail : "skipped(anchor=null)";

        if (hoveredAnchor != null &&
            IsElementConnectedToVisualRoot(hoveredAnchor) &&
            !ReferenceEquals(cachedAnchor, hoveredAnchor) &&
            TryResolveItemsHostContainerTargetFromAnchor(hoveredAnchor, pointerPosition, out target, out hoveredDetail))
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

    private bool TryResolveItemsHostContainerTargetFromAnchor(
        UIElement? anchor,
        Vector2 pointerPosition,
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

        if (!IsElementConnectedToVisualRoot(anchor))
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

        if (!IsElementConnectedToVisualRoot(container))
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

    private static bool TryGetHoverHostAnchor(UIElement? anchor, out UIElement? hostAnchor)
    {
        hostAnchor = null;
        if (anchor == null)
        {
            return false;
        }

        if (anchor is Button or CalendarDayButton)
        {
            for (var current = anchor.VisualParent ?? anchor.LogicalParent; current != null; current = current.VisualParent ?? current.LogicalParent)
            {
                if (current is ScrollViewer)
                {
                    continue;
                }

                if (current is UniformGrid or StackPanel or ItemsPresenter)
                {
                    hostAnchor = current;
                    return true;
                }

                if (current is Panel panel)
                {
                    hostAnchor = panel;
                    return true;
                }
            }
        }

        for (var current = anchor.VisualParent ?? anchor.LogicalParent; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Expander expander)
            {
                hostAnchor = expander;
                return true;
            }
        }

        if (!IsHoverHostElement(anchor))
        {
            return false;
        }

        return TryGetClickHostAnchor(anchor, out hostAnchor);
    }

    private static bool IsClickCapableElement(UIElement element)
    {
        if (element is Button or ITextInputControl or Thumb or ScrollViewer or ScrollBar or
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
        var dispatchStart = Stopwatch.GetTimestamp();
        var previousScope = _activeKeyboardMenuScope;
        var hadActiveScope = _hasActiveKeyboardMenuScope;
        try
        {
            _activeKeyboardMenuScope = BuildKeyboardMenuScope();
            _hasActiveKeyboardMenuScope = true;
            _lastInputKeyEventCount++;
            _lastInputRoutedEventCount += 2; var previewArgs = new KeyRoutedEventArgs(UIElement.PreviewKeyDownEvent, key, modifiers);
            target.RaiseRoutedEventInternal(UIElement.PreviewKeyDownEvent, previewArgs);
            if (previewArgs.Handled) return;

            var keyArgs = new KeyRoutedEventArgs(UIElement.KeyDownEvent, key, modifiers);
            target.RaiseRoutedEventInternal(UIElement.KeyDownEvent, keyArgs);
            if (keyArgs.Handled) return;

            if (key == Keys.F10 && modifiers == ModifierKeys.None)
            {
                if (TryResolveScopedMenuForKeyboard(out var menu) &&
                    menu.TryActivateMenuBarFromKeyboard(_inputState.FocusedElement))
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

                if (AccessKeyService.TryExecute(accessKey, _visualRoot, _inputState.FocusedElement))
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

            if (_inputState.FocusedElement is DataGrid focusedDataGrid &&
                focusedDataGrid.HandleKeyDownFromInput(key, modifiers))
            {
                return;
            }

            if (_inputState.FocusedElement is ITextInputControl focusedTextInput &&
                focusedTextInput.HandleKeyDownFromInput(key, modifiers))
            {
                return;
            }

            if (TryFindFocusedListBox(out var focusedListBox) &&
                focusedListBox.HandleKeyDownFromInput(key, modifiers))
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
            _activeKeyboardMenuScope = previousScope;
            _hasActiveKeyboardMenuScope = hadActiveScope;
        }
    }

    private void DispatchKeyUp(Keys key, ModifierKeys modifiers)
    {
        var target = _inputState.FocusedElement ?? _visualRoot;
        var dispatchStart = Stopwatch.GetTimestamp(); _lastInputKeyEventCount++;
        _lastInputRoutedEventCount += 2; target.RaiseRoutedEventInternal(UIElement.PreviewKeyUpEvent, new KeyRoutedEventArgs(UIElement.PreviewKeyUpEvent, key, modifiers)); target.RaiseRoutedEventInternal(UIElement.KeyUpEvent, new KeyRoutedEventArgs(UIElement.KeyUpEvent, key, modifiers));
    }

    private bool TryFindFocusedListBox(out ListBox listBox)
    {
        for (var current = _inputState.FocusedElement; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is ListBox focusedListBox)
            {
                listBox = focusedListBox;
                return true;
            }
        }

        listBox = null!;
        return false;
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

        if (focused is DataGrid dataGrid)
        {
            _ = dataGrid.HandleTextInputFromInput(character);
        }
        else if (focused is ITextInputControl textInput)
        {
            _ = textInput.HandleTextInputFromInput(character);
        }
    }

    private OverlayDismissResult TryDismissOverlayOnOutsidePointerDown(Vector2 pointerPosition, UIElement? pointerTarget)
    {
        if (!TryGetTopOverlayCandidate(out var candidate) ||
            !candidate.CloseOnOutsidePointerDown)
        {
            return OverlayDismissResult.None;
        }

        if ((pointerTarget != null && IsElementDescendantOf(pointerTarget, candidate.Element)) ||
            candidate.Element.HitTest(pointerPosition))
        {
            return OverlayDismissResult.None;
        }

        return CloseOverlay(candidate.Element, consumePointerClick: true);
    }

    private bool TryDismissTopOverlayOnEscape()
    {
        if (!TryGetTopOverlayCandidate(out var candidate) ||
            !candidate.CloseOnEscape)
        {
            return false;
        }

        return CloseOverlay(candidate.Element, consumePointerClick: false).Dismissed;
    }

    private bool TryGetTopOverlayCandidate(out OverlayCandidate candidate)
    {
        if (_cachedTopOverlayCandidateHasValue &&
            _cachedTopOverlayCandidateVersion == _overlayCandidateVersion)
        {
            candidate = _cachedTopOverlayCandidate;
            return _hasCachedTopOverlayCandidate;
        }

        var hasCandidate = false;
        var bestDepth = int.MinValue;
        var bestZIndex = int.MinValue;
        var bestOrder = int.MinValue;
        var currentCandidate = default(OverlayCandidate);
        EnsureVisualIndexCurrent();
        for (var i = _openOverlayVisuals.Count - 1; i >= 0; i--)
        {
            var overlay = _openOverlayVisuals[i];
            if (!IsOverlayCurrentlyOpen(overlay) || !IsElementConnectedToVisualRoot(overlay))
            {
                UnregisterOpenOverlay(overlay);
                continue;
            }

            _lastOverlayRegistryScanCount++;
            if (!_visualIndex.TryGetNode(overlay, out var node))
            {
                continue;
            }

            var zIndex = Panel.GetZIndex(overlay);
            var isOverlay = false;
            var closeOnOutsidePointerDown = false;
            var closeOnEscape = false;
            switch (overlay)
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

            if (!isOverlay)
            {
                continue;
            }

            var overlayCandidate = new OverlayCandidate(overlay, closeOnOutsidePointerDown, closeOnEscape);
            if (IsBetterOverlayCandidate(
                    node.Depth,
                    zIndex,
                    node.PreorderIndex,
                    hasCandidate,
                    bestDepth,
                    bestZIndex,
                    bestOrder))
            {
                hasCandidate = true;
                bestDepth = node.Depth;
                bestZIndex = zIndex;
                bestOrder = node.PreorderIndex;
                currentCandidate = overlayCandidate;
            }
        }

        if (hasCandidate)
        {
            _lastOverlayRegistryHitCount++;
        }

        _cachedTopOverlayCandidateVersion = _overlayCandidateVersion;
        _cachedTopOverlayCandidate = currentCandidate;
        _cachedTopOverlayCandidateHasValue = true;
        _hasCachedTopOverlayCandidate = hasCandidate;
        candidate = currentCandidate;
        return hasCandidate;
    }

    private void InvalidateOverlayCandidateCache()
    {
        _overlayCandidateVersion++;
        _cachedTopOverlayCandidateHasValue = false;
        _hasCachedTopOverlayCandidate = false;
        _cachedTopOverlayCandidate = default;
        _cachedTopOverlayCandidateVersion = -1;
        _cachedTopContextMenu = null;
        _cachedTopContextMenuVersion = -1;
        _cachedTopContextMenuHasValue = false;
        _hasCachedTopContextMenu = false;
    }

    internal void NotifyOverlayVisualTreeMutation()
    {
        InvalidateOverlayInteractionCaches(clearOverlayOwnedHover: true);
    }

    private void InvalidateOverlayInteractionCaches(bool clearOverlayOwnedHover = false)
    {
        InvalidateOverlayCandidateCache();
        RefreshPointerTargetsAfterLayoutMutation();
        if (clearOverlayOwnedHover)
        {
            ClearHoverStateIfOverlayOwned();
        }
    }

    private void ClearHoverStateIfOverlayOwned()
    {
        var hovered = _inputState.HoveredElement;
        if (!IsOverlayOwnedElement(hovered))
        {
            return;
        }

        UpdateHover(null);
    }

    private static bool IsOverlayOwnedElement(UIElement? element)
    {
        for (var current = element; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Popup or ContextMenu)
            {
                return true;
            }
        }

        return false;
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

    private OverlayDismissResult CloseOverlay(UIElement overlay, bool consumePointerClick)
    {
        switch (overlay)
        {
            case Popup popup:
                popup.Close();
                TrySynchronizePopupFocusRestore(popup);
                break;
            case ContextMenu contextMenu:
                contextMenu.Close();
                TrySynchronizeContextMenuFocusRestore(contextMenu);
                break;
            default:
                return OverlayDismissResult.None;
        }

        InvalidateOverlayInteractionCaches(clearOverlayOwnedHover: true);

        return new OverlayDismissResult(Dismissed: true, Consumed: consumePointerClick);
    }

    private static bool IsOverlayCurrentlyOpen(UIElement overlay)
    {
        return overlay switch
        {
            Popup popup => popup.IsOpen,
            ContextMenu contextMenu => contextMenu.IsOpen,
            _ => false
        };
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

    private readonly record struct OverlayDismissResult(
        bool Dismissed,
        bool Consumed)
    {
        internal static OverlayDismissResult None => new(Dismissed: false, Consumed: false);
    }

    private void SetFocus(UIElement? element)
    {
        if (ReferenceEquals(_inputState.FocusedElement, element))
        {
            return;
        }

        var old = _inputState.FocusedElement;
        _inputState.FocusedElement = element;
        InvalidateKeyboardMenuScopeCache();
        InvalidateActiveUpdateParticipants();
        FocusManager.SetFocus(element);
        Automation.NotifyFocusChanged(old, element);
        
        if (old is DataGrid oldDataGrid)
        {
            oldDataGrid.SetFocusedFromInput(false);
            old.RaiseRoutedEventInternal(UIElement.LostFocusEvent, new FocusChangedRoutedEventArgs(UIElement.LostFocusEvent, old, element));
            _lastInputRoutedEventCount++;
        }
        else if (old is ITextInputControl oldTextInput)
        {
            oldTextInput.SetFocusedFromInput(false);
            old.RaiseRoutedEventInternal(UIElement.LostFocusEvent, new FocusChangedRoutedEventArgs(UIElement.LostFocusEvent, old, element));
            _lastInputRoutedEventCount++;
        }

        if (element is DataGrid newDataGrid)
        {
            newDataGrid.SetFocusedFromInput(true);
            element.RaiseRoutedEventInternal(UIElement.GotFocusEvent, new FocusChangedRoutedEventArgs(UIElement.GotFocusEvent, old, element));
            _lastInputRoutedEventCount++;
        }
        else if (element is ITextInputControl newTextInput)
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

        if (CanvasThumbInvestigationLog.ShouldTrace(
                _visualRoot,
                _inputState.LastPointerPosition,
                element,
                _inputState.HoveredElement,
                _inputState.CapturedPointerElement))
        {
            CanvasThumbInvestigationLog.Write(
                "Capture",
                $"pointer={CanvasThumbInvestigationLog.DescribePointer(_inputState.LastPointerPosition)} captured={CanvasThumbInvestigationLog.DescribeElement(element)} hovered={CanvasThumbInvestigationLog.DescribeElement(_inputState.HoveredElement)}");
        }
    }

    private void ReleasePointer(UIElement? element)
    {
        if (!ReferenceEquals(_inputState.CapturedPointerElement, element))
        {
            return;
        }

        _inputState.CapturedPointerElement = null;
        FocusManager.ReleasePointer(element);

    if (CanvasThumbInvestigationLog.ShouldTrace(
        _visualRoot,
        _inputState.LastPointerPosition,
        element,
        _inputState.HoveredElement))
    {
        CanvasThumbInvestigationLog.Write(
        "Release",
        $"pointer={CanvasThumbInvestigationLog.DescribePointer(_inputState.LastPointerPosition)} released={CanvasThumbInvestigationLog.DescribeElement(element)} hovered={CanvasThumbInvestigationLog.DescribeElement(_inputState.HoveredElement)}");
    }
    }

    private bool TryHandleMenuAccessKey(char accessKey)
    {
        var menus = EnumerateMenusForKeyboardScope();
        for (var i = 0; i < menus.Count; i++)
        {
            var menu = menus[i];
            if (!menu.TryHandleAccessKeyFromInput(accessKey, _inputState.FocusedElement))
            {
                continue;
            }

            TrySynchronizeMenuFocusRestore(menu);
            return true;
        }

        return false;
    }

    private bool TryFindMenuInMenuMode(out Menu menu)
    {
        var menus = EnumerateMenusForKeyboardScope();
        for (var i = 0; i < menus.Count; i++)
        {
            var candidate = menus[i];
            if (!candidate.IsMenuMode)
            {
                continue;
            }

            menu = candidate;
            return true;
        }

        menu = null!;
        return false;
    }

    private bool TryResolveScopedMenuForKeyboard(out Menu menu)
    {
        var menus = EnumerateMenusForKeyboardScope();
        if (menus.Count > 0)
        {
            menu = menus[0];
            return true;
        }

        menu = null!;
        return false;
    }

    private IReadOnlyList<Menu> EnumerateMenusForKeyboardScope()
    {
        if (_hasActiveKeyboardMenuScope)
        {
            return _activeKeyboardMenuScope.Menus;
        }

        return BuildKeyboardMenuScope().Menus;
    }

    private KeyboardMenuScope BuildKeyboardMenuScope()
    {
        EnsureVisualIndexCurrent();
        if (_hasCachedKeyboardMenuScope &&
            _cachedKeyboardMenuScopeVisualIndexVersion == _visualIndex.Version &&
            ReferenceEquals(_cachedKeyboardMenuScopeFocusedElement, _inputState.FocusedElement))
        {
            return _cachedKeyboardMenuScope;
        }

        _lastMenuScopeBuildCount++;
        var allMenus = _visualIndex.Menus;
        if (allMenus.Count == 0)
        {
            var emptyScope = new KeyboardMenuScope(Array.Empty<Menu>());
            _cachedKeyboardMenuScope = emptyScope;
            _hasCachedKeyboardMenuScope = true;
            _cachedKeyboardMenuScopeVisualIndexVersion = _visualIndex.Version;
            _cachedKeyboardMenuScopeFocusedElement = _inputState.FocusedElement;
            return emptyScope;
        }

        var prioritized = new List<Menu>(allMenus.Count);
        var seenMenus = _keyboardMenuScopeDedupSet;
        seenMenus.Clear();
        if (TryFindAnyMenuInMenuMode(allMenus, out var activeMenuMode))
        {
            AddUniqueMenu(prioritized, seenMenus, activeMenuMode);
        }

        if (TryFindAncestorMenuOfFocusedElement(out var focusedAncestorMenu))
        {
            AddUniqueMenu(prioritized, seenMenus, focusedAncestorMenu);
        }

        if (TryFindHighestZVisibleMenu(allMenus, out var highestZMenu))
        {
            AddUniqueMenu(prioritized, seenMenus, highestZMenu);
        }

        for (var i = 0; i < allMenus.Count; i++)
        {
            AddUniqueMenu(prioritized, seenMenus, allMenus[i]);
        }

        var scope = new KeyboardMenuScope(prioritized);
        seenMenus.Clear();
        _cachedKeyboardMenuScope = scope;
        _hasCachedKeyboardMenuScope = true;
        _cachedKeyboardMenuScopeVisualIndexVersion = _visualIndex.Version;
        _cachedKeyboardMenuScopeFocusedElement = _inputState.FocusedElement;
        return scope;
    }

    private bool TryFindAnyMenuInMenuMode(IReadOnlyList<Menu> menus, out Menu menu)
    {
        menu = null!;
        var found = false;
        var bestZIndex = int.MinValue;
        for (var i = 0; i < menus.Count; i++)
        {
            var candidate = menus[i];
            if (!candidate.IsMenuMode)
            {
                continue;
            }

            var candidateZ = Panel.GetZIndex(candidate);
            if (!found || candidateZ > bestZIndex)
            {
                menu = candidate;
                bestZIndex = candidateZ;
                found = true;
            }
        }

        return found;
    }

    private bool TryFindAncestorMenuOfFocusedElement(out Menu menu)
    {
        for (var current = _inputState.FocusedElement; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (current is Menu focusedMenu)
            {
                menu = focusedMenu;
                return true;
            }
        }

        menu = null!;
        return false;
    }

    private static bool TryFindHighestZVisibleMenu(IReadOnlyList<Menu> menus, out Menu menu)
    {
        menu = null!;
        var found = false;
        var bestZIndex = int.MinValue;
        for (var i = 0; i < menus.Count; i++)
        {
            var candidate = menus[i];
            if (!candidate.IsVisible)
            {
                continue;
            }

            var candidateZIndex = Panel.GetZIndex(candidate);
            if (!found || candidateZIndex > bestZIndex)
            {
                menu = candidate;
                bestZIndex = candidateZIndex;
                found = true;
            }
        }

        return found;
    }

    private static void AddUniqueMenu(List<Menu> menus, HashSet<Menu> seenMenus, Menu menu)
    {
        if (seenMenus.Add(menu))
        {
            menus.Add(menu);
        }
    }

    private void TrySynchronizeMenuFocusRestore(Menu menu)
    {
        _ = menu.TryConsumePendingFocusRestore(out var focusTarget);
        TryRestoreFocusIfConnected(focusTarget);
    }

    private void TrySynchronizeContextMenuFocusRestore(ContextMenu contextMenu)
    {
        _ = contextMenu.TryConsumePendingFocusRestore(out var focusTarget);
        TryRestoreFocusIfConnected(focusTarget);
    }

    private void TrySynchronizePopupFocusRestore(Popup popup)
    {
        _ = popup.TryConsumePendingFocusRestore(out var focusTarget);
        TryRestoreFocusIfConnected(focusTarget);
    }

    private void TryRestoreFocusIfConnected(UIElement? focusTarget)
    {
        if (focusTarget != null &&
            IsElementDescendantOf(focusTarget, _visualRoot))
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
        InvalidateOverlayInteractionCaches();
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
        InvalidateOverlayInteractionCaches();
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
        var cachedMenuStart = Stopwatch.GetTimestamp();
        if (_lastKnownOpenContextMenu is { IsOpen: true } knownOpenContextMenu)
        {
            _lastInputPointerResolveContextMenuCachedMenuMs += Stopwatch.GetElapsedTime(cachedMenuStart).TotalMilliseconds;
            contextMenu = knownOpenContextMenu;
            return true;
        }

        _lastInputPointerResolveContextMenuCachedMenuMs += Stopwatch.GetElapsedTime(cachedMenuStart).TotalMilliseconds;
        if (_lastKnownOpenContextMenu is not null)
        {
            _lastKnownOpenContextMenu = null;
        }

        var overlayCandidateStart = Stopwatch.GetTimestamp();
        if (_cachedTopContextMenuHasValue &&
            _cachedTopContextMenuVersion == _overlayCandidateVersion)
        {
            _lastInputPointerResolveContextMenuOverlayCandidateMs += Stopwatch.GetElapsedTime(overlayCandidateStart).TotalMilliseconds;
            if (_hasCachedTopContextMenu && _cachedTopContextMenu != null)
            {
                contextMenu = _cachedTopContextMenu;
                _lastKnownOpenContextMenu = contextMenu;
                return true;
            }

            contextMenu = null!;
            return false;
        }

        EnsureVisualIndexCurrent();
        var found = false;
        var bestDepth = int.MinValue;
        var bestZIndex = int.MinValue;
        var bestOrder = int.MinValue;
        ContextMenu? bestMenu = null;
        for (var i = _openContextMenus.Count - 1; i >= 0; i--)
        {
            var candidateMenu = _openContextMenus[i];
            if (!candidateMenu.IsOpen || !IsElementConnectedToVisualRoot(candidateMenu))
            {
                UnregisterOpenOverlay(candidateMenu);
                continue;
            }

            _lastOverlayRegistryScanCount++;
            if (!_visualIndex.TryGetNode(candidateMenu, out var node))
            {
                continue;
            }

            var zIndex = Panel.GetZIndex(candidateMenu);
            if (!found || IsBetterOverlayCandidate(node.Depth, zIndex, node.PreorderIndex, found, bestDepth, bestZIndex, bestOrder))
            {
                found = true;
                bestDepth = node.Depth;
                bestZIndex = zIndex;
                bestOrder = node.PreorderIndex;
                bestMenu = candidateMenu;
            }
        }

        if (bestMenu != null)
        {
            _lastOverlayRegistryHitCount++;
            _lastInputPointerResolveContextMenuOverlayCandidateMs += Stopwatch.GetElapsedTime(overlayCandidateStart).TotalMilliseconds;
            _lastKnownOpenContextMenu = bestMenu;
            _cachedTopContextMenu = bestMenu;
            _cachedTopContextMenuVersion = _overlayCandidateVersion;
            _cachedTopContextMenuHasValue = true;
            _hasCachedTopContextMenu = true;
            contextMenu = bestMenu;
            return true;
        }

        _lastInputPointerResolveContextMenuOverlayCandidateMs += Stopwatch.GetElapsedTime(overlayCandidateStart).TotalMilliseconds;
        _cachedTopContextMenu = null;
        _cachedTopContextMenuVersion = _overlayCandidateVersion;
        _cachedTopContextMenuHasValue = true;
        _hasCachedTopContextMenu = false;
        contextMenu = null!;
        return false;
    }

    private void CloseAllOpenContextMenus()
    {
        for (var i = _openContextMenus.Count - 1; i >= 0; i--)
        {
            if (_openContextMenus[i].IsOpen)
            {
                _openContextMenus[i].Close();
            }
        }

        _lastKnownOpenContextMenu = null;
        InvalidateOverlayInteractionCaches(clearOverlayOwnedHover: true);
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

        private bool TryHandleTextInputWheel(UIElement? element, int delta)
    {
            if (element is not ITextInputControl textInput)
        {
                return false;
            }

            if (!ReferenceEquals(_inputState.FocusedElement, element))
            {
                SetFocus(element);
            }

            return textInput.HandleMouseWheelFromInput(delta);
    }

    private ReadOnlySpan<UIElement> GetInputAncestorChain(UIElement start)
    {
        if (_inputAncestorCache.TryGetValue(start, out var cachedChain))
        {
            return cachedChain.Chain;
        }

        _inputAncestorBuilder.Clear();
        for (var current = start; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            _inputAncestorBuilder.Add(current);
        }

        var chain = _inputAncestorBuilder.ToArray();
        _inputAncestorCache[start] = new CachedInputAncestorChain(chain);
        return chain;
    }

    private bool TryFindWheelAncestors(UIElement start, out UIElement? textInput, out ScrollViewer? scrollViewer)
    {
        textInput = null;
        scrollViewer = null;
        foreach (var current in GetInputAncestorChain(start))
        {
            if (current is ITextInputControl)
            {
                textInput = current;
                return true;
            }

            scrollViewer ??= current as ScrollViewer;
        }

        return scrollViewer != null;
    }

    private bool TryFindWheelTextInputAncestor(UIElement start, out UIElement? textInput)
    {
        foreach (var current in GetInputAncestorChain(start))
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

    private bool TryFindAncestor<TElement>(UIElement start, out TElement? result)
        where TElement : UIElement
    {
        foreach (var current in GetInputAncestorChain(start))
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

    private static void CollectVisualsOfType<TElement>(UIElement root, List<TElement> results)
        where TElement : UIElement
    {
        if (root is TElement typed)
        {
            results.Add(typed);
        }

        foreach (var child in root.GetVisualChildren())
        {
            CollectVisualsOfType(child, results);
        }
    }

    private readonly record struct KeyboardMenuScope(IReadOnlyList<Menu> Menus);

}
