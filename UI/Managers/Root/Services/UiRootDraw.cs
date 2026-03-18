using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace InkkSlinger;

public sealed partial class UiRoot
{
    public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
    {
        Dispatcher.VerifyAccess();
        _ = gameTime;
        if (spriteBatch == null)
        {
            throw new ArgumentNullException(nameof(spriteBatch));
        }

        var drawStart = Stopwatch.GetTimestamp();
        DrawCalls = 1;
        LastDrawUsedPartialRedraw = false;
        LastDrawReasons = _scheduledDrawReasons;
        _scheduledDrawReasons = UiRedrawReason.None;
        _lastRetainedNodesVisited = 0;
        _lastRetainedNodesDrawn = 0;
        _lastRetainedClipPushCount = 0;
        _lastRetainedTraversalCount = 0;
        _lastDirtyRegionTraversalCount = 0;
        _lastSpriteBatchRestartCount = 0;
        UiDrawing.ResetFrameTelemetry();

        var graphicsDevice = spriteBatch.GraphicsDevice;
        if (!_hasLastLayoutViewport || !AreViewportsEqual(_lastLayoutViewport, graphicsDevice.Viewport))
        {
            SyncDirtyRegionViewport(graphicsDevice.Viewport);
        }

        var dirtyCoverage = _dirtyRegions.GetDirtyAreaCoverage();
        var usePartialClear = UseRetainedRenderList &&
                              UseDirtyRegionRendering &&
                              !_dirtyRegions.IsFullFrameDirty &&
                              ShouldUsePartialDirtyRedraw(_dirtyRegions.RegionCount, dirtyCoverage);
        if (!usePartialClear)
        {
            graphicsDevice.Clear(_clearColor);
        }

        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            depthStencilState: DepthStencilState.None,
            rasterizerState: UiRasterizerState);
        UiDrawing.SetActiveBatchState(graphicsDevice,
            SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, UiRasterizerState);
        try
        {
            UiDrawing.ResetState(graphicsDevice);
            if (UseRetainedRenderList)
            {
                if (UseDirtyRegionRendering)
                {
                    DrawRetainedRenderListWithDirtyRegions(spriteBatch);
                }
                else
                {
                    LastDirtyRectCount = 1;
                    LastDirtyAreaPercentage = 1d;
                    DrawRetainedRenderList(spriteBatch);
                }
            }
            else
            {
                _visualRoot.Draw(spriteBatch);

                LastDirtyRectCount = 1;
                LastDirtyAreaPercentage = 1d;
            }

            if (UseSoftwareCursor)
            {
                DrawSoftwareCursor(spriteBatch, _inputState.LastPointerPosition);
            }
        }
        finally
        {
            UiDrawing.ClearActiveBatchState(graphicsDevice);
            spriteBatch.End();
        }

        var drawingTelemetry = UiDrawing.ConsumeFrameTelemetry();
        _lastSpriteBatchRestartCount = drawingTelemetry.SpriteBatchRestartCount;
        _lastRetainedClipPushCount = drawingTelemetry.ClipPushCount;

        ApplyRenderInvalidationCleanupAfterDraw();
        _hasMeasureInvalidation = false;
        _hasArrangeInvalidation = false;
        _hasRenderInvalidation = false;
        _hasCaretBlinkInvalidation = false;
        _mustDrawNextFrame = false;
        ClearDirtyRenderQueue();
        ResetRetainedSyncTrackingState();
        _dirtyRegions.Clear();
        LastDrawMs = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
    }

    private void DrawSoftwareCursor(SpriteBatch spriteBatch, Vector2 pointer)
    {
        var size = MathF.Max(6f, SoftwareCursorSize);
        var half = size / 2f;
        var color = SoftwareCursorColor;
        const float thickness = 2f;

        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - half, pointer.Y - 1f, size, thickness), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - 1f, pointer.Y - half, thickness, size), color, 1f);
        UiDrawing.DrawFilledRect(spriteBatch, new LayoutRect(pointer.X - 1f, pointer.Y - 1f, thickness, thickness), Color.Black, 1f);
    }

    private void ApplyRenderInvalidationCleanupAfterDraw()
    {
        if (!UseRetainedRenderList || _dirtyRegions.IsFullFrameDirty || _lastRetainedSyncUsedFullRebuild)
        {
            _visualRoot.ClearRenderInvalidationRecursive();
            return;
        }

        if (_lastSynchronizedDirtyRenderRoots.Count == 0)
        {
            if (_dirtyRenderSet.Count == 0 && !_hasRenderInvalidation)
            {
                // Animation-only redraws can still occur after a previous retained draw left stale
                // render dirty flags on visuals. With no synchronized dirty roots and no newly
                // queued invalidations, those flags are stale and must be cleared here so future
                // InvalidateVisual calls are not short-circuited.
                _visualRoot.ClearRenderInvalidationRecursive();
            }

            return;
        }

        for (var i = 0; i < _lastSynchronizedDirtyRenderRoots.Count; i++)
        {
            _lastSynchronizedDirtyRenderRoots[i].ClearRenderInvalidationRecursive();
        }

        for (var i = 0; i < _lastSynchronizedDirtyRenderRoots.Count; i++)
        {
            ClearRenderInvalidationAncestorChain(
                _lastSynchronizedDirtyRenderRoots[i].GetInvalidationParent(),
                _lastSynchronizedDirtyRenderRoots[i]);
        }
    }

    private void ClearRenderInvalidationAncestorChain(UIElement? visual, UIElement sourceDirtyRoot)
    {
        for (var current = visual; current != null; current = current.GetInvalidationParent())
        {
            if (HasPendingDirtyVisualInSubtree(current, sourceDirtyRoot))
            {
                continue;
            }

            current.ClearRenderInvalidationShallow();
        }
    }

    private bool HasPendingDirtyVisualInSubtree(UIElement subtreeRoot, UIElement sourceDirtyRoot)
    {
        if (!_renderNodeIndices.TryGetValue(subtreeRoot, out var subtreeRootIndex))
        {
            return false;
        }

        var subtreeEnd = _retainedRenderList[subtreeRootIndex].SubtreeEndIndexExclusive;
        var excludedIndex = _renderNodeIndices.TryGetValue(sourceDirtyRoot, out var sourceDirtyRootIndex)
            ? sourceDirtyRootIndex
            : -1;
        for (var i = 0; i < _lastSynchronizedDirtyRenderSpans.Count; i++)
        {
            var span = _lastSynchronizedDirtyRenderSpans[i];
            if (span.StartIndex == excludedIndex)
            {
                continue;
            }

            if (span.StartIndex >= subtreeRootIndex && span.StartIndex < subtreeEnd)
            {
                return true;
            }
        }

        if (_dirtyRenderSet.Count == 0)
        {
            return false;
        }

        foreach (var dirtyVisual in _dirtyRenderSet)
        {
            if (ReferenceEquals(dirtyVisual, sourceDirtyRoot))
            {
                continue;
            }

            if (_renderNodeIndices.TryGetValue(dirtyVisual, out var dirtyRenderNodeIndex) &&
                dirtyRenderNodeIndex >= subtreeRootIndex &&
                dirtyRenderNodeIndex < subtreeEnd)
            {
                return true;
            }
        }

        return false;
    }
}
