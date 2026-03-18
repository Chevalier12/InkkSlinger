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

    internal void EnsureRenderInvalidationTracked(UIElement visual)
    {
        if (_dirtyRenderSet.Contains(visual))
        {
            TrackQueuedRenderMutation(visual);
            return;
        }

        NotifyInvalidation(UiInvalidationType.Render, visual);
    }

    internal void NotifyInvalidation(UiInvalidationType invalidationType, UIElement? source = null)
    {
        var effectiveSource = source;
        if (source != null &&
            !TryResolveInvalidationSource(source, invalidationType == UiInvalidationType.Render, out effectiveSource))
        {
            return;
        }

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
                BumpInputCacheVersion();
                if (RenderInvalidationAffectsPointerTargets(source, effectiveSource))
                {
                    _pointerResolveStateVersion++;
                }
                if (source is TextBox textBox && textBox.IsFocused)
                {
                    _hasCaretBlinkInvalidation = true;
                }

                if (UseDirtyRegionRendering)
                {
                    TrackDirtyBoundsForVisual(effectiveSource);
                }
                if (effectiveSource != null)
                {
                    EnqueueDirtyRenderNode(effectiveSource);
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

    private void TrackQueuedRenderMutation(UIElement source)
    {
        if (!TryResolveInvalidationSource(source, allowRetainedAncestorFallback: true, out var effectiveSource) ||
            effectiveSource == null)
        {
            return;
        }

        _hasRenderInvalidation = true;
        _mustDrawNextFrame = true;
        _renderStateVersion++;
        BumpInputCacheVersion();
        if (RenderInvalidationAffectsPointerTargets(source, effectiveSource))
        {
            _pointerResolveStateVersion++;
        }

        if (source is TextBox textBox && textBox.IsFocused)
        {
            _hasCaretBlinkInvalidation = true;
        }

        if (UseDirtyRegionRendering)
        {
            TrackDirtyBoundsForVisual(effectiveSource);
        }

        if (source is IUiRootUpdateParticipant || effectiveSource is IUiRootUpdateParticipant)
        {
            InvalidateActiveUpdateParticipants();
        }
    }

    private bool TryResolveInvalidationSource(UIElement source, bool allowRetainedAncestorFallback, out UIElement? effectiveSource)
    {
        effectiveSource = null;
        var connectedToRoot = false;
        EnsureVisualIndexCurrent();

        for (var current = source; current != null; current = current.GetInvalidationParent())
        {
            if (ReferenceEquals(current, _visualRoot))
            {
                connectedToRoot = true;
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

        if (!connectedToRoot)
        {
            return false;
        }

        effectiveSource = allowRetainedAncestorFallback ? _visualRoot : null;
        return allowRetainedAncestorFallback;
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
        EnsureVisualIndexCurrent();
        if (!TryGetIndexedVisualNodeCore(element, out _) &&
            !TryGetIndexedVisualNodeCore(oldParent, out _) &&
            !TryGetIndexedVisualNodeCore(newParent, out _) &&
            !ReferenceEquals(element, _visualRoot))
        {
            return;
        }

        _visualStructureChangeCount++;
        _visualStructureVersion++;
        MarkVisualIndexDirty();
        InvalidateOverlayCandidateCache();
        BumpPointerResolveStateVersion();
        _renderListNeedsFullRebuild = true;
        _mustDrawNextFrame = true;
        _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
        EnqueueDirtyRenderNode(element);
        Automation.NotifyVisualStructureChanged(element, oldParent, newParent);
    }

}
