using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    public bool ShouldDrawThisFrame(GameTime gameTime, Viewport viewport, GraphicsDevice? graphicsDevice = null)
    {
        _ = gameTime;
        var reasons = UiRedrawReason.None;

        if (!_hasLastScheduledViewport ||
            _lastScheduledViewport.Width != viewport.Width ||
            _lastScheduledViewport.Height != viewport.Height ||
            _lastScheduledViewport.X != viewport.X ||
            _lastScheduledViewport.Y != viewport.Y)
        {
            reasons |= UiRedrawReason.Resize;
            _lastScheduledViewport = viewport;
            _hasLastScheduledViewport = true;
        }

        if (!ReferenceEquals(_lastGraphicsDevice, graphicsDevice))
        {
            if (_lastGraphicsDevice != null)
            {
                UiDrawing.ReleaseDeviceResources(_lastGraphicsDevice);
            }

            reasons |= UiRedrawReason.Resize;
            _lastGraphicsDevice = graphicsDevice;
        }

        if (_mustDrawNextFrame)
        {
            reasons |= UiRedrawReason.LayoutInvalidated;
        }

        if (_hasMeasureInvalidation || _hasArrangeInvalidation)
        {
            reasons |= UiRedrawReason.LayoutInvalidated;
        }

        if (_hasRenderInvalidation || _retainedRender.HasDirtyWork)
        {
            reasons |= UiRedrawReason.RenderInvalidated;
        }

        if (AnimationManager.Current.HasRunningAnimations || _forceAnimationActiveForTests)
        {
            reasons |= UiRedrawReason.AnimationActive;
        }

        if (_hasCaretBlinkInvalidation)
        {
            reasons |= UiRedrawReason.CaretBlinkActive;
        }

        if (AlwaysDrawCompatibilityMode)
        {
            reasons |= UiRedrawReason.RenderInvalidated;
        }

        if (!UseConditionalDrawScheduling)
        {
            reasons |= UiRedrawReason.RenderInvalidated;
        }

        var shouldDraw = !UseConditionalDrawScheduling || AlwaysDrawCompatibilityMode || reasons != UiRedrawReason.None;
        LastShouldDrawReasons = reasons;
        _scheduledDrawReasons = shouldDraw ? reasons : UiRedrawReason.None;
        if (!shouldDraw)
        {
            DrawSkippedFrameCount++;
        }

        return shouldDraw;
    }

    public void Shutdown()
    {
        Automation.Shutdown();
        UnregisterRoot(this);
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }

    internal void EnsureRenderInvalidationTracked(UIElement visual, bool requireDeepSync = false)
    {
        if (_retainedRender.IsDirtyRenderQueued(visual))
        {
            TrackQueuedRenderMutation(visual, requireDeepSync);
            return;
        }

        NotifyInvalidation(UiInvalidationType.Render, visual, requireDeepSync);
    }

    internal void NotifyDirectRenderInvalidation(UIElement visual, bool requireDeepSync = false)
    {
        if (!_retainedRender.TryResolveInvalidationSource(visual, allowRetainedAncestorFallback: true, out var effectiveSource) ||
            effectiveSource == null)
        {
            NotifyInvalidation(UiInvalidationType.Render, visual, requireDeepSync);
            return;
        }

        RecordRenderInvalidationSources(visual, effectiveSource);

        _hasRenderInvalidation = true;
        _mustDrawNextFrame = true;
        RenderInvalidationCount++;
        _renderStateVersion++;
        if (RenderInvalidationAffectsPointerTargets(visual, effectiveSource))
        {
            _pointerResolveStateVersion++;
        }

        if (visual is TextBox textBox && textBox.IsFocused)
        {
            _hasCaretBlinkInvalidation = true;
        }

        _retainedRender.NotifyInvalidation(_retainedRender.CreateRenderStateInvalidation(visual, effectiveSource, requireDeepSync));

        if (visual is IUiRootUpdateParticipant || effectiveSource is IUiRootUpdateParticipant)
        {
            InvalidateActiveUpdateParticipants();
        }
    }

    internal void NotifyInvalidation(UiInvalidationType invalidationType, UIElement? source = null, bool requireDeepSync = false)
    {
        var effectiveSource = source;
        if (source != null &&
            !_retainedRender.TryResolveInvalidationSource(source, invalidationType == UiInvalidationType.Render, out effectiveSource))
        {
            RecordRenderInvalidationSources(source, effectiveSource: null);
            return;
        }

        RecordRenderInvalidationSources(source, effectiveSource);

        switch (invalidationType)
        {
            case UiInvalidationType.Measure:
                _hasMeasureInvalidation = true;
                _mustDrawNextFrame = true;
                MeasureInvalidationCount++;
                _renderStateVersion++;
                BumpPointerResolveStateVersion();
                break;
            case UiInvalidationType.Arrange:
                _hasArrangeInvalidation = true;
                _mustDrawNextFrame = true;
                ArrangeInvalidationCount++;
                _renderStateVersion++;
                BumpPointerResolveStateVersion();
                break;
            case UiInvalidationType.Render:
                _hasRenderInvalidation = true;
                _mustDrawNextFrame = true;
                RenderInvalidationCount++;
                _renderStateVersion++;
                if (RenderInvalidationAffectsPointerTargets(source, effectiveSource))
                {
                    _pointerResolveStateVersion++;
                }
                if (source is TextBox textBox && textBox.IsFocused)
                {
                    _hasCaretBlinkInvalidation = true;
                }

                _retainedRender.NotifyInvalidation(_retainedRender.CreateRenderStateInvalidation(source, effectiveSource, requireDeepSync));

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidationType), invalidationType, null);
        }

        if (source is IUiRootUpdateParticipant || effectiveSource is IUiRootUpdateParticipant)
        {
            InvalidateActiveUpdateParticipants();
        }
    }

    private void TrackQueuedRenderMutation(UIElement source, bool requireDeepSync = false)
    {
        if (!_retainedRender.TryResolveInvalidationSource(source, allowRetainedAncestorFallback: true, out var effectiveSource) ||
            effectiveSource == null)
        {
            return;
        }

        RecordRenderInvalidationSources(source, effectiveSource);

        _hasRenderInvalidation = true;
        _mustDrawNextFrame = true;
        _renderStateVersion++;
        if (RenderInvalidationAffectsPointerTargets(source, effectiveSource))
        {
            _pointerResolveStateVersion++;
        }

        if (source is TextBox textBox && textBox.IsFocused)
        {
            _hasCaretBlinkInvalidation = true;
        }

        _retainedRender.NotifyInvalidation(_retainedRender.CreateRenderStateInvalidation(source, effectiveSource, requireDeepSync));

        if (source is IUiRootUpdateParticipant || effectiveSource is IUiRootUpdateParticipant)
        {
            InvalidateActiveUpdateParticipants();
        }
    }

    private void RecordRenderInvalidationSources(UIElement? requestedSource, UIElement? effectiveSource)
    {
        _lastRenderInvalidationRequestedSourceElement = requestedSource;
        _lastRenderInvalidationRequestedSourceType = requestedSource?.GetType().Name ?? "none";
        _lastRenderInvalidationRequestedSourceName = requestedSource is FrameworkElement requestedFrameworkElement
            ? requestedFrameworkElement.Name
            : string.Empty;
        _lastRenderInvalidationEffectiveSourceElement = effectiveSource;
        _lastRenderInvalidationEffectiveSourceType = effectiveSource?.GetType().Name ?? "none";
        _lastRenderInvalidationEffectiveSourceName = effectiveSource is FrameworkElement effectiveFrameworkElement
            ? effectiveFrameworkElement.Name
            : string.Empty;
    }

    private void ResetRenderInvalidationResolutionDebugState()
    {
        _lastRenderInvalidationEffectiveSourceResolution = "none";
        _lastRenderInvalidationClipPromotionAncestorType = "none";
        _lastRenderInvalidationClipPromotionAncestorName = string.Empty;
        _lastRenderInvalidationRetainedSyncSourceElement = null;
        _lastRenderInvalidationRetainedSyncSourceType = "none";
        _lastRenderInvalidationRetainedSyncSourceName = string.Empty;
        _lastRenderInvalidationRetainedSyncSourceResolution = "none";
        _lastDirtyBoundsSourceResolution = "none";
    }

    private static bool RenderInvalidationAffectsPointerTargets(UIElement? source, UIElement? effectiveSource)
    {
        if (source == null || effectiveSource == null)
        {
            return true;
        }

        for (var current = source; current != null; current = current.GetInvalidationParent())
        {
            if (current.TryGetLocalRenderTransformSnapshot(out _) ||
                current.TryGetLocalClipSnapshot(out _))
            {
                return true;
            }

            if (ReferenceEquals(current, effectiveSource))
            {
                return false;
            }
        }

        return true;
    }

    internal void NotifyVisualStructureChanged(UIElement element, UIElement? oldParent, UIElement? newParent)
    {
        var owningRoot = ResolveVisualStructureNotificationRoot(element, oldParent, newParent);
        if (!ReferenceEquals(owningRoot, this))
        {
            owningRoot.NotifyVisualStructureChanged(element, oldParent, newParent);
            return;
        }

        if (!ReferenceEquals(element, _visualRoot) &&
            !IsElementConnectedToVisualRootCore(element) &&
            !IsElementConnectedToVisualRootCore(oldParent) &&
            !IsElementConnectedToVisualRootCore(newParent))
        {
            return;
        }

        _visualStructureChangeCount++;
        _visualStructureVersion++;
        MarkVisualIndexDirty();
        ClearDetachedInputState(element);
        InvalidateInputCachesForSubtree(element);
        InvalidateOverlayCandidateCache();
        BumpPointerResolveStateVersion();
        _renderListNeedsFullRebuild = true;
        _mustDrawNextFrame = true;
        MarkFullFrameDirty(UiFullDirtyReason.VisualStructureChanged);
        _retainedRender.NotifyInvalidation(new RetainedInvalidation(
            element,
            element,
            element,
            element,
            RetainedInvalidationKind.Structure,
            RequireDeepSync: true));
        Automation.NotifyVisualStructureChanged(element, oldParent, newParent);
    }

    internal void NotifyStableSubtreeVisualStructureChanged(UIElement element, UIElement? oldParent, UIElement? newParent)
    {
        var owningRoot = ResolveVisualStructureNotificationRoot(element, oldParent, newParent);
        if (!ReferenceEquals(owningRoot, this))
        {
            owningRoot.NotifyStableSubtreeVisualStructureChanged(element, oldParent, newParent);
            return;
        }

        if (!ReferenceEquals(element, _visualRoot) &&
            !IsElementConnectedToVisualRootCore(element) &&
            !IsElementConnectedToVisualRootCore(oldParent) &&
            !IsElementConnectedToVisualRootCore(newParent))
        {
            return;
        }

        _visualStructureChangeCount++;
        _visualStructureVersion++;
        MarkVisualIndexDirty();
        ClearDetachedInputState(element);
        InvalidateInputCachesForSubtree(element);
        InvalidateOverlayCandidateCache();
        BumpPointerResolveStateVersion();
        _mustDrawNextFrame = true;
        _retainedRender.NotifyInvalidation(new RetainedInvalidation(
            element,
            element,
            element,
            element,
            RetainedInvalidationKind.Structure,
            RequireDeepSync: true));
        Automation.NotifyVisualStructureChanged(element, oldParent, newParent);
    }

    private void ClearDetachedInputState(UIElement mutationRoot)
    {
        if (_inputState.CapturedPointerElement != null &&
            IsElementWithinSubtree(_inputState.CapturedPointerElement, mutationRoot) &&
            !IsElementConnectedToVisualRootCore(_inputState.CapturedPointerElement))
        {
            ReleasePointer(_inputState.CapturedPointerElement);
        }

        if (_inputState.FocusedElement != null &&
            IsElementWithinSubtree(_inputState.FocusedElement, mutationRoot) &&
            !IsElementConnectedToVisualRootCore(_inputState.FocusedElement))
        {
            SetFocus(null);
        }

        if (_inputState.HoveredElement != null &&
            IsElementWithinSubtree(_inputState.HoveredElement, mutationRoot) &&
            !IsElementConnectedToVisualRootCore(_inputState.HoveredElement))
        {
            UpdateHover(null);
        }
    }

    private static bool IsElementWithinSubtree(UIElement element, UIElement subtreeRoot)
    {
        if (ReferenceEquals(element, subtreeRoot))
        {
            return true;
        }

        for (var current = element.VisualParent ?? element.LogicalParent; current != null; current = current.VisualParent ?? current.LogicalParent)
        {
            if (ReferenceEquals(current, subtreeRoot))
            {
                return true;
            }
        }

        return false;
    }

}
