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
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }

        _cacheSpriteBatch?.Dispose();
        _cacheSpriteBatch = null;
        _renderCacheStore.Dispose();
    }

    internal void NotifyInvalidation(UiInvalidationType invalidationType, UIElement? source = null)
    {
        ObserveInvalidationDiagnostics(invalidationType, source);
        switch (invalidationType)
        {
            case UiInvalidationType.Measure:
                _hasMeasureInvalidation = true;
                _mustDrawNextFrame = true;
                MeasureInvalidationCount++;
                break;
            case UiInvalidationType.Arrange:
                _hasArrangeInvalidation = true;
                _mustDrawNextFrame = true;
                ArrangeInvalidationCount++;
                break;
            case UiInvalidationType.Render:
                _hasRenderInvalidation = true;
                _mustDrawNextFrame = true;
                RenderInvalidationCount++;
                ObserveRenderCacheInvalidationSource(source);
                if (source is TextBox textBox && textBox.IsFocused)
                {
                    _hasCaretBlinkInvalidation = true;
                }

                if (UseDirtyRegionRendering)
                {
                    TrackDirtyBoundsForVisual(source);
                }
                if (source != null)
                {
                    EnqueueDirtyRenderNode(source);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidationType), invalidationType, null);
        }
    }

    internal void NotifyVisualStructureChanged(UIElement element, UIElement? oldParent, UIElement? newParent)
    {
        if (!IsPartOfVisualTree(element) &&
            !IsPartOfVisualTree(oldParent) &&
            !IsPartOfVisualTree(newParent) &&
            !ReferenceEquals(element, _visualRoot))
        {
            return;
        }

        _renderListNeedsFullRebuild = true;
        _mustDrawNextFrame = true;
        ObserveDirtyRegionFallbackVisualStructureChange();
        _dirtyRegions.MarkFullFrameDirty(dueToFragmentation: false);
        _renderCacheStore.Clear();
        EnqueueDirtyRenderNode(element);
    }

}
