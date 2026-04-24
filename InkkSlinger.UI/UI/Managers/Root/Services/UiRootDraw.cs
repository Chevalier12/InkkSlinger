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

        DrawExecutedFrameCount++;

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
        _lastSpriteBatchRestartMs = 0d;
        _lastDrawClearMs = 0d;
        _lastDrawInitialBatchBeginMs = 0d;
        _lastDrawVisualTreeMs = 0d;
        _lastDrawCursorMs = 0d;
        _lastDrawFinalBatchEndMs = 0d;
        _lastDrawCleanupMs = 0d;
        UiDrawing.ResetFrameTelemetry();

        var graphicsDevice = spriteBatch.GraphicsDevice;
        if (!_hasLastLayoutViewport || !AreViewportsEqual(_lastLayoutViewport, graphicsDevice.Viewport))
        {
            SyncDirtyRegionViewport(graphicsDevice.Viewport);
        }

        var dirtyCoverage = _dirtyRegions.GetDirtyAreaCoverage();
        var usePartialClear = UseRetainedRenderList &&
                              UseDirtyRegionRendering &&
                              _fullRedrawSettleFramesRemaining <= 0 &&
                              !_dirtyRegions.IsFullFrameDirty &&
                              ShouldUsePartialDirtyRedraw(_dirtyRegions.RegionCount, dirtyCoverage);
        if (!usePartialClear)
        {
            var clearStart = Stopwatch.GetTimestamp();
            graphicsDevice.Clear(_clearColor);
            _lastDrawClearMs = Stopwatch.GetElapsedTime(clearStart).TotalMilliseconds;
        }

        var batchBeginStart = Stopwatch.GetTimestamp();
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearClamp,
            depthStencilState: DepthStencilState.None,
            rasterizerState: UiRasterizerState);
        _lastDrawInitialBatchBeginMs = Stopwatch.GetElapsedTime(batchBeginStart).TotalMilliseconds;
        UiDrawing.SetActiveBatchState(graphicsDevice,
            SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
            DepthStencilState.None, UiRasterizerState);
        try
        {
            UiDrawing.ResetState(graphicsDevice);
            var treeDrawStart = Stopwatch.GetTimestamp();
            if (UseRetainedRenderList)
            {
                SynchronizeRetainedRenderListForDrawIfNeeded();
                if (UseDirtyRegionRendering && usePartialClear)
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
            _lastDrawVisualTreeMs = Stopwatch.GetElapsedTime(treeDrawStart).TotalMilliseconds;

            if (UseSoftwareCursor)
            {
                var cursorDrawStart = Stopwatch.GetTimestamp();
                DrawSoftwareCursor(spriteBatch, _inputState.LastPointerPosition);
                _lastDrawCursorMs = Stopwatch.GetElapsedTime(cursorDrawStart).TotalMilliseconds;
            }
        }
        finally
        {
            UiDrawing.ClearActiveBatchState(graphicsDevice);
            var batchEndStart = Stopwatch.GetTimestamp();
            spriteBatch.End();
            _lastDrawFinalBatchEndMs = Stopwatch.GetElapsedTime(batchEndStart).TotalMilliseconds;
        }

        var drawingTelemetry = UiDrawing.ConsumeFrameTelemetry();
        _lastSpriteBatchRestartCount = drawingTelemetry.SpriteBatchRestartCount;
        _lastSpriteBatchRestartMs = (double)drawingTelemetry.SpriteBatchRestartElapsedTicks * 1000d / Stopwatch.Frequency;
        _lastRetainedClipPushCount = drawingTelemetry.ClipPushCount;

        var cleanupStart = Stopwatch.GetTimestamp();
        ApplyRenderInvalidationCleanupAfterDraw();
        _hasMeasureInvalidation = false;
        _hasArrangeInvalidation = false;
        _hasRenderInvalidation = false;
        _hasCaretBlinkInvalidation = false;
        _mustDrawNextFrame = false;
        ClearDirtyRenderQueue();
        ResetRetainedSyncTrackingState();
        _dirtyRegions.Clear();
        ConsumeFullRedrawSettleFrame();
        _lastDrawCleanupMs = Stopwatch.GetElapsedTime(cleanupStart).TotalMilliseconds;
        LastDrawMs = Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
    }

    private void SynchronizeRetainedRenderListForDrawIfNeeded()
    {
        if (_renderListNeedsFullRebuild || _dirtyRenderQueue.Count > 0 || _dirtyRenderSet.Count > 0)
        {
            EnsureVisualIndexCurrent();
            SynchronizeRetainedRenderList();
        }
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
            if (_dirtyRenderSet.Count == 0)
            {
                // When no roots were synchronized in this draw phase and nothing remains queued,
                // any surviving render flags were already synchronized earlier in the frame and
                // must not leak past cleanup.
                _visualRoot.ClearRenderInvalidationRecursive();
            }

            return;
        }

        for (var i = 0; i < _lastSynchronizedDirtyRenderRoots.Count; i++)
        {
            _lastSynchronizedDirtyRenderRoots[i].ClearRenderInvalidationRecursive();
        }

        BuildPendingDirtyAncestorCounts();
        for (var i = 0; i < _lastSynchronizedDirtyRenderRoots.Count; i++)
        {
            ClearRenderInvalidationAncestorChain(_lastSynchronizedDirtyRenderRoots[i].GetInvalidationParent());
        }

        _pendingDirtyAncestorCounts.Clear();
    }

    private void BuildPendingDirtyAncestorCounts()
    {
        _pendingDirtyAncestorCounts.Clear();

        for (var i = 0; i < _lastSynchronizedDirtyRenderRoots.Count; i++)
        {
            AddPendingDirtyAncestorChain(_lastSynchronizedDirtyRenderRoots[i]);
        }

        if (_dirtyRenderSet.Count == 0)
        {
            return;
        }

        foreach (var dirtyVisual in _dirtyRenderSet)
        {
            AddPendingDirtyAncestorChain(dirtyVisual);
        }
    }

    private void AddPendingDirtyAncestorChain(UIElement dirtyVisual)
    {
        for (var current = dirtyVisual.GetInvalidationParent(); current != null; current = current.GetInvalidationParent())
        {
            _pendingDirtyAncestorCounts.TryGetValue(current, out var count);
            _pendingDirtyAncestorCounts[current] = count + 1;
        }
    }

    private void ClearRenderInvalidationAncestorChain(UIElement? visual)
    {
        for (var current = visual; current != null; current = current.GetInvalidationParent())
        {
            if (!_pendingDirtyAncestorCounts.TryGetValue(current, out var remainingCount))
            {
                current.ClearRenderInvalidationShallow();
                continue;
            }

            remainingCount--;
            if (remainingCount > 0)
            {
                _pendingDirtyAncestorCounts[current] = remainingCount;
                continue;
            }

            _pendingDirtyAncestorCounts.Remove(current);
            current.ClearRenderInvalidationShallow();
        }
    }
}
