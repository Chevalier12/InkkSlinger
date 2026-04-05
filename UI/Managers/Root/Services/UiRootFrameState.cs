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

        if (_hasRenderInvalidation || _dirtyRegions.IsFullFrameDirty || _dirtyRegions.RegionCount > 0)
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
        if (shouldDraw)
        {
            DrawExecutedFrameCount++;
        }
        else
        {
            DrawSkippedFrameCount++;
        }
        return shouldDraw;
    }

    public void Shutdown()
    {
        Automation.Shutdown();
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }

    internal void EnsureRenderInvalidationTracked(UIElement visual, bool requireDeepSync = false)
    {
        if (_dirtyRenderSet.Contains(visual))
        {
            TrackQueuedRenderMutation(visual, requireDeepSync);
            return;
        }

        NotifyInvalidation(UiInvalidationType.Render, visual, requireDeepSync);
    }

    internal void NotifyDirectRenderInvalidation(UIElement visual, bool requireDeepSync = false)
    {
        if (!TryResolveInvalidationSource(visual, allowRetainedAncestorFallback: true, out var effectiveSource) ||
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

        var retainedSyncSource = ResolveRetainedSyncSource(visual, effectiveSource, requireDeepSync);

        if (UseDirtyRegionRendering)
        {
            TrackDirtyBoundsForVisual(ResolveDirtyBoundsSource(visual, effectiveSource));
        }

        if (retainedSyncSource != null)
        {
            EnqueueDirtyRenderNode(retainedSyncSource, requireDeepSync);
        }

        if (visual is IUiRootUpdateParticipant || effectiveSource is IUiRootUpdateParticipant)
        {
            InvalidateActiveUpdateParticipants();
        }
    }

    internal void NotifyInvalidation(UiInvalidationType invalidationType, UIElement? source = null, bool requireDeepSync = false)
    {
        var effectiveSource = source;
        if (source != null &&
            !TryResolveInvalidationSource(source, invalidationType == UiInvalidationType.Render, out effectiveSource))
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

                var retainedSyncSource = ResolveRetainedSyncSource(source, effectiveSource, requireDeepSync);

                if (UseDirtyRegionRendering)
                {
                    TrackDirtyBoundsForVisual(ResolveDirtyBoundsSource(source, effectiveSource));
                }

                if (retainedSyncSource != null)
                {
                    EnqueueDirtyRenderNode(retainedSyncSource, requireDeepSync);
                }

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
        if (!TryResolveInvalidationSource(source, allowRetainedAncestorFallback: true, out var effectiveSource) ||
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

        var retainedSyncSource = ResolveRetainedSyncSource(source, effectiveSource, requireDeepSync);

        if (UseDirtyRegionRendering)
        {
            TrackDirtyBoundsForVisual(ResolveDirtyBoundsSource(source, effectiveSource));
        }

        if (retainedSyncSource != null)
        {
            EnqueueDirtyRenderNode(retainedSyncSource, requireDeepSync);
        }

        if (source is IUiRootUpdateParticipant || effectiveSource is IUiRootUpdateParticipant)
        {
            InvalidateActiveUpdateParticipants();
        }
    }

    private void RecordRenderInvalidationSources(UIElement? requestedSource, UIElement? effectiveSource)
    {
        _lastRenderInvalidationRequestedSourceType = requestedSource?.GetType().Name ?? "none";
        _lastRenderInvalidationRequestedSourceName = requestedSource is FrameworkElement requestedFrameworkElement
            ? requestedFrameworkElement.Name
            : string.Empty;
        _lastRenderInvalidationEffectiveSourceType = effectiveSource?.GetType().Name ?? "none";
        _lastRenderInvalidationEffectiveSourceName = effectiveSource is FrameworkElement effectiveFrameworkElement
            ? effectiveFrameworkElement.Name
            : string.Empty;
    }

    private bool TryResolveInvalidationSource(UIElement source, bool allowRetainedAncestorFallback, out UIElement? effectiveSource)
    {
        effectiveSource = null;
        UIElement? connectedFallback = null;
        var clipPromotionAncestor = allowRetainedAncestorFallback
            ? FindEscapingRenderClipAncestor(source)
            : null;
        for (var current = source; current != null; current = current.GetInvalidationParent())
        {
            if (allowRetainedAncestorFallback)
            {
                if (TryGetIndexedVisualNodeCore(current, out _))
                {
                    if (clipPromotionAncestor != null && !ReferenceEquals(current, clipPromotionAncestor))
                    {
                        if (IsElementConnectedToVisualRootCore(current))
                        {
                            connectedFallback = current;
                        }

                        continue;
                    }

                    effectiveSource = current;
                    return true;
                }

                if (IsElementConnectedToVisualRootCore(current))
                {
                    connectedFallback = current;
                }

                continue;
            }

            if (!TryGetIndexedVisualNodeCore(current, out _))
            {
                continue;
            }

            if (!allowRetainedAncestorFallback && !ReferenceEquals(current, source))
            {
                return false;
            }

            effectiveSource = current;
            return true;
        }

        if (allowRetainedAncestorFallback && connectedFallback != null)
        {
            effectiveSource = connectedFallback;
            return true;
        }

        return false;
    }

    private UIElement? ResolveRetainedSyncSource(UIElement? requestedSource, UIElement? effectiveSource, bool requireDeepSync)
    {
        _ = requireDeepSync;
        if (requestedSource != null &&
            TryGetIndexedVisualNodeCore(requestedSource, out _) &&
            IsTransformScrollRetainedSyncCandidate(requestedSource))
        {
            return requestedSource;
        }

        if (ShouldAnchorEscapingRenderInvalidationToRequestedSource(requestedSource, effectiveSource))
        {
            return requestedSource;
        }

        return effectiveSource;
    }

    private UIElement? ResolveDirtyBoundsSource(UIElement? requestedSource, UIElement? effectiveSource)
    {
        if (requestedSource != null &&
            TryGetIndexedVisualNodeCore(requestedSource, out _) &&
            IsTransformScrollRetainedSyncCandidate(requestedSource))
        {
            return requestedSource;
        }

        if (ShouldAnchorEscapingRenderInvalidationToRequestedSource(requestedSource, effectiveSource))
        {
            return requestedSource;
        }

        return effectiveSource;
    }

    private bool ShouldAnchorEscapingRenderInvalidationToRequestedSource(UIElement? requestedSource, UIElement? effectiveSource)
    {
        if (requestedSource == null ||
            effectiveSource == null ||
            ReferenceEquals(requestedSource, effectiveSource) ||
            !TryGetIndexedVisualNodeCore(requestedSource, out _))
        {
            return false;
        }

        // Keep retained sync and dirty-bounds tracking anchored to the actual mutated subtree
        // when clip promotion only exists to widen coverage for transformed/effected descendants.
        return ReferenceEquals(FindEscapingRenderClipAncestor(requestedSource), effectiveSource);
    }

    private static bool IsTransformScrollRetainedSyncCandidate(UIElement element)
    {
        if (element is IScrollTransformContent)
        {
            return true;
        }

        if (element is VirtualizingStackPanel)
        {
            return false;
        }

        if (element is not Panel)
        {
            return false;
        }

        if (!ScrollViewer.GetUseTransformContentScrolling(element))
        {
            return false;
        }

        return (element.VisualParent is ScrollViewer visualOwner && ReferenceEquals(visualOwner.Content, element)) ||
               (element.LogicalParent is ScrollViewer logicalOwner && ReferenceEquals(logicalOwner.Content, element));
    }

    private static UIElement? FindEscapingRenderClipAncestor(UIElement source)
    {
        var foundEscapingRender = false;

        for (var current = source; current != null; current = current.GetInvalidationParent())
        {
            foundEscapingRender |= CanRenderOutsideOwnSlot(current);
            if (foundEscapingRender && current.TryGetLocalClipSnapshot(out _))
            {
                return current;
            }
        }

        return null;
    }

    private static bool CanRenderOutsideOwnSlot(UIElement element)
    {
        if (element.TryGetLocalRenderTransformSnapshot(out var transform) && !AreTransformsEffectivelyEqual(transform, Matrix.Identity))
        {
            return true;
        }

        if (element.Effect == null)
        {
            return false;
        }

        var slot = element.LayoutSlot;
        var renderBounds = element.Effect.GetRenderBounds(element);
        return renderBounds.X < slot.X - 0.01f ||
               renderBounds.Y < slot.Y - 0.01f ||
               renderBounds.X + renderBounds.Width > slot.X + slot.Width + 0.01f ||
               renderBounds.Y + renderBounds.Height > slot.Y + slot.Height + 0.01f;
    }

    private static bool AreTransformsEffectivelyEqual(Matrix left, Matrix right)
    {
        return MathF.Abs(left.M11 - right.M11) <= 0.0001f &&
               MathF.Abs(left.M12 - right.M12) <= 0.0001f &&
               MathF.Abs(left.M13 - right.M13) <= 0.0001f &&
               MathF.Abs(left.M14 - right.M14) <= 0.0001f &&
               MathF.Abs(left.M21 - right.M21) <= 0.0001f &&
               MathF.Abs(left.M22 - right.M22) <= 0.0001f &&
               MathF.Abs(left.M23 - right.M23) <= 0.0001f &&
               MathF.Abs(left.M24 - right.M24) <= 0.0001f &&
               MathF.Abs(left.M31 - right.M31) <= 0.0001f &&
               MathF.Abs(left.M32 - right.M32) <= 0.0001f &&
               MathF.Abs(left.M33 - right.M33) <= 0.0001f &&
               MathF.Abs(left.M34 - right.M34) <= 0.0001f &&
               MathF.Abs(left.M41 - right.M41) <= 0.0001f &&
               MathF.Abs(left.M42 - right.M42) <= 0.0001f &&
               MathF.Abs(left.M43 - right.M43) <= 0.0001f &&
               MathF.Abs(left.M44 - right.M44) <= 0.0001f;
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
        InvalidateInputCachesForSubtree(element);
        InvalidateOverlayCandidateCache();
        BumpPointerResolveStateVersion();
        _renderListNeedsFullRebuild = true;
        _mustDrawNextFrame = true;
        MarkFullFrameDirty(UiFullDirtyReason.VisualStructureChanged);
        EnqueueDirtyRenderNode(element);
        Automation.NotifyVisualStructureChanged(element, oldParent, newParent);
    }

}
